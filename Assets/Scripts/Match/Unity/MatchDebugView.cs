using System;
using System.Collections.Generic;
using SoccerIsland.Career.UnityIntegration;
using SoccerIsland.NPC;
using SoccerIsland.World;
using UnityEngine;

namespace SoccerIsland.Match.UnityIntegration
{
    /// <summary>
    /// 試合を画面上で遊ぶための仮UI（OnGUI）。本番の2D演出ができるまでの確認用。
    /// NPCManager・WorldManager・CareerManager と同じシーンに置き、Play 中に「試合を始める」を押す。
    /// </summary>
    public class MatchDebugView : MonoBehaviour
    {
        public enum MissionKind { None, Win, Cramp, FanShowcase, OffsideTrap, ThrowMatch }

        public MissionKind mission = MissionKind.None;
        public Position protagonistPosition = Position.MF;
        [Tooltip("ON：自チーム全員にコマンド（キャプテン翼式） / OFF：主人公だけ")]
        public bool fullTeamControl = true;

        MatchSession _session;
        MatchPrompt _prompt;
        Vector2 _logScroll;
        MatchPlayer _coachTarget;
        string _message = "";

        void OnGUI()
        {
            var career = CareerManager.Instance != null ? CareerManager.Instance.Career : null;
            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));
            if (career == null) { GUILayout.Label("CareerManager がありません"); GUILayout.EndArea(); return; }

            if (_session == null) DrawStart(career);
            else if (_session.Current == MatchSession.Phase.Meeting) DrawMeeting();
            else DrawMatch();

            GUILayout.EndArea();
        }

        void DrawStart(SoccerIsland.Career.IslandCareer career)
        {
            GUILayout.Label("試合（デバッグ）", Big());
            mission = (MissionKind)GUILayout.SelectionGrid((int)mission, new[] { "なし", "勝利", "足がつる", "足技", "オフサイド5回", "八百長" }, 6);
            if (GUILayout.Button("今日の試合のどれかに助っ人で出る", GUILayout.Height(40)))
            {
                career.Player.position = protagonistPosition;
                SoccerIsland.World.Match target = null;
                foreach (var m in career.World.TodaysMatches()) if (!m.played && m.awayLineup.Count > 0) { target = m; break; }
                if (target == null) foreach (var m in career.World.State.matches) if (!m.played && m.awayLineup.Count > 0) { target = m; break; }
                if (target == null) { _message = "試合がありません"; return; }
                var setup = MatchFactory.FromWorldMatch(career, target, Side.Home);
                var mm = CreateMission();
                if (mm != null) setup.missions.Add(mm);
                _session = new MatchSession(career, setup);
            }
            GUILayout.Label(_message);
        }

        IMatchMission CreateMission()
        {
            switch (mission)
            {
                case MissionKind.Win: return new WinMission();
                case MissionKind.Cramp: return new CrampMission(20);
                case MissionKind.FanShowcase: return new FanShowcaseMission();
                case MissionKind.OffsideTrap: return new OffsideTrapMission();
                case MissionKind.ThrowMatch: return new ThrowMatchMission();
                default: return null;
            }
        }

        void DrawMeeting()
        {
            GUILayout.Label($"ミーティング：{_session.Setup.home.name} vs {_session.Setup.away.name}", Big());
            foreach (var m in _session.Setup.missions) GUILayout.Label("【イベント】" + m.Title);
            var chem = _session.Diagnose();
            GUILayout.Label($"チームの雰囲気 {chem.score:0}");
            foreach (var r in chem.reasons) GUILayout.Label("  " + r);
            GUILayout.Space(8);
            foreach (MeetingTopic t in Enum.GetValues(typeof(MeetingTopic)))
            {
                if (t == MeetingTopic.TrustAce || t == MeetingTopic.BonusPromise) continue;
                if (GUILayout.Button(TopicName(t))) ShowMeeting(_session.HoldMeeting(t));
            }
            if (GUILayout.Button("キックオフ！", GUILayout.Height(40)))
            {
                _session.Kickoff().Mode = fullTeamControl ? ControlMode.FullTeam : ControlMode.ProtagonistOnly;
                _prompt = _session.Advance();
            }
            GUILayout.Label(_message);
        }

        void ShowMeeting(MeetingResult r)
        {
            if (r == null) return;
            var lines = new List<string>();
            foreach (var x in r.reactions) lines.Add($"{x.player.name}「{x.line}」");
            foreach (var a in r.arguments) lines.Add($"口論！ {a.a.name}「{a.lineA}」 {a.b.name}「{a.lineB}」{(a.resolvedByMoodMaker ? "→ ムードメーカーが収めた" : "")}");
            _message = string.Join("\n", lines);
        }

        void DrawMatch()
        {
            var e = _session.Engine;
            GUILayout.Label($"{e.Home.name} {e.Home.score} - {e.Away.score} {e.Away.name}　{e.MinuteInt}分　評価 {e.Trust:0}" +
                            (e.FixingActive ? $"　疑い {e.Suspicion:0}" : ""), Big());

            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(Screen.height * 0.45f));
            var log = _session.Log;
            for (int i = Math.Max(0, log.Count - 60); i < log.Count; i++)
            {
                var ev = log[i];
                // カットイン（本番では顔アップ＋技名の2D演出に置き換える）
                if (ev.cutIn) GUILayout.Label($"{ev.minute,2}' ★ {ev.text}", Big());
                else GUILayout.Label($"{ev.minute,2}' {ev.text}");
            }
            GUILayout.EndScrollView();
            _logScroll.y = float.MaxValue;

            if (_session.Current == MatchSession.Phase.Finished)
            {
                var r = _session.Result;
                GUILayout.Label($"試合終了　評価 {r.protagonist?.rating:0.0}　チームメイトの評価 {r.trust:0}　経験値 +{r.protagonistExp}");
                foreach (var lv in r.levelUps) GUILayout.Label($"レベルアップ！ Lv.{lv}");
                foreach (var m in r.learnedSpecials) GUILayout.Label($"必殺技「{m.name}」を覚えた！");
                foreach (var m in r.missions) GUILayout.Label($"【{m.title}】{(m.status == MissionStatus.Success ? "成功" : "失敗")}　{m.detail}");
                if (GUILayout.Button("閉じる")) _session = null;
                return;
            }

            if (_prompt != null)
            {
                GUILayout.Label(_prompt.situation);
                GUILayout.Label($"{_prompt.actor.name}　ガッツ {_prompt.guts}/{_prompt.maxGuts}　{MiniMap(_prompt.zone, _prompt.lane)}");
                GUILayout.BeginHorizontal();
                foreach (var c in _prompt.options)
                    if (GUILayout.Button(CommandName(c), GUILayout.Height(36)))
                    {
                        _session.Choose(c, null);
                        _prompt = _session.Advance();
                        break;
                    }
                GUILayout.EndHorizontal();
            }
            else if (GUILayout.Button("進める", GUILayout.Height(36))) _prompt = _session.Advance();

            DrawCoaching(e);
            if (GUILayout.Button("おまかせで最後まで")) { _session.AutoPlay(); _prompt = null; }
        }

        void DrawCoaching(MatchEngine e)
        {
            var team = _session.MyTeam;
            if (team == null) return;
            GUILayout.Label(e.CanCoach ? "コーチング（相手を選んでから指示）" : "コーチング（クールダウン中）");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_coachTarget == null, "全員")) _coachTarget = null;
            foreach (var p in team.Active)
            {
                if (p.isProtagonist) continue;
                string label = p.name + (p.demand.HasValue ? "（！）" : "");
                if (GUILayout.Toggle(_coachTarget == p, label)) _coachTarget = p;
            }
            GUILayout.EndHorizontal();
            if (_coachTarget != null && _coachTarget.demand.HasValue) GUILayout.Label($"{_coachTarget.name}「{_coachTarget.demandLine}」");
            GUILayout.BeginHorizontal();
            foreach (CoachingCommand c in Enum.GetValues(typeof(CoachingCommand)))
                if (GUILayout.Button(CoachName(c))) e.Coach(c, _coachTarget);
            GUILayout.EndHorizontal();
        }

        /// <summary>7ゾーン×3列の簡易ミニマップ（● がボール）。</summary>
        static string MiniMap(int zone, int lane)
        {
            var sb = new System.Text.StringBuilder("[");
            for (int z = 0; z <= 6; z++) sb.Append(z == zone ? (lane == 0 ? "↖" : lane == 2 ? "↙" : "●") : "・");
            return sb.Append("]→").ToString();
        }

        static GUIStyle Big() => new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };

        static string TopicName(MeetingTopic t)
        {
            switch (t)
            {
                case MeetingTopic.Attack: return "攻撃的にいこう";
                case MeetingTopic.Defend: return "守備から入ろう";
                case MeetingTopic.Enjoy: return "楽しもう";
                case MeetingTopic.FireUp: return "気合を入れろ！";
                case MeetingTopic.TeamFirst: return "チームのために戦おう";
                case MeetingTopic.Freedom: return "自由にやれ";
                case MeetingTopic.OffsideTrapPlan: return "オフサイドトラップで行く";
                default: return t.ToString();
            }
        }

        static string CommandName(MatchCommand c)
        {
            switch (c)
            {
                case MatchCommand.Dribble: return "ドリブル";
                case MatchCommand.FlairDribble: return "華麗なドリブル";
                case MatchCommand.Pass: return "パス";
                case MatchCommand.LongPass: return "ロングパス";
                case MatchCommand.OneTwo: return "ワンツー";
                case MatchCommand.Shoot: return "シュート";
                case MatchCommand.SpecialShot: return "必殺シュート";
                case MatchCommand.Keep: return "キープ";
                case MatchCommand.BackPass: return "バックパス";
                case MatchCommand.MissOnPurpose: return "わざと外す";
                case MatchCommand.SloppyPass: return "わざとパスミス";
                case MatchCommand.Tackle: return "タックル";
                case MatchCommand.PassCut: return "パスカット";
                case MatchCommand.Block: return "ブロック";
                case MatchCommand.OffsideTrap: return "オフサイドトラップ";
                case MatchCommand.Centering: return "センタリング";
                case MatchCommand.SpecialDribble: return "必殺ドリブル";
                case MatchCommand.Header: return "ヘディング";
                case MatchCommand.Volley: return "ボレー";
                case MatchCommand.Trap: return "トラップ";
                case MatchCommand.SpecialTackle: return "必殺タックル";
                case MatchCommand.SpecialBlock: return "必殺ブロック";
                case MatchCommand.Clear: return "クリア";
                case MatchCommand.Compete: return "競る";
                case MatchCommand.Catch: return "キャッチ";
                case MatchCommand.Punch: return "パンチング";
                case MatchCommand.SpecialSave: return "必殺セーブ";
                default: return "様子を見る";
            }
        }

        static string CoachName(CoachingCommand c)
        {
            switch (c)
            {
                case CoachingCommand.Praise: return "褒める";
                case CoachingCommand.Scold: return "叱る";
                case CoachingCommand.CalmDown: return "落ち着け";
                case CoachingCommand.GoForward: return "攻めろ";
                case CoachingCommand.DefendDeep: return "守れ";
                case CoachingCommand.PassAround: return "パスを回せ";
                case CoachingCommand.TakeOn: return "仕掛けろ";
                case CoachingCommand.PlayPhysical: return "体を張れ";
                case CoachingCommand.FreeRole: return "自由にやれ";
                case CoachingCommand.SupportRole: return "サポートに徹しろ";
                case CoachingCommand.GiveBallToMe: return "ボールを集めろ";
                default: return "ラインを上げろ";
            }
        }
    }
}
