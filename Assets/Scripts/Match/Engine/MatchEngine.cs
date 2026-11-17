using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Match
{
    public class MatchSetup
    {
        public string title = "試合";
        public MatchCategory category = MatchCategory.Kusa;
        public MatchTeam home;
        public MatchTeam away;
        /// <summary>主人公がいる側（観戦だけなら null）。</summary>
        public Side? protagonistSide;
        public int minutes = 90;
        public int seed = 1;
        /// <summary>観客数。多いほど派手なプレーが評判になり、八百長もばれやすい。</summary>
        public int crowd = 100;
        public bool underground;
        public int worldMatchId = -1;
        public List<IMatchMission> missions = new List<IMatchMission>();
        /// <summary>CPUチームの陣形・作戦を性格から自動で決める。</summary>
        public bool autoSelectCpuTactics = true;
        /// <summary>主人公がゴールを決めたときのパフォーマンス（null ならなし）。</summary>
        public SoccerIsland.Career.GoalPerformanceDesign celebration;
        /// <summary>「絶対王者」の弱点を見つけたか（見つけていないと勝てない）。</summary>
        public bool unbeatableBroken;
    }

    public class MatchChoice
    {
        public MatchCommand command;
        public MatchPlayer passTarget;
        public MatchChoice(MatchCommand c, MatchPlayer t = null) { command = c; passTarget = t; }
    }

    /// <summary>主人公のコマンドを自動で選ぶ（オート操作・テスト用）。</summary>
    public interface IMatchController
    {
        MatchChoice Decide(MatchEngine engine, MatchPrompt prompt);
    }

    /// <summary>
    /// コマンド選択式の試合エンジン。
    ///
    /// フィールドは攻撃方向に 0（自陣ゴール前）〜 6（相手ゴール前）の7ゾーン。
    /// ボールを持った選手が前に進むと、そのゾーンを守る相手と「対決」になる。
    /// 攻撃側：ドリブル／パス／ワンツー／シュート…、守備側：タックル／パスカット／ブロック／オフサイドトラップ…
    /// 能力値・テンション・気力・チームの雰囲気・運で勝敗が決まる。
    ///
    /// 使い方：Advance() を呼ぶと、主人公の判断が必要になるまで（または試合終了まで）進む。
    /// MatchPrompt が返ったら Choose() で選ぶ。Coach() はいつでも呼べる（クールダウンあり）。
    /// </summary>
    public class MatchEngine
    {
        // ---- 調整用パラメータ ----
        public float shotOffset = -12f;
        public float rollStdDev = 10f;
        public int coachingCooldown = 4;
        /// <summary>主人公にボール・相手が集まりやすくする倍率（RPGとして主人公の出番を増やす）。</summary>
        public float protagonistSpotlight = 4f;

        /// <summary>操作方式。既定は自チーム全員にコマンドを出すキャプテン翼式。</summary>
        public ControlMode Mode { get; set; } = ControlMode.FullTeam;
        /// <summary>ボールのある列：左(0)・中央(1)・右(2)。</summary>
        public int Lane { get; private set; } = 1;

        public MatchSetup Setup { get; }
        public MatchTeam Home => Setup.home;
        public MatchTeam Away => Setup.away;
        public float Minute { get; private set; }
        public bool Finished { get; private set; }
        public Side Possession { get; private set; }
        public MatchPlayer Holder { get; private set; }
        /// <summary>攻撃側から見たボールの位置（0〜6）。</summary>
        public int Zone { get; private set; }
        public MatchPrompt Pending { get; private set; }

        /// <summary>チームメイトから主人公への評価（0〜100）。</summary>
        public float Trust { get; private set; } = 50f;
        /// <summary>八百長の疑い（0〜100）。100で発覚。</summary>
        public float Suspicion { get; private set; }
        public int OffsideTrapsSucceeded { get; private set; }
        /// <summary>主人公がゴールパフォーマンスをした回数。</summary>
        public int CelebrationsPerformed { get; private set; }
        public IMatchController Controller { get; set; }

        public List<MatchEvent> Log { get; } = new List<MatchEvent>();
        public event Action<MatchEvent> OnEvent;

        readonly SeededRandom _rng;
        bool _secondHalf;
        int _lastCoachMinute = -100;
        int _chainActions;          // 主人公のプレーからの「チャンスにつながった」判定の残り手数
        bool _chainRewarded;
        MatchPlayer _lastPasser;    // アシスト判定
        PendingAction _pa;
        ShotStage _shot;
        AerialStage _aerial;

        class PendingAction
        {
            public MatchPlayer attacker, defender, target;
            public MatchCommand? atk, def;
        }

        /// <summary>シュートがキーパーまで届いた場面。</summary>
        class ShotStage
        {
            public MatchPlayer shooter, keeper;
            public float power;
            public MatchCommand? keeperCmd;
        }

        /// <summary>センタリングが上がった場面（空中戦）。</summary>
        class AerialStage
        {
            public MatchPlayer crosser, receiver, defender;
            public float cross;
            public MatchCommand? atk, def;
        }

        public MatchEngine(MatchSetup setup)
        {
            Setup = setup;
            setup.home.side = Side.Home;
            setup.away.side = Side.Away;
            _rng = new SeededRandom(setup.seed);
            foreach (var t in new[] { setup.home, setup.away })
            {
                foreach (var p in t.players) p.RememberNaturalPosition();
                bool cpu = !setup.protagonistSide.HasValue || setup.protagonistSide.Value != t.side;
                if (cpu && setup.autoSelectCpuTactics) FormationSystem.AutoSelect(t);
                t.chemistry = TeamChemistry.Evaluate(t.players);
                t.mood += t.chemistry.score * 0.5f;
            }
            ApplyBossTraits();
            ApplyTownRivalry();
            Emit(MatchEventType.Info, Side.Home, null, null, $"{setup.title}　{setup.home.name} vs {setup.away.name}　観客{setup.crowd}人");
            foreach (var m in setup.missions) m.OnStart(this);
            Kickoff(Side.Home);
        }

        public MatchTeam TeamOf(Side s) => s == Side.Home ? Setup.home : Setup.away;
        public MatchTeam Opponent(Side s) => s == Side.Home ? Setup.away : Setup.home;
        public MatchTeam ProtagonistTeam => Setup.protagonistSide.HasValue ? TeamOf(Setup.protagonistSide.Value) : null;
        public MatchPlayer Protagonist => ProtagonistTeam?.Protagonist;
        public bool FixingActive
        {
            get { foreach (var m in Setup.missions) if (m.AllowsFixingCommands) return true; return false; }
        }
        public int MinuteInt => (int)Minute;

        // ==================================================================
        // 進行
        // ==================================================================

        public MatchPrompt Advance()
        {
            while (!Finished)
            {
                if (Pending != null)
                {
                    if (Controller == null) return Pending;
                    var c = Controller.Decide(this, Pending);
                    Choose(c.command, c.passTarget);
                    continue;
                }

                if (_shot != null) { StepShot(); continue; }
                if (_aerial != null) { StepAerial(); continue; }

                if (_pa == null)
                {
                    if (CheckClock()) continue;
                    BeginAction();
                }

                bool reads = _pa.def == null && _pa.defender != null && !IsControllable(_pa.defender) &&
                             HasTrait(Opponent(Possession), BossTrait.GeniusVision);
                if (reads)
                {
                    // 天才の読み：攻撃側のコマンドを見てから守る
                    if (_pa.atk == null)
                    {
                        if (IsControllable(_pa.attacker)) { Pending = AttackPrompt(); continue; }
                        AiAttack(_pa);
                    }
                    _pa.def = _rng.Chance(0.45f) ? CounterOf(_pa.atk.Value) : AiDefense(_pa.defender);
                    if (_pa.def == MatchCommand.Block && Zone < 4) _pa.def = MatchCommand.Tackle;
                }

                if (_pa.def == null)
                {
                    if (_pa.defender != null && IsControllable(_pa.defender))
                    {
                        Pending = DefensePrompt();
                        continue;
                    }
                    _pa.def = AiDefense(_pa.defender);
                }

                if (_pa.atk == null)
                {
                    if (IsControllable(_pa.attacker))
                    {
                        Pending = AttackPrompt();
                        continue;
                    }
                    AiAttack(_pa);
                }

                var pa = _pa;
                _pa = null;
                Resolve(pa);
            }
            return null;
        }

        void StepShot()
        {
            if (_shot.keeperCmd == null)
            {
                if (_shot.keeper != null && IsControllable(_shot.keeper)) { Pending = KeeperPrompt(); return; }
                _shot.keeperCmd = AiKeeper(_shot.keeper, _shot.power);
            }
            var s = _shot;
            _shot = null;
            ResolveKeeper(s);
        }

        void StepAerial()
        {
            if (_aerial.def == null)
            {
                if (_aerial.defender != null && IsControllable(_aerial.defender)) { Pending = AerialDefendPrompt(); return; }
                _aerial.def = AiAerialDefense(_aerial.defender);
            }
            if (_aerial.atk == null)
            {
                if (IsControllable(_aerial.receiver)) { Pending = AerialPrompt(); return; }
                _aerial.atk = AiAerialAttack(_aerial.receiver);
            }
            var a = _aerial;
            _aerial = null;
            ResolveAerial(a);
        }

        /// <summary>最後まで進める（主人公はコントローラーか、いなければオートで）。</summary>
        public MatchResult RunToEnd()
        {
            if (Controller == null) Controller = new AutoPilot();
            Advance();
            return Result();
        }

        public void Choose(MatchCommand command, MatchPlayer passTarget = null)
        {
            if (Pending == null) throw new InvalidOperationException("選択待ちではありません");
            if (!Pending.options.Contains(command)) throw new ArgumentException("選べないコマンド: " + command);
            var prompt = Pending;
            Pending = null;
            switch (prompt.kind)
            {
                case PromptKind.Defend:
                    _pa.def = Obey(prompt.actor, command, false);
                    break;
                case PromptKind.Attack:
                    var cmd = Obey(prompt.actor, command, true);
                    _pa.atk = cmd;
                    if (IsPass(cmd) || cmd == MatchCommand.Centering)
                        _pa.target = cmd == command && passTarget != null && prompt.passTargets.Contains(passTarget)
                            ? passTarget : PickPassTarget(_pa.attacker, cmd);
                    break;
                case PromptKind.Keeper:
                    _shot.keeperCmd = command;
                    break;
                case PromptKind.Aerial:
                    _aerial.atk = command;
                    break;
                case PromptKind.AerialDefend:
                    _aerial.def = command;
                    break;
            }
        }

        /// <summary>
        /// 指示を聞くかどうか。主人公は必ず従う。献身的は必ず聞く。
        /// エゴイストはパスを命じられても自分で行くことがあり、センス的は地味な指示を嫌う。
        /// チームメイトの評価（信頼）が低いと、誰でも聞かなくなる。
        /// </summary>
        MatchCommand Obey(MatchPlayer p, MatchCommand ordered, bool attacking)
        {
            if (p.isProtagonist || Mode == ControlMode.ProtagonistOnly) return ordered;
            float disobey = Trust < 30f ? (30f - Trust) / 100f : 0f;
            MatchCommand replacement = ordered;
            string line = null;
            switch (p.soccerPersonality)
            {
                case SoccerPersonality.Devoted:
                    return ordered;
                case SoccerPersonality.Egoist:
                    if (attacking && Zone >= 3 && (IsPass(ordered) || ordered == MatchCommand.Keep || ordered == MatchCommand.Centering))
                    {
                        disobey += 0.35f;
                        replacement = Zone >= 5 ? MatchCommand.Shoot : MatchCommand.Dribble;
                        line = "うるせえ、俺が行く！";
                    }
                    break;
                case SoccerPersonality.Sense:
                case SoccerPersonality.Gifted:
                    if (attacking && (ordered == MatchCommand.Keep || ordered == MatchCommand.BackPass))
                    {
                        disobey += 0.4f;
                        replacement = MatchCommand.FlairDribble;
                        line = "つまらない。こっちの方が面白い。";
                    }
                    break;
                case SoccerPersonality.Hysteric:
                    if (!attacking && p.tension > 80f && ordered != MatchCommand.Tackle)
                    {
                        disobey += 0.4f;
                        replacement = MatchCommand.Tackle;
                        line = "（カッとなって突っ込んだ！）";
                    }
                    break;
                case SoccerPersonality.Fighter:
                    if (!attacking && ordered == MatchCommand.Wait)
                    {
                        disobey += 0.5f;
                        replacement = MatchCommand.Tackle;
                        line = "見てるだけなんてできるか！";
                    }
                    break;
            }
            if (replacement == ordered && disobey > 0f)
            {
                // 信頼が低いだけの命令無視：自分の判断で動く
                replacement = attacking ? (Zone >= 5 ? MatchCommand.Shoot : MatchCommand.Dribble) : MatchCommand.Tackle;
                line = "……勝手にやらせてもらう。";
            }
            if (replacement == ordered || !_rng.Chance(disobey)) return ordered;
            if (replacement == MatchCommand.Shoot && Zone < 3) replacement = MatchCommand.Dribble;
            Emit(MatchEventType.Disobey, SideOf(p), p, null, $"{p.name}「{line}」（指示を無視した）");
            var team = TeamOf(SideOf(p));
            team.mood = Math.Max(-100f, team.mood - 2f);
            return replacement;
        }

        bool IsControllable(MatchPlayer p)
        {
            if (p == null || !p.CanTouchBall || ProtagonistTeam == null) return false;
            if (Mode == ControlMode.ProtagonistOnly) return p.isProtagonist;
            return ProtagonistTeam.players.Contains(p);
        }

        static bool IsPass(MatchCommand c) =>
            c == MatchCommand.Pass || c == MatchCommand.LongPass || c == MatchCommand.OneTwo || c == MatchCommand.BackPass || c == MatchCommand.SloppyPass;

        bool CheckClock()
        {
            if (!_secondHalf && Minute >= Setup.minutes / 2f)
            {
                _secondHalf = true;
                Emit(MatchEventType.HalfTime, Side.Home, null, null, $"前半終了　{Home.score} - {Away.score}");
                foreach (var t in new[] { Home, Away })
                    foreach (var p in t.Active)
                    {
                        p.guts = Math.Min(p.maxGuts, p.guts + p.maxGuts * 3 / 10);
                        p.demand = null;
                    }
                Kickoff(Side.Away);
                return true;
            }
            if (Minute >= Setup.minutes)
            {
                Finished = true;
                Emit(MatchEventType.FullTime, Side.Home, null, null, $"試合終了　{Home.name} {Home.score} - {Away.score} {Away.name}");
                return true;
            }
            return false;
        }

        void BeginAction()
        {
            int before = MinuteInt;
            Minute += 1f + _rng.Value * 0.8f;
            if (MinuteInt != before)
                foreach (var m in Setup.missions) m.OnMinute(this, MinuteInt);

            UpdateMood(Home);
            UpdateMood(Away);
            foreach (var t in new[] { Home, Away })
            {
                if (t.instruction != TeamInstruction.None && MinuteInt > t.instructionUntilMinute) t.instruction = TeamInstruction.None;
                if (t.focusPlayer != null && MinuteInt > t.focusUntilMinute) t.focusPlayer = null;
                var opp = Opponent(t.side);
                foreach (var p in t.Active)
                    if (Coaching.UpdateDemand(p, t, opp, MinuteInt, _rng) && t == ProtagonistTeam)
                        Emit(MatchEventType.Demand, t.side, p, null, $"{p.name}「{p.demandLine}」");
            }

            if (Holder == null || !Holder.CanTouchBall) Holder = PickCarrier(TeamOf(Possession), Zone);
            if (Holder != null && Holder.position == Position.GK)
            {
                // キーパーはボールを持ち上がらず、味方へフィードする
                var keeper = Holder;
                Zone = Math.Max(Zone, 1);
                Holder = PickCarrier(TeamOf(Possession), Zone) ?? keeper;
                if (Holder != keeper) Emit(MatchEventType.Pass, Possession, keeper, Holder, $"{keeper.name}から{Holder.name}へフィード");
            }
            _pa = new PendingAction { attacker = Holder, defender = PickDefender(Opponent(Possession), Zone) };
        }

        /// <summary>ムードメーカーのテンションがチームの雰囲気を引っ張る。</summary>
        void UpdateMood(MatchTeam t)
        {
            float target = t.chemistry.score * 0.5f + (t.score - Opponent(t.side).score) * 6f;
            foreach (var p in t.Active)
                if (p.soccerPersonality == SoccerPersonality.MoodMaker) target += (p.tension - 50f) * 0.6f;
            t.mood += (target - t.mood) * 0.08f;
            t.mood = Math.Max(-100f, Math.Min(100f, t.mood));
            foreach (var p in t.Active) p.tension += (60f - p.tension) * 0.01f;
        }

        // ==================================================================
        // 選手の配置
        // ==================================================================

        /// <summary>守備側から見たゾーンに応じて、守る選手を選ぶ。</summary>
        MatchPlayer PickDefender(MatchTeam def, int attackZone)
        {
            int z = 6 - attackZone; // 守備側から見た位置
            var list = new List<MatchPlayer>();
            foreach (var p in def.Active)
            {
                if (!p.CanTouchBall) continue;
                if (Covers(p.position, z, true)) list.Add(p);
            }
            return PickWeighted(list);
        }

        static bool Covers(Position pos, int z, bool defending)
        {
            switch (pos)
            {
                case Position.GK: return z == 0;
                case Position.DF: return z >= 0 && z <= 2;
                case Position.MF: return z >= 2 && z <= 4;
                default: return defending ? z >= 4 : z >= 4;
            }
        }

        MatchPlayer PickCarrier(MatchTeam t, int zone)
        {
            var list = new List<MatchPlayer>();
            foreach (var p in t.Active)
                if (p.CanTouchBall && p.position != Position.GK && Covers(p.position, zone, false)) list.Add(p);
            if (list.Count == 0)
                foreach (var p in t.Active) if (p.CanTouchBall && p.position != Position.GK) list.Add(p);
            if (list.Count == 0)
                foreach (var p in t.Active) if (p.CanTouchBall) list.Add(p);
            return PickWeighted(list);
        }

        MatchPlayer PickWeighted(List<MatchPlayer> list)
        {
            if (list.Count == 0) return null;
            var w = new float[list.Count];
            for (int i = 0; i < list.Count; i++) w[i] = list[i].isProtagonist ? protagonistSpotlight : 1f;
            return list[_rng.WeightedIndex(w)];
        }

        void Kickoff(Side side)
        {
            Possession = side;
            Zone = 3;
            Lane = 1;
            Holder = PickCarrier(TeamOf(side), 3);
            _lastPasser = null;
            Emit(MatchEventType.Kickoff, side, Holder, null, $"{TeamOf(side).name}のキックオフ");
        }

        void Turnover(Side newSide, MatchPlayer newHolder, int zone)
        {
            Possession = newSide;
            Zone = Math.Max(0, Math.Min(6, zone));
            Lane = _rng.Range(0, 3);
            Holder = newHolder != null && newHolder.CanTouchBall ? newHolder : PickCarrier(TeamOf(newSide), Zone);
            _lastPasser = null;
        }

        // ==================================================================
        // プロンプト（主人公の選択肢）
        // ==================================================================

        MatchPrompt NewPrompt(PromptKind kind, MatchPlayer actor, MatchPlayer opponent) =>
            new MatchPrompt { kind = kind, actor = actor, opponent = opponent, zone = Zone, minute = MinuteInt, lane = Lane, guts = actor.guts, maxGuts = actor.maxGuts };

        bool HasSpecial(MatchPlayer p, SpecialKind kind)
        {
            var m = SpecialMoveLibrary.Find(p.specials, kind);
            return m != null && p.guts >= m.gutsCost;
        }

        MatchPrompt AttackPrompt()
        {
            var p = _pa.attacker;
            var pr = NewPrompt(PromptKind.Attack, p, _pa.defender);
            pr.options.Add(MatchCommand.Dribble);
            pr.options.Add(MatchCommand.FlairDribble);
            if (HasSpecial(p, SpecialKind.Dribble)) pr.options.Add(MatchCommand.SpecialDribble);
            pr.passTargets = PassTargets(p);
            if (pr.passTargets.Count > 0)
            {
                pr.options.Add(MatchCommand.Pass);
                if (Zone <= 4) pr.options.Add(MatchCommand.LongPass);
                pr.options.Add(MatchCommand.OneTwo);
                if (Zone >= 4 && Lane != 1) pr.options.Add(MatchCommand.Centering);
                if (Zone >= 1) pr.options.Add(MatchCommand.BackPass);
            }
            if (Zone >= 3) pr.options.Add(MatchCommand.Shoot);
            if (Zone >= 4 && CanSpecial(p)) pr.options.Add(MatchCommand.SpecialShot);
            pr.options.Add(MatchCommand.Keep);
            if (FixingActive && p.isProtagonist)
            {
                if (Zone >= 3) pr.options.Add(MatchCommand.MissOnPurpose);
                pr.options.Add(MatchCommand.SloppyPass);
            }
            pr.situation = _pa.defender != null
                ? $"{p.name}（{ZoneName(Zone)}・{LaneName(Lane)}）　{_pa.defender.name}が立ちはだかる！"
                : $"{p.name}（{ZoneName(Zone)}・{LaneName(Lane)}）　前が空いている！";
            return pr;
        }

        MatchPrompt DefensePrompt()
        {
            var d = _pa.defender;
            var pr = NewPrompt(PromptKind.Defend, d, _pa.attacker);
            pr.options.Add(MatchCommand.Tackle);
            if (HasSpecial(d, SpecialKind.Tackle)) pr.options.Add(MatchCommand.SpecialTackle);
            pr.options.Add(MatchCommand.PassCut);
            if (Zone >= 4) pr.options.Add(MatchCommand.Block);
            if (Zone >= 4 && HasSpecial(d, SpecialKind.Block)) pr.options.Add(MatchCommand.SpecialBlock);
            if (Zone >= 2 && Zone <= 5 && d.position == Position.DF) pr.options.Add(MatchCommand.OffsideTrap);
            pr.options.Add(MatchCommand.Wait);
            pr.situation = $"{_pa.attacker.name}がボールを持って向かってくる！　{d.name}はどうする？（{ZoneName(Zone)}）";
            return pr;
        }

        MatchPrompt KeeperPrompt()
        {
            var k = _shot.keeper;
            var pr = NewPrompt(PromptKind.Keeper, k, _shot.shooter);
            pr.options.Add(MatchCommand.Catch);
            pr.options.Add(MatchCommand.Punch);
            if (HasSpecial(k, SpecialKind.Save)) pr.options.Add(MatchCommand.SpecialSave);
            pr.situation = $"{_shot.shooter.name}のシュートが来る！　キーパー{k.name}！";
            return pr;
        }

        MatchPrompt AerialPrompt()
        {
            var r = _aerial.receiver;
            var pr = NewPrompt(PromptKind.Aerial, r, _aerial.defender);
            pr.options.Add(MatchCommand.Header);
            pr.options.Add(MatchCommand.Volley);
            pr.options.Add(MatchCommand.Trap);
            pr.situation = $"{_aerial.crosser.name}のセンタリング！　{r.name}が飛び込む！";
            return pr;
        }

        MatchPrompt AerialDefendPrompt()
        {
            var d = _aerial.defender;
            var pr = NewPrompt(PromptKind.AerialDefend, d, _aerial.receiver);
            pr.options.Add(MatchCommand.Clear);
            pr.options.Add(MatchCommand.Compete);
            pr.options.Add(MatchCommand.PassCut);
            pr.situation = $"{_aerial.crosser.name}がセンタリングを上げた！　{d.name}、{_aerial.receiver.name}と競る！";
            return pr;
        }

        static string LaneName(int lane) => lane == 0 ? "左サイド" : lane == 2 ? "右サイド" : "中央";

        static string ZoneName(int zone)
        {
            switch (zone)
            {
                case 0: return "自陣ゴール前";
                case 1: return "自陣深く";
                case 2: return "自陣";
                case 3: return "センターサークル";
                case 4: return "敵陣";
                case 5: return "ペナルティエリア手前";
                default: return "ゴール前";
            }
        }

        List<MatchPlayer> PassTargets(MatchPlayer from)
        {
            var list = new List<MatchPlayer>();
            foreach (var p in TeamOf(Possession).Active)
                if (p != from && p.CanTouchBall && p.position != Position.GK) list.Add(p);
            return list;
        }

        public bool CanSpecial(MatchPlayer p) => HasSpecial(p, SpecialKind.Shot);

        // ==================================================================
        // AI
        // ==================================================================

        bool Follows(MatchTeam t) => _rng.Chance(t == ProtagonistTeam ? 0.5f + Trust / 200f : 0.8f);

        void AiAttack(PendingAction pa)
        {
            var p = pa.attacker;
            var team = TeamOf(Possession);
            var instr = Follows(team) ? team.instruction : TeamInstruction.None;

            float shoot = Zone >= 6 ? 75 : Zone == 5 ? 55 : Zone == 4 ? 18 : 0;
            float dribble = 40, pass = 40, longPass = Zone <= 3 ? 10 : 0, oneTwo = 8, keep = 4;
            float centering = Zone >= 4 && Lane != 1 ? 30 : 0;

            switch (p.soccerPersonality)
            {
                case SoccerPersonality.Egoist: dribble *= 1.6f; pass *= 0.4f; shoot *= 1.4f; centering *= 0.3f; break;
                case SoccerPersonality.Devoted: pass *= 1.5f; dribble *= 0.6f; shoot *= 0.6f; centering *= 1.5f; break;
                case SoccerPersonality.Sense:
                case SoccerPersonality.Gifted: oneTwo *= 2.5f; dribble *= 1.2f; break;
                case SoccerPersonality.Fighter: dribble *= 1.2f; break;
            }
            switch (p.role)
            {
                case PlayerRole.Support: pass *= 1.6f; dribble *= 0.6f; shoot *= 0.6f; centering *= 1.5f; break;
                case PlayerRole.Free: dribble *= 1.3f; oneTwo *= 1.5f; break;
            }
            switch (instr)
            {
                case TeamInstruction.GoForward: shoot *= 1.3f; longPass *= 1.5f; centering *= 1.3f; break;
                case TeamInstruction.DefendDeep: keep *= 3f; longPass *= 1.5f; break;
                case TeamInstruction.PassAround: pass *= 1.8f; dribble *= 0.5f; break;
                case TeamInstruction.TakeOn: dribble *= 1.8f; break;
                case TeamInstruction.FeedProtagonist: pass *= 1.5f; break;
            }
            if (pa.defender == null) dribble *= 1.5f;
            if (p.guts < GutsCost(MatchCommand.Shoot)) shoot = 0;
            var targets = PassTargets(p);
            if (targets.Count == 0) pass = longPass = oneTwo = centering = 0;

            var tw = FormationSystem.AttackWeights(team.tactic, Lane);
            int i = _rng.WeightedIndex(new[] { dribble * tw[0], pass * tw[1], longPass * tw[2], oneTwo * tw[3], shoot * tw[4], keep * tw[5], centering * tw[6] });
            switch (i)
            {
                case 0: pa.atk = HasSpecial(p, SpecialKind.Dribble) && pa.defender != null && _rng.Chance(0.3f) ? MatchCommand.SpecialDribble : MatchCommand.Dribble; break;
                case 1: pa.atk = MatchCommand.Pass; break;
                case 2: pa.atk = MatchCommand.LongPass; break;
                case 3: pa.atk = MatchCommand.OneTwo; break;
                case 4: pa.atk = CanSpecial(p) && _rng.Chance(0.4f) ? MatchCommand.SpecialShot : MatchCommand.Shoot; break;
                case 5: pa.atk = MatchCommand.Keep; break;
                default: pa.atk = MatchCommand.Centering; break;
            }
            if (IsPass(pa.atk.Value) || pa.atk == MatchCommand.Centering) pa.target = PickPassTarget(p, pa.atk.Value);
        }

        MatchPlayer PickPassTarget(MatchPlayer from, MatchCommand cmd)
        {
            var team = TeamOf(Possession);
            var targets = PassTargets(from);
            if (targets.Count == 0) return null;
            var w = new float[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                float x = 1f;
                if (cmd != MatchCommand.BackPass)
                {
                    if (t.position == Position.FW) x = 2f;
                    else if (t.position == Position.MF) x = 1.5f;
                    else x = 0.6f;
                }
                else x = t.position == Position.DF ? 2f : 1f;
                if (t.isProtagonist) x *= protagonistSpotlight * Trust / 50f * (team.instruction == TeamInstruction.FeedProtagonist ? 2.5f : 1f);
                if (team.focusPlayer == t) x *= 3f;
                if (from.soccerPersonality == SoccerPersonality.Egoist && t.soccerPersonality == SoccerPersonality.Egoist) x *= 0.3f;
                w[i] = Math.Max(0.05f, x);
            }
            return targets[_rng.WeightedIndex(w)];
        }

        MatchCommand AiDefense(MatchPlayer d)
        {
            if (d == null) return MatchCommand.Wait;
            var team = Opponent(Possession);
            var instr = Follows(team) ? team.instruction : TeamInstruction.None;
            float tackle = 50, cut = 32, block = Zone >= 4 ? 25 : 0, trap = 0, wait = 6;
            if (d.soccerPersonality == SoccerPersonality.Fighter || d.role == PlayerRole.Physical) { tackle *= 1.5f; block *= 1.5f; }
            if (instr == TeamInstruction.PlayPhysical) { tackle *= 1.3f; block *= 1.3f; }
            if (d.position == Position.DF && Zone >= 2 && Zone <= 5)
                trap = (team.offsideTrapPlanned ? 12 : 3) * (instr == TeamInstruction.RaiseLine ? 3 : 1);
            if (d.position == Position.GK) { tackle = 30; cut = 20; block = 50; trap = 0; }
            if (d.guts < 40) { tackle *= 0.5f; wait *= 3; }
            int i = _rng.WeightedIndex(new[] { tackle, cut, block, trap, wait });
            switch (i)
            {
                case 0: return HasSpecial(d, SpecialKind.Tackle) && _rng.Chance(0.3f) ? MatchCommand.SpecialTackle : MatchCommand.Tackle;
                case 1: return MatchCommand.PassCut;
                case 2: return HasSpecial(d, SpecialKind.Block) && _rng.Chance(0.4f) ? MatchCommand.SpecialBlock : MatchCommand.Block;
                case 3: return MatchCommand.OffsideTrap;
                default: return MatchCommand.Wait;
            }
        }

        MatchCommand AiKeeper(MatchPlayer k, float shot)
        {
            if (k == null) return MatchCommand.Catch;
            float save = k.stats.goalkeeping * 0.8f + k.stats.mental * 0.2f;
            if (shot > save && HasSpecial(k, SpecialKind.Save) && _rng.Chance(0.6f)) return MatchCommand.SpecialSave;
            return shot > save + 5f ? MatchCommand.Punch : MatchCommand.Catch;
        }

        MatchCommand AiAerialAttack(MatchPlayer r)
        {
            float header = 40 + (r.stats.physical - 50) * 0.8f, volley = 25 + (r.stats.technique - 50) * 0.8f, trap = 20;
            if (r.soccerPersonality == SoccerPersonality.Devoted) trap *= 1.5f;
            int i = _rng.WeightedIndex(new[] { Math.Max(1f, header), Math.Max(1f, volley), trap });
            return i == 0 ? MatchCommand.Header : i == 1 ? MatchCommand.Volley : MatchCommand.Trap;
        }

        MatchCommand AiAerialDefense(MatchPlayer d)
        {
            if (d == null) return MatchCommand.Clear;
            int i = _rng.WeightedIndex(new[] { 50f, 30f, 20f });
            return i == 0 ? MatchCommand.Clear : i == 1 ? MatchCommand.Compete : MatchCommand.PassCut;
        }

        // ==================================================================
        // 解決
        // ==================================================================

        public static int GutsCost(MatchCommand c)
        {
            switch (c)
            {
                case MatchCommand.Dribble: return 40;
                case MatchCommand.FlairDribble: return 60;
                case MatchCommand.Pass: return 20;
                case MatchCommand.LongPass: return 30;
                case MatchCommand.OneTwo: return 40;
                case MatchCommand.Centering: return 30;
                case MatchCommand.Shoot: return 60;
                case MatchCommand.SpecialShot: return 160;
                case MatchCommand.SpecialDribble: return 120;
                case MatchCommand.Keep: return 5;
                case MatchCommand.BackPass: return 10;
                case MatchCommand.Header: return 50;
                case MatchCommand.Volley: return 60;
                case MatchCommand.Trap: return 20;
                case MatchCommand.Tackle: return 35;
                case MatchCommand.SpecialTackle: return 110;
                case MatchCommand.PassCut: return 25;
                case MatchCommand.Block: return 35;
                case MatchCommand.SpecialBlock: return 110;
                case MatchCommand.OffsideTrap: return 20;
                case MatchCommand.Clear: return 30;
                case MatchCommand.Compete: return 35;
                case MatchCommand.Catch: return 30;
                case MatchCommand.Punch: return 30;
                case MatchCommand.SpecialSave: return 140;
                default: return 10;
            }
        }

        static SpecialKind? SpecialKindOf(MatchCommand c)
        {
            switch (c)
            {
                case MatchCommand.SpecialShot: return SpecialKind.Shot;
                case MatchCommand.SpecialDribble: return SpecialKind.Dribble;
                case MatchCommand.SpecialTackle: return SpecialKind.Tackle;
                case MatchCommand.SpecialBlock: return SpecialKind.Block;
                case MatchCommand.SpecialSave: return SpecialKind.Save;
                default: return null;
            }
        }

        /// <summary>必殺技を使うならその技（ガッツが足りなければ null）。</summary>
        SpecialMove UseSpecial(MatchPlayer p, MatchCommand c)
        {
            var kind = SpecialKindOf(c);
            if (!kind.HasValue || p == null) return null;
            var m = SpecialMoveLibrary.Find(p.specials, kind.Value);
            return m != null && p.guts >= m.gutsCost ? m : null;
        }

        float Perf(MatchPlayer p)
        {
            var t = TeamOf(SideOf(p));
            float tension = 0.85f + 0.3f * p.tension / 100f;
            float guts = p.GutsRatio < 0.25f ? 0.75f : 1f;
            float mood = 1f + t.mood / 500f;
            float pos = p.OutOfPosition ? 0.92f : 1f;
            return tension * guts * mood * pos;
        }

        Side SideOf(MatchPlayer p) => Home.players.Contains(p) ? Side.Home : Side.Away;

        float Roll(float v) => v + _rng.Gaussian(0f, rollStdDev);

        void Spend(MatchPlayer p, MatchCommand c, SpecialMove special = null)
        {
            if (p == null) return;
            int cost = special != null ? special.gutsCost : GutsCost(c);
            cost = (int)Math.Round(cost * FormationSystem.GutsDrain(TeamOf(SideOf(p))));
            p.guts = Math.Max(0, p.guts - cost);
        }

        static void Exp(MatchPlayer p, int amount)
        {
            if (p != null && p.isProtagonist) p.exp += amount;
        }

        void Resolve(PendingAction pa)
        {
            var a = pa.attacker;
            var d = pa.defender;
            var cmd = pa.atk.Value;
            var def = pa.def.Value;
            var atkSpecial = UseSpecial(a, cmd);
            var defSpecial = UseSpecial(d, def);
            // 必殺技のガッツが足りなければ通常技になる
            if (SpecialKindOf(cmd).HasValue && atkSpecial == null) cmd = cmd == MatchCommand.SpecialShot ? MatchCommand.Shoot : MatchCommand.Dribble;
            if (SpecialKindOf(def).HasValue && defSpecial == null) def = def == MatchCommand.SpecialBlock ? MatchCommand.Block : MatchCommand.Tackle;
            Spend(a, cmd, atkSpecial);
            if (d != null) Spend(d, def, defSpecial);
            a.lastTouchMinute = MinuteInt;
            if (defSpecial != null)
                Emit(MatchEventType.Info, Opponent(Possession).side, d, a, $"{d.name}「{defSpecial.shout}」　{defSpecial.name}！", 0f, true, defSpecial.name, defSpecial.shout);

            bool protagonistTeamAttacking = ProtagonistTeam != null && Possession == Setup.protagonistSide;
            if (protagonistTeamAttacking && !a.isProtagonist && _chainActions > 0) _chainActions--;

            if (d != null && d.isProtagonist && def == MatchCommand.Wait && Zone >= 3)
                Passive(d, "見ているだけ");

            switch (cmd)
            {
                case MatchCommand.Dribble:
                case MatchCommand.FlairDribble:
                case MatchCommand.SpecialDribble:
                    ResolveDribble(a, d, def, cmd, atkSpecial, defSpecial);
                    break;
                case MatchCommand.Pass:
                case MatchCommand.LongPass:
                case MatchCommand.OneTwo:
                    ResolvePass(a, pa.target, d, cmd, def);
                    break;
                case MatchCommand.Centering:
                    ResolveCentering(a, pa.target, d, def);
                    break;
                case MatchCommand.BackPass:
                    if (a.isProtagonist && Zone >= 4) Passive(a, "バックパス");
                    Emit(MatchEventType.Pass, Possession, a, pa.target, $"{a.name}、{pa.target?.name}へバックパス");
                    _lastPasser = a;
                    Holder = pa.target ?? a;
                    Zone = Math.Max(0, Zone - 1);
                    break;
                case MatchCommand.Keep:
                    ResolveKeep(a, d, def);
                    break;
                case MatchCommand.Shoot:
                case MatchCommand.SpecialShot:
                case MatchCommand.MissOnPurpose:
                    ResolveShot(a, d, def, cmd, 0f, atkSpecial, defSpecial);
                    break;
                case MatchCommand.SloppyPass:
                    AddSuspicion(12f, "不自然なパスミス");
                    Emit(MatchEventType.PassIntercepted, Possession, a, d, $"{a.name}のパスが相手へ……！");
                    LoseBall(a, d);
                    break;
            }
        }

        void ResolveDribble(MatchPlayer a, MatchPlayer d, MatchCommand def, MatchCommand cmd, SpecialMove special, SpecialMove defSpecial)
        {
            bool flair = cmd == MatchCommand.FlairDribble;
            float att = (a.stats.dribbling * 0.5f + a.stats.technique * 0.25f + a.stats.speed * 0.25f) * Perf(a) - (flair ? 8f : 0f) + (special?.power ?? 0)
                        + FormationSystem.AttackBonus(TeamOf(Possession), cmd, Zone, Lane);
            if (special != null)
                Emit(MatchEventType.Dribble, Possession, a, d, $"{a.name}「{special.shout}」　{special.name}！", 0f, true, special.name, special.shout);

            bool won;
            bool tackling = def == MatchCommand.Tackle || def == MatchCommand.SpecialTackle;
            if (d == null) won = true;
            else if (tackling)
            {
                if (CheckFoul(d, a)) return;
                float tk = (d.stats.defense * 0.55f + d.stats.physical * 0.25f + d.stats.speed * 0.2f) * Perf(d) + (defSpecial?.power ?? 0)
                           + FormationSystem.DefenseBonus(Opponent(Possession), def, Zone);
                won = Roll(att) > Roll(tk);
                if (!won) d.tacklesWon++;
            }
            else if (def == MatchCommand.Wait) won = _rng.Chance(0.75f);
            else won = true; // 読みが外れた

            if (!won)
            {
                Emit(MatchEventType.DribbleBeaten, Possession, d, a, $"{d.name}のタックル！ ボールを奪った！", 0f, defSpecial != null, defSpecial?.name, null);
                if (d.isProtagonist) { ChangeTrust(5f, "タックル成功"); d.rating += 0.2f; }
                Exp(d, 5);
                LoseBall(a, d);
                return;
            }

            int adv = def == MatchCommand.OffsideTrap ? 2 : 1;
            Zone = Math.Min(6, Zone + adv);
            if (_rng.Chance(0.3f)) Lane = Math.Max(0, Math.Min(2, Lane + (_rng.Chance(0.5f) ? 1 : -1)));
            string how = special != null ? special.name : flair ? "華麗なドリブル" : "ドリブル";
            Emit(MatchEventType.Dribble, Possession, a, d, d != null ? $"{a.name}の{how}！ {d.name}を抜いた！" : $"{a.name}、{how}で持ち上がる");
            if (def == MatchCommand.OffsideTrap && d != null)
                Emit(MatchEventType.OffsideTrapFail, Opponent(Possession).side, d, a, "オフサイドトラップ失敗！ ドリブルで抜け出された！");

            bool beautiful = d != null && (special != null || (flair ? _rng.Chance(0.8f) : _rng.Chance(a.stats.technique / 140f)));
            if (beautiful)
            {
                a.beautifulSkills++;
                a.rating += 0.2f;
                Emit(MatchEventType.BeautifulSkill, Possession, a, d, Setup.crowd >= 300 ? "観客席から大歓声！" : "綺麗な足技に見ている人が沸いた！");
            }
            Exp(a, special != null ? 8 : flair ? 6 : 4);

            if (a.isProtagonist)
            {
                a.rating += 0.05f;
                // ドリブル突破はチームメイトに「独りよがり」と思われる。ここからチャンスを作れば評価は一気に上がる
                ChangeTrust(flair ? -4f : -3f, "独りよがりなドリブル");
                _chainActions = 3;
                _chainRewarded = false;
            }
            if (Zone == 6) MaybeChanceCreated(a);
        }

        void ResolvePass(MatchPlayer a, MatchPlayer target, MatchPlayer d, MatchCommand cmd, MatchCommand def)
        {
            if (target == null) { ResolveKeep(a, d, def); return; }
            float pass = (a.stats.passing * 0.7f + a.stats.technique * 0.3f) * Perf(a) + TeamOf(Possession).chemistry.Teamwork * 20f
                         + FormationSystem.AttackBonus(TeamOf(Possession), cmd, Zone, Lane);
            if (cmd == MatchCommand.LongPass) pass -= 8f;
            if (cmd == MatchCommand.OneTwo) { pass += (target.stats.passing - 50) * 0.2f; Spend(target, MatchCommand.OneTwo); }

            if (d != null && def == MatchCommand.OffsideTrap)
            {
                if (ResolveOffsideTrap(a, target, d, pass)) return;
                Holder = cmd == MatchCommand.OneTwo ? a : target;
                Zone = Math.Min(6, Zone + 3);
                _lastPasser = cmd == MatchCommand.OneTwo ? null : a;
                MaybeChanceCreated(a);
                return;
            }

            bool intercepted = false;
            if (d != null && def == MatchCommand.PassCut)
            {
                float cut = (d.stats.defense * 0.5f + d.stats.speed * 0.3f + d.stats.mental * 0.2f) * Perf(d)
                            + FormationSystem.DefenseBonus(Opponent(Possession), def, Zone);
                intercepted = Roll(cut) > Roll(pass + 6f);
            }
            else if (cmd == MatchCommand.LongPass) intercepted = _rng.Chance(0.12f);

            if (intercepted)
            {
                var stealer = d ?? PickDefender(Opponent(Possession), Zone);
                Emit(MatchEventType.PassIntercepted, Possession, stealer, a, $"{stealer?.name}がパスカット！");
                if (stealer != null && stealer.isProtagonist) { ChangeTrust(4f, "パスカット"); stealer.rating += 0.2f; }
                Exp(stealer, 4);
                LoseBall(a, stealer);
                return;
            }

            switch (cmd)
            {
                case MatchCommand.OneTwo:
                    Zone = Math.Min(6, Zone + 2);
                    Emit(MatchEventType.OneTwo, Possession, a, target, $"{a.name}と{target.name}のワンツー！");
                    Holder = a;
                    _lastPasser = target;
                    Exp(a, 3); Exp(target, 3);
                    break;
                case MatchCommand.LongPass:
                    Zone = Math.Min(6, Zone + 2);
                    Emit(MatchEventType.LongPass, Possession, a, target, $"{a.name}のロングパス！ {target.name}が受ける");
                    Holder = target;
                    _lastPasser = a;
                    Exp(a, 3);
                    break;
                default:
                    Zone = Math.Min(6, Zone + 1);
                    Emit(MatchEventType.Pass, Possession, a, target, $"{a.name}から{target.name}へパス");
                    Holder = target;
                    _lastPasser = a;
                    Exp(a, 2);
                    break;
            }
            Lane = _rng.Range(0, 3);
            if (a.isProtagonist) { a.rating += 0.02f; _chainActions = Math.Max(_chainActions, 2); _chainRewarded = false; }
            if (Zone >= 5) MaybeChanceCreated(a);
        }

        /// <summary>センタリング。地上の守備者に読まれなければ、ゴール前で空中戦になる。</summary>
        void ResolveCentering(MatchPlayer a, MatchPlayer target, MatchPlayer d, MatchCommand def)
        {
            if (target == null) { ResolveKeep(a, d, def); return; }
            float cross = (a.stats.passing * 0.6f + a.stats.technique * 0.4f) * Perf(a)
                          + FormationSystem.AttackBonus(TeamOf(Possession), MatchCommand.Centering, Zone, Lane);
            if (d != null && def == MatchCommand.PassCut)
            {
                float cut = (d.stats.defense * 0.5f + d.stats.speed * 0.3f + d.stats.mental * 0.2f) * Perf(d);
                if (Roll(cut) > Roll(cross + 4f))
                {
                    Emit(MatchEventType.PassIntercepted, Possession, d, a, $"{d.name}がセンタリングをカット！");
                    Exp(d, 4);
                    LoseBall(a, d);
                    return;
                }
            }
            Emit(MatchEventType.Centering, Possession, a, target, $"{a.name}、{LaneName(Lane)}からセンタリング！", 0f, true, null, "上げるぞ！");
            Exp(a, 3);
            if (a.isProtagonist) { _chainActions = Math.Max(_chainActions, 2); _chainRewarded = false; }
            _lastPasser = a;

            // 空中戦の守備者：DF を優先
            var defTeam = Opponent(Possession);
            var cands = new List<MatchPlayer>();
            foreach (var p in defTeam.Active) if (p.CanTouchBall && p.position == Position.DF) cands.Add(p);
            if (cands.Count == 0) foreach (var p in defTeam.Active) if (p.CanTouchBall && p.position != Position.GK) cands.Add(p);
            _aerial = new AerialStage { crosser = a, receiver = target, defender = PickWeighted(cands), cross = cross };
            Zone = 6;
            Lane = 1;
        }

        void ResolveAerial(AerialStage s)
        {
            var r = s.receiver;
            var d = s.defender;
            Spend(r, s.atk.Value);
            if (d != null) Spend(d, s.def.Value);
            r.lastTouchMinute = MinuteInt;

            float atk;
            switch (s.atk.Value)
            {
                case MatchCommand.Header: atk = (r.stats.physical * 0.4f + r.stats.shooting * 0.3f + r.stats.speed * 0.3f) * Perf(r); break;
                case MatchCommand.Volley: atk = (r.stats.shooting * 0.5f + r.stats.technique * 0.5f) * Perf(r) - 5f; break;
                default: atk = (r.stats.technique * 0.6f + r.stats.physical * 0.4f) * Perf(r) + 5f; break;
            }
            atk += (s.cross - 50f) * 0.2f + FormationSystem.AttackBonus(TeamOf(Possession), s.atk.Value, 6, 1);

            bool won = true;
            if (d != null)
            {
                float dv;
                switch (s.def.Value)
                {
                    case MatchCommand.Clear: dv = (d.stats.defense * 0.5f + d.stats.physical * 0.5f) * Perf(d); break;
                    case MatchCommand.Compete: dv = (d.stats.physical * 0.7f + d.stats.speed * 0.3f) * Perf(d) + 3f; break;
                    default: dv = (d.stats.defense * 0.4f + d.stats.speed * 0.4f + d.stats.mental * 0.2f) * Perf(d) - 4f; break;
                }
                dv += FormationSystem.DefenseBonus(Opponent(Possession), s.def.Value, 6);
                won = Roll(atk) > Roll(dv);
            }

            if (!won)
            {
                string how = s.def.Value == MatchCommand.Clear ? "大きくクリア！" : s.def.Value == MatchCommand.Compete ? "競り勝った！" : "ボールをカット！";
                Emit(MatchEventType.Clear, Opponent(Possession).side, d, r, $"{d.name}、{how}", 0f, true, null, null);
                if (d.isProtagonist) { ChangeTrust(4f, "空中戦"); d.rating += 0.2f; }
                Exp(d, 5);
                if (s.def.Value == MatchCommand.Compete && _rng.Chance(0.4f))
                {
                    Emit(MatchEventType.Info, Possession, r, null, "こぼれ球を拾った！");
                    Holder = r;
                    Zone = 5;
                    return;
                }
                Turnover(Opponent(Possession).side, d, s.def.Value == MatchCommand.Clear ? 2 : 1);
                _chainActions = 0;
                return;
            }

            Exp(r, 5);
            if (r.isProtagonist) MaybeChanceCreated(r);
            switch (s.atk.Value)
            {
                case MatchCommand.Header:
                    Emit(MatchEventType.Header, Possession, r, d, $"{r.name}のヘディングシュート！", 0f, true, null, "うおおっ！");
                    StartShot(r, (r.stats.shooting * 0.5f + r.stats.physical * 0.5f) * Perf(r) + 4f);
                    break;
                case MatchCommand.Volley:
                    Emit(MatchEventType.Volley, Possession, r, d, $"{r.name}、ダイレクトボレー！", 0f, true, null, "合わせる！");
                    StartShot(r, (r.stats.shooting * 0.7f + r.stats.technique * 0.3f) * Perf(r) + 10f);
                    break;
                default:
                    Emit(MatchEventType.Keep, Possession, r, d, $"{r.name}、胸でトラップ！ ゴール前で持った！");
                    Holder = r;
                    Zone = 6;
                    break;
            }
        }

        /// <summary>オフサイドトラップ。成功したら true。</summary>
        bool ResolveOffsideTrap(MatchPlayer a, MatchPlayer target, MatchPlayer d, float pass)
        {
            var defTeam = Opponent(Possession);
            float line = 0f;
            int n = 0;
            foreach (var p in defTeam.Active)
            {
                if (p.position != Position.DF) continue;
                line += (p.stats.defense * 0.4f + p.stats.mental * 0.6f) * Perf(p);
                n++;
            }
            if (n == 0) line = 30f; else line /= n;
            line += (defTeam.offsideTrapPlanned ? 8f : 0f) + defTeam.chemistry.Teamwork * 20f;
            foreach (var p in defTeam.Active) if (p.position == Position.DF && p != d) Spend(p, MatchCommand.OffsideTrap);

            float run = pass * 0.6f + target.stats.speed * 0.4f;
            if (Roll(line) > Roll(run))
            {
                OffsideTrapsSucceeded += defTeam == ProtagonistTeam ? 1 : 0;
                Emit(MatchEventType.OffsideTrapSuccess, defTeam.side, d, target, $"オフサイドトラップ成功！ {target.name}はオフサイド！", 0f, true, null, "今だ、上げろ！");
                if (d.isProtagonist) { ChangeTrust(4f, "オフサイドトラップ"); d.rating += 0.15f; }
                Exp(d, 4);
                Turnover(defTeam.side, d, 1);
                return true;
            }
            Emit(MatchEventType.OffsideTrapFail, defTeam.side, d, target, $"オフサイドトラップ失敗！ {target.name}が裏へ抜けた！");
            if (d.isProtagonist) { ChangeTrust(-6f, "トラップ失敗"); d.rating -= 0.2f; }
            return false;
        }

        void ResolveKeep(MatchPlayer a, MatchPlayer d, MatchCommand def)
        {
            if (a.isProtagonist && Zone >= 3) Passive(a, "キープ");
            if (d != null && (def == MatchCommand.Tackle || def == MatchCommand.SpecialTackle))
            {
                if (CheckFoul(d, a)) return;
                float keep = (a.stats.technique * 0.5f + a.stats.physical * 0.5f) * Perf(a) + 8f;
                float tk = (d.stats.defense * 0.55f + d.stats.physical * 0.45f) * Perf(d);
                if (Roll(tk) > Roll(keep))
                {
                    Emit(MatchEventType.DribbleBeaten, Possession, d, a, $"{d.name}がボールを奪った！");
                    Exp(d, 5);
                    LoseBall(a, d);
                    return;
                }
            }
            Emit(MatchEventType.Keep, Possession, a, null, $"{a.name}、ボールをキープ");
        }

        void ResolveShot(MatchPlayer a, MatchPlayer d, MatchCommand def, MatchCommand cmd, float bonus,
                         SpecialMove special = null, SpecialMove defSpecial = null)
        {
            var defTeam = Opponent(Possession);
            a.shots++;
            if (ProtagonistTeam != null && Possession == Setup.protagonistSide) MaybeChanceCreated(a);

            if (cmd == MatchCommand.MissOnPurpose)
            {
                AddSuspicion(Zone >= 5 ? 22f : 12f, "ありえないシュートミス");
                Emit(MatchEventType.Missed, Possession, a, null, $"{a.name}のシュートは……大きく外れた！");
                Turnover(defTeam.side, defTeam.Keeper, 0);
                return;
            }

            float dist = Zone >= 6 ? 1f : Zone == 5 ? 0.9f : Zone == 4 ? 0.75f : Zone == 3 ? 0.55f : 0.35f;
            float shot = (a.stats.shooting * 0.7f + a.stats.technique * 0.3f) * Perf(a) * dist + (special?.power ?? 0) + bonus
                         + FormationSystem.AttackBonus(TeamOf(Possession), cmd, Zone, Lane);
            if (special != null)
            {
                Emit(MatchEventType.SpecialShot, Possession, a, null, $"{a.name}「{special.shout}」　必殺の{special.name}！", 0f, true, special.name, special.shout);
                if (Setup.crowd >= 100) a.beautifulSkills++;
            }
            else Emit(MatchEventType.Shot, Possession, a, null, $"{a.name}、シュート！", 0f, true, null, null);
            Exp(a, special != null ? 6 : 3);

            bool blocking = def == MatchCommand.Block || def == MatchCommand.SpecialBlock;
            if (d != null && blocking && d.position != Position.GK)
            {
                float blk = (d.stats.defense * 0.6f + d.stats.physical * 0.4f) * Perf(d) + (defSpecial?.power ?? 0)
                            + FormationSystem.DefenseBonus(defTeam, def, Zone);
                if (Roll(blk) > Roll(shot + 10f))
                {
                    Emit(MatchEventType.Blocked, defTeam.side, d, a, $"{d.name}が体を張ってブロック！", 0f, true, defSpecial?.name, null);
                    if (d.isProtagonist) { ChangeTrust(5f, "シュートブロック"); d.rating += 0.25f; }
                    Exp(d, 5);
                    Turnover(defTeam.side, d, 1);
                    return;
                }
                shot *= 0.85f;
            }

            float wide = 0.12f + (Zone < 4 ? 0.2f : 0f) - (special != null ? 0.08f : 0f);
            if (_rng.Chance(wide))
            {
                Emit(MatchEventType.Missed, Possession, a, null, "シュートはゴールの外へ！");
                Turnover(defTeam.side, defTeam.Keeper, 0);
                return;
            }
            StartShot(a, shot);
        }

        /// <summary>シュートがキーパーまで届いた。キーパーの判断（キャッチ・パンチング・必殺セーブ）へ。</summary>
        void StartShot(MatchPlayer shooter, float power)
        {
            var k = Opponent(Possession).Keeper;
            if (k == null || !k.CanTouchBall) { Goal(shooter); return; }
            _shot = new ShotStage { shooter = shooter, keeper = k, power = power };
        }

        void ResolveKeeper(ShotStage s)
        {
            var k = s.keeper;
            var defTeam = Opponent(Possession);
            var cmd = s.keeperCmd.Value;
            var special = UseSpecial(k, cmd);
            if (cmd == MatchCommand.SpecialSave && special == null) cmd = MatchCommand.Catch;
            Spend(k, cmd, special);

            float save = (k.stats.goalkeeping * 0.8f + k.stats.mental * 0.2f) * Perf(k);
            if (cmd == MatchCommand.Punch) save += 7f;
            if (HasTrait(defTeam, BossTrait.IronWall)) save += 10f;
            if (special != null)
            {
                save += special.power;
                Emit(MatchEventType.Info, defTeam.side, k, s.shooter, $"{k.name}「{special.shout}」　{special.name}！", 0f, true, special.name, special.shout);
            }

            if (Roll(save) > Roll(s.power + shotOffset))
            {
                if (k.isProtagonist) k.rating += 0.3f;
                Exp(k, 6);
                if (s.shooter.isProtagonist) s.shooter.rating += 0.05f;
                if (cmd == MatchCommand.Punch)
                {
                    Emit(MatchEventType.Punch, defTeam.side, k, s.shooter, $"{k.name}、パンチングで弾いた！", 0f, true, null, "させるか！");
                    if (_rng.Chance(0.45f))
                    {
                        var rebound = PickCarrier(TeamOf(Possession), 5);
                        Emit(MatchEventType.Info, Possession, rebound, null, $"こぼれ球！ {rebound?.name}が拾った！");
                        Holder = rebound;
                        Zone = 5;
                        return;
                    }
                    Turnover(defTeam.side, PickCarrier(defTeam, 1), 1);
                    return;
                }
                Emit(MatchEventType.Catch, defTeam.side, k, s.shooter, $"{k.name}、がっちりキャッチ！", 0f, true, special?.name, null);
                Turnover(defTeam.side, k, 0);
                return;
            }
            Goal(s.shooter);
        }

        void Goal(MatchPlayer scorer)
        {
            var team = TeamOf(Possession);
            var opp = Opponent(Possession);
            team.score++;
            scorer.goals++;
            scorer.rating += 1.0f;
            var assist = _lastPasser != null && _lastPasser != scorer ? _lastPasser : null;
            if (assist != null) { assist.assists++; assist.rating += 0.6f; }
            Emit(MatchEventType.Goal, Possession, scorer, assist, $"ゴーーール！！ {scorer.name}！　{Home.score} - {Away.score}", 0f, true, null, "よっしゃあああ！");
            Exp(scorer, 20);
            Exp(assist, 12);

            foreach (var p in team.Active) p.tension = Math.Min(100f, p.tension + 8f);
            foreach (var p in opp.Active) p.tension = Math.Max(0f, p.tension - 6f);

            if (scorer.isProtagonist) Celebrate(scorer, opp);

            if (team == ProtagonistTeam)
            {
                if (scorer.isProtagonist) ChangeTrust(15f, "ゴール");
                else if (assist != null && assist.isProtagonist) ChangeTrust(12f, "アシスト");
            }
            _chainActions = 0;
            Kickoff(opp.side);
        }

        /// <summary>ゴールパフォーマンス。ユニフォームを脱ぐとイエロー、挑発すると相手が燃える。</summary>
        void Celebrate(MatchPlayer scorer, MatchTeam opp)
        {
            var design = Setup.celebration;
            if (design == null || !design.IsValid || scorer.sentOff) return;
            CelebrationsPerformed++;
            var moves = new List<string>();
            foreach (var m in design.moves) moves.Add(MoveName(m));
            Emit(MatchEventType.Info, SideOf(scorer), scorer, null,
                 $"{scorer.name}のゴールパフォーマンス「{design.name}」！（{string.Join("→", moves)}）", 0f, true, design.name, null);
            if (Setup.crowd >= 100) scorer.beautifulSkills++;
            if (design.IsProvocative)
            {
                foreach (var p in opp.Active) p.tension = Math.Min(100f, p.tension + 8f);
                Emit(MatchEventType.Info, opp.side, null, null, $"{opp.name}の選手たちが激怒している！");
            }
            if (design.RisksYellowCard)
            {
                scorer.yellowCards++;
                Emit(MatchEventType.YellowCard, SideOf(scorer), scorer, null, $"{scorer.name}、ユニフォームを脱いでイエローカード！");
                if (scorer.yellowCards >= 2) SendOff(scorer, "パフォーマンスで2枚目のイエロー、退場……！");
            }
        }

        static string MoveName(SoccerIsland.Career.PerformanceMove m)
        {
            switch (m)
            {
                case SoccerIsland.Career.PerformanceMove.Run: return "走り回る";
                case SoccerIsland.Career.PerformanceMove.KneeSlide: return "ひざスライディング";
                case SoccerIsland.Career.PerformanceMove.Backflip: return "バク宙";
                case SoccerIsland.Career.PerformanceMove.Cartwheel: return "側転";
                case SoccerIsland.Career.PerformanceMove.Dance: return "ダンス";
                case SoccerIsland.Career.PerformanceMove.PointToSky: return "天を指さす";
                case SoccerIsland.Career.PerformanceMove.HeartHands: return "ハート";
                case SoccerIsland.Career.PerformanceMove.ShushCrowd: return "観客を黙らせる";
                case SoccerIsland.Career.PerformanceMove.CornerFlagKick: return "コーナーフラッグを蹴る";
                case SoccerIsland.Career.PerformanceMove.Salute: return "敬礼";
                case SoccerIsland.Career.PerformanceMove.Robot: return "ロボットダンス";
                case SoccerIsland.Career.PerformanceMove.Bow: return "お辞儀";
                case SoccerIsland.Career.PerformanceMove.TeammateHug: return "仲間と抱き合う";
                case SoccerIsland.Career.PerformanceMove.ShirtOverHead: return "ユニフォームを脱ぐ";
                default: return "ファンに手を振る";
            }
        }

        public static string PerformanceMoveName(SoccerIsland.Career.PerformanceMove m) => MoveName(m);

        void LoseBall(MatchPlayer a, MatchPlayer stealer)
        {
            a.ballsLost++;
            if (a.isProtagonist) { a.rating -= 0.2f; ChangeTrust(-6f, "ボールロスト"); }
            _chainActions = 0;
            Turnover(Opponent(Possession).side, stealer, 6 - Zone);
        }

        /// <summary>主人公のドリブル・パスから数手以内にシュートまで行けば「チャンスを作った」。</summary>
        void MaybeChanceCreated(MatchPlayer actor)
        {
            if (ProtagonistTeam == null || Possession != Setup.protagonistSide) return;
            if (_chainActions <= 0 || _chainRewarded) return;
            _chainRewarded = true;
            var pro = Protagonist;
            if (pro != null) pro.rating += 0.1f;
            Emit(MatchEventType.ChanceCreated, Possession, pro, actor, "主人公のプレーから決定機！");
            ChangeTrust(12f, "チャンスを作った");
        }

        bool CheckFoul(MatchPlayer d, MatchPlayer victim)
        {
            var team = TeamOf(SideOf(d));
            float c = 0.06f + team.chemistry.cardRisk;
            if (d.soccerPersonality == SoccerPersonality.Fighter) c += 0.07f;
            if (d.soccerPersonality == SoccerPersonality.Hysteric) c += 0.08f;
            if (d.tension > 85f) c += 0.05f;
            if (d.role == PlayerRole.Physical || team.instruction == TeamInstruction.PlayPhysical) c += 0.05f;
            var other = Opponent(team.side);
            if (HasTrait(team, BossTrait.BribedReferee) && !HasTrait(other, BossTrait.Warded)) c *= 0.4f;  // 自分たちのファウルは見逃される
            if (HasTrait(other, BossTrait.BribedReferee) && !HasTrait(team, BossTrait.Warded)) c *= 1.6f;  // 相手のファウルは厳しく取られる
            if (!_rng.Chance(c)) return false;

            Emit(MatchEventType.Foul, team.side, d, victim, $"{d.name}のファウル！");
            if (d.soccerPersonality == SoccerPersonality.Hysteric) d.tension = Math.Min(100f, d.tension + 15f);
            float yellow = d.soccerPersonality == SoccerPersonality.Hysteric ? 0.5f : 0.35f;
            float red = d.soccerPersonality == SoccerPersonality.Fighter ? 0.06f : 0.04f;
            if (_rng.Chance(red)) SendOff(d, "一発レッド！");
            else if (_rng.Chance(yellow))
            {
                d.yellowCards++;
                Emit(MatchEventType.YellowCard, team.side, d, null, $"{d.name}にイエローカード");
                if (d.yellowCards >= 2) SendOff(d, "2枚目のイエローで退場！");
            }
            if (d.isProtagonist) { d.rating -= 0.3f; ChangeTrust(-3f, "ファウル"); }

            // フリーキック
            if (Zone >= 4)
            {
                Emit(MatchEventType.FreeKick, Possession, victim, null, $"{ZoneName(Zone)}でフリーキック！");
                ResolveShot(victim, null, MatchCommand.Wait, MatchCommand.Shoot, 6f);
            }
            return true;
        }

        void SendOff(MatchPlayer p, string text)
        {
            p.sentOff = true;
            p.rating -= 1.5f;
            Emit(MatchEventType.RedCard, SideOf(p), p, null, $"{p.name}、{text}");
            var t = TeamOf(SideOf(p));
            t.chemistry = TeamChemistry.Evaluate(t.players);
        }

        static string SpecialName(MatchPlayer p)
        {
            switch (p.soccerPersonality)
            {
                case SoccerPersonality.God: return "天啓";
                case SoccerPersonality.Gifted: return "未来視ショット";
                case SoccerPersonality.Sense: return "幻惑ループ";
                case SoccerPersonality.Fighter: return "剛腕キャノン";
                case SoccerPersonality.Egoist: return "独裁者の一撃";
                default: return p.isProtagonist ? "移住者シュート" : "島流しシュート";
            }
        }

        // ==================================================================
        // 主人公の評価・八百長
        // ==================================================================

        void Passive(MatchPlayer p, string what)
        {
            p.rating -= 0.3f;
            Emit(MatchEventType.Passive, SideOf(p), p, null, $"{p.name}の消極的なプレー（{what}）にチームメイトがため息");
            ChangeTrust(-8f, "消極的");
        }

        public void ChangeTrust(float delta, string reason)
        {
            if (ProtagonistTeam == null || delta == 0f) return;
            // 雰囲気が良いチームは評価が上がりやすく、下がりにくい
            float mood = ProtagonistTeam.mood / 200f;
            delta *= delta > 0 ? 1f + mood : 1f - mood;
            float before = Trust;
            Trust = Math.Max(0f, Math.Min(100f, Trust + delta));
            if (Math.Abs(Trust - before) >= 0.5f)
                Emit(MatchEventType.TrustChanged, Setup.protagonistSide.Value, Protagonist, null,
                     $"チームメイトの評価 {(Trust > before ? "↑" : "↓")}（{reason}）", Trust - before);
        }

        void AddSuspicion(float amount, string reason)
        {
            float crowd = 1f + Math.Min(1.5f, Setup.crowd / 2000f);
            Suspicion = Math.Min(100f, Suspicion + amount * crowd);
            ChangeTrust(-5f, reason);
            Emit(MatchEventType.Suspicion, Setup.protagonistSide ?? Side.Home, Protagonist, null, $"観客がざわついている……（{reason}）", Suspicion);
        }

        // ==================================================================
        // コーチング
        // ==================================================================

        public bool CanCoach
        {
            get
            {
                var pro = Protagonist;
                if (pro == null || Finished) return false;
                int cd = pro.cramped ? 2 : coachingCooldown;
                return MinuteInt - _lastCoachMinute >= cd;
            }
        }

        /// <summary>主人公が指示を出す。target が null ならチーム全体。</summary>
        public CoachingResult Coach(CoachingCommand cmd, MatchPlayer target = null)
        {
            var team = ProtagonistTeam;
            if (team == null || !CanCoach) return new CoachingResult { notAllowed = true, line = "今は指示を出せない" };
            _lastCoachMinute = MinuteInt;

            CoachingResult r;
            if (target != null)
            {
                r = Coaching.CoachPlayer(target, cmd, Trust, _rng);
                if (r.accepted)
                {
                    switch (cmd)
                    {
                        case CoachingCommand.GiveBallToMe: team.focusPlayer = target; team.focusUntilMinute = MinuteInt + 10; break;
                        case CoachingCommand.PlayPhysical: target.role = PlayerRole.Physical; break;
                        case CoachingCommand.SupportRole: target.role = PlayerRole.Support; break;
                        case CoachingCommand.FreeRole: target.role = PlayerRole.Free; break;
                    }
                }
                if (r.metDemand) ChangeTrust(3f, "的確な指示");
                Emit(r.accepted ? MatchEventType.CoachingAccepted : MatchEventType.CoachingRejected, team.side, target, null, $"{target.name}「{r.line}」");
                return r;
            }

            r = new CoachingResult { accepted = true };
            var ti = Coaching.ToTeamInstruction(cmd);
            if (ti != TeamInstruction.None)
            {
                team.instruction = ti;
                team.instructionUntilMinute = MinuteInt + 15;
            }
            float sum = 0f;
            int n = 0;
            foreach (var p in team.Active)
            {
                if (p.isProtagonist) continue;
                float d = TeamReaction(p, cmd);
                p.tension = Math.Max(0f, Math.Min(100f, p.tension + d));
                if (p.demand.HasValue && p.demand.Value == cmd) { p.demand = null; d += 10f; p.tension = Math.Min(100f, p.tension + 10f); }
                sum += d;
                n++;
            }
            r.tensionDelta = n == 0 ? 0f : sum / n;
            r.line = TeamLine(cmd);
            Emit(MatchEventType.CoachingAccepted, team.side, Protagonist, null, $"主人公「{r.line}」");
            return r;
        }

        static float TeamReaction(MatchPlayer p, CoachingCommand cmd)
        {
            var sp = p.soccerPersonality;
            switch (cmd)
            {
                case CoachingCommand.Praise: return sp == SoccerPersonality.MoodMaker ? 10f : 5f;
                case CoachingCommand.Scold:
                    return sp == SoccerPersonality.Hysteric ? 20f : sp == SoccerPersonality.Fighter ? 5f :
                           p.personality == Personality.Introvert || p.personality == Personality.Gloomy ? -10f : -3f;
                case CoachingCommand.CalmDown: return p.tension > 75f ? -15f : 0f;
                case CoachingCommand.PassAround:
                    return sp == SoccerPersonality.Sense || sp == SoccerPersonality.Gifted || sp == SoccerPersonality.Egoist ? -10f : sp == SoccerPersonality.Devoted ? 5f : 0f;
                case CoachingCommand.DefendDeep: return sp == SoccerPersonality.Sense || sp == SoccerPersonality.Egoist ? -10f : sp == SoccerPersonality.Devoted ? 5f : 0f;
                case CoachingCommand.TakeOn:
                case CoachingCommand.GoForward: return sp == SoccerPersonality.Egoist ? 8f : sp == SoccerPersonality.Fighter ? 5f : 2f;
                case CoachingCommand.PlayPhysical: return sp == SoccerPersonality.Fighter ? 10f : sp == SoccerPersonality.Sense ? -5f : 0f;
                case CoachingCommand.RaiseLine: return p.position == Position.DF ? 3f : 0f;
                default: return 0f;
            }
        }

        static string TeamLine(CoachingCommand cmd)
        {
            switch (cmd)
            {
                case CoachingCommand.Praise: return "いいぞみんな！ その調子だ！";
                case CoachingCommand.Scold: return "何やってんだ！ 気合入れろ！";
                case CoachingCommand.CalmDown: return "落ち着け！ まだ時間はある！";
                case CoachingCommand.GoForward: return "攻めろ！ 前へ出ろ！";
                case CoachingCommand.DefendDeep: return "守れ！ 1点を守り切るぞ！";
                case CoachingCommand.PassAround: return "パスを回せ！ 焦るな！";
                case CoachingCommand.TakeOn: return "どんどん仕掛けろ！";
                case CoachingCommand.PlayPhysical: return "体を張れ！ 当たり負けするな！";
                case CoachingCommand.RaiseLine: return "ラインを上げろ！ オフサイドを取るぞ！";
                case CoachingCommand.GiveBallToMe: return "俺にボールを集めろ！";
                default: return "頼むぞ！";
            }
        }

        // ==================================================================
        // 足がつる・イベント用
        // ==================================================================

        public static bool HasTrait(MatchTeam t, BossTrait trait) => t != null && t.traits.Contains(trait);

        void ApplyBossTraits()
        {
            foreach (var t in new[] { Home, Away })
            {
                var opp = Opponent(t.side);
                if (HasTrait(t, BossTrait.Intimidation) && HasTrait(opp, BossTrait.Warded))
                    Emit(MatchEventType.Info, opp.side, null, null, $"{opp.name}は魔除けのおかげで、{t.name}の威圧が効かない！", 0f, true, null, null);
                else if (HasTrait(t, BossTrait.Intimidation))
                {
                    foreach (var p in opp.Active)
                        p.tension = Math.Max(0f, p.tension + (p.soccerPersonality == SoccerPersonality.Fighter || p.soccerPersonality == SoccerPersonality.Hysteric ? 5f : -15f));
                    Emit(MatchEventType.Info, t.side, null, null, $"{t.name}の威圧感に、{opp.name}の選手たちが萎縮している……", 0f, true, null, null);
                }
                if (HasTrait(t, BossTrait.Unbeatable) && !Setup.unbeatableBroken)
                {
                    foreach (var p in t.players)
                    {
                        for (int i = 0; i < NPCAbilities.StatCount; i++) p.stats[i] = NPCAbilities.Clamp(p.stats[i] + 15);
                        p.maxGuts = p.guts = p.maxGuts * 3 / 2;
                    }
                    Emit(MatchEventType.Info, t.side, null, null, $"{t.name}……まるで歯が立たない気配がする。何か弱点はないのか？", 0f, true, null, null);
                }
            }
        }

        /// <summary>地域対抗：ライバルの街との試合は燃える。</summary>
        void ApplyTownRivalry()
        {
            if (Home.town < 0 || Away.town < 0) return;
            var th = (Town)Home.town;
            var ta = (Town)Away.town;
            foreach (var p in Home.players) p.tension = Math.Min(100f, p.tension + TownRivalry.DerbyTension(th, ta));
            foreach (var p in Away.players) p.tension = Math.Min(100f, p.tension + TownRivalry.DerbyTension(ta, th));
            string flavor = TownRivalry.Flavor(th, ta);
            if (flavor.Length > 0)
                Emit(MatchEventType.Info, Side.Home, null, null, $"{flavor}！ {NPCLabels.Of(th)}と{NPCLabels.Of(ta)}、スタンドは一触即発の雰囲気だ！", 0f, true, null, null);
        }

        /// <summary>試合中に作戦を変える（コーチングのクールダウンを使う）。</summary>
        public List<MeetingReaction> ChangeTactic(TeamTactic tactic)
        {
            var team = ProtagonistTeam;
            if (team == null || !CanCoach) return null;
            _lastCoachMinute = MinuteInt;
            var r = FormationSystem.SetTactic(team, tactic);
            Emit(MatchEventType.CoachingAccepted, team.side, Protagonist, null, $"作戦変更：{FormationSystem.Name(tactic)}");
            return r;
        }

        /// <summary>天才の読み：相手のコマンドに対する一番の対抗手段。</summary>
        static MatchCommand CounterOf(MatchCommand atk)
        {
            switch (atk)
            {
                case MatchCommand.Pass:
                case MatchCommand.LongPass:
                case MatchCommand.OneTwo:
                case MatchCommand.Centering:
                case MatchCommand.BackPass: return MatchCommand.PassCut;
                case MatchCommand.Shoot:
                case MatchCommand.SpecialShot: return MatchCommand.Block;
                default: return MatchCommand.Tackle;
            }
        }

        public void CrampProtagonist()
        {
            var p = Protagonist;
            if (p == null || p.cramped) return;
            p.cramped = true;
            if (Holder == p) Holder = PickCarrier(TeamOf(Possession), Zone);
            Emit(MatchEventType.Cramp, SideOf(p), p, null, $"{p.name}の足がつった！ もう走れない……指示で勝たせるしかない！");
        }

        // ==================================================================
        // 結果
        // ==================================================================

        public MatchResult Result()
        {
            var r = new MatchResult
            {
                homeScore = Home.score, awayScore = Away.score,
                protagonistSide = Setup.protagonistSide, protagonist = Protagonist,
                trust = Trust, suspicion = Suspicion, fixingExposed = Suspicion >= 100f,
                celebrations = CelebrationsPerformed,
                log = Log,
            };
            foreach (var t in new[] { Home, Away })
                foreach (var p in t.players) p.rating = Math.Max(3f, Math.Min(10f, p.rating));
            if (Protagonist != null)
            {
                // チームメイトの評価も採点に反映
                Protagonist.rating = Math.Max(3f, Math.Min(10f, Protagonist.rating + (Trust - 50f) / 100f));
            }
            foreach (var m in Setup.missions) r.missions.Add(m.Evaluate(this, r));
            if (Protagonist != null)
            {
                int bonus = r.ProtagonistWon ? 30 : r.ProtagonistLost ? 5 : 15;
                if (Setup.category == MatchCategory.IslandSelect || Setup.category == MatchCategory.RegionalDerby) bonus *= 2;
                r.protagonistExp = Protagonist.exp + bonus;
            }
            return r;
        }

        void Emit(MatchEventType type, Side side, MatchPlayer actor, MatchPlayer other, string text, float value = 0f,
                  bool cutIn = false, string moveName = null, string shout = null)
        {
            var e = new MatchEvent { type = type, minute = MinuteInt, side = side, actor = actor, other = other, text = text, value = value,
                                     cutIn = cutIn, moveName = moveName, shout = shout };
            Log.Add(e);
            OnEvent?.Invoke(e);
            foreach (var m in Setup.missions) m.OnEvent(this, e);
        }
    }
}
