using System;
using System.Collections.Generic;
using SoccerIsland.Career;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Match
{
    public enum TournamentKind
    {
        GodCup,                 // サッカーの神様を目指す大会
        UndergroundTournament,  // 裏トーナメント
        RegionalCup,            // 地域対抗カップ
        SummitCup,              // 島の頂上決戦（2周目〜。決勝は仮面のチーム）
        ReincarnationFinal,     // 転生の謎を解く最終決戦（3周目〜。島のボス）
    }

    [Serializable]
    public class TournamentEntry
    {
        public string name;
        public int clubId = -1;
        public BossKind boss = BossKind.None;
        public bool isPlayer;
        /// <summary>地域対抗用：街の選抜。</summary>
        public int town = -1;
    }

    [Serializable]
    public class TournamentMatchRecord
    {
        public int round;
        public int a, b;
        public int scoreA, scoreB;
        public bool penalties;
        public int pkA, pkB;
        public int winner;
    }

    /// <summary>トーナメントの進行状況（セーブ可能）。</summary>
    [Serializable]
    public class TournamentState
    {
        public string name;
        public TournamentKind kind;
        public List<TournamentEntry> entries = new List<TournamentEntry>();
        /// <summary>勝ち残っているエントリー（対戦順）。</summary>
        public List<int> alive = new List<int>();
        public List<TournamentMatchRecord> history = new List<TournamentMatchRecord>();
        public int round = 1;
        public bool finished;
        public int champion = -1;
        public long prize;
        public float famePrize;
        public bool underground;
        public int seed;

        public int PlayerIndex
        {
            get { for (int i = 0; i < entries.Count; i++) if (entries[i].isPlayer) return i; return -1; }
        }
        public bool PlayerAlive => alive.Contains(PlayerIndex);
        public bool PlayerWon => finished && champion == PlayerIndex;

        public string RoundName
        {
            get
            {
                int n = alive.Count;
                return n == 2 ? "決勝" : n == 4 ? "準決勝" : n == 8 ? "準々決勝" : $"{round}回戦";
            }
        }
    }

    /// <summary>
    /// トーナメント。主人公の試合は MatchSession で遊び、ほかの試合はCPU同士で計算する。
    /// 引き分けはPK戦。
    /// </summary>
    public class TournamentSystem
    {
        readonly IslandCareer _career;
        public TournamentState State { get; }

        public TournamentSystem(IslandCareer career, TournamentState state)
        {
            _career = career;
            State = state;
        }

        // ------------------------------------------------------------------
        // 大会を作る
        // ------------------------------------------------------------------

        /// <summary>サッカーの神様を目指す大会。強豪クラブ・ギフテッド・神様のチームと戦う8チームのトーナメント。</summary>
        public static TournamentSystem CreateGodCup(IslandCareer career)
        {
            var st = new TournamentState { name = "サッカーの神様を目指す大会", kind = TournamentKind.GodCup, prize = 10000000, famePrize = 15f, seed = career.Population.Database.currentDay * 131 + 1 };
            st.entries.Add(new TournamentEntry { name = career.Player.teamName, isPlayer = true });
            foreach (var club in StrongestClubs(career, 5, false)) st.entries.Add(new TournamentEntry { name = club.name, clubId = club.id });
            st.entries.Add(new TournamentEntry { name = BossTeams.Name(BossKind.Gifted), boss = BossKind.Gifted });
            st.entries.Add(new TournamentEntry { name = BossTeams.Name(BossKind.GodTeam), boss = BossKind.GodTeam });
            Seed(st, godTeamLast: true);
            return new TournamentSystem(career, st);
        }

        /// <summary>裏トーナメント。裏市場に出入りできないと参加できない。参加費が要る。</summary>
        public static TournamentSystem CreateUndergroundTournament(IslandCareer career, long entryFee = 500000)
        {
            if (!career.Player.blackMarketAccess) return null;
            if (!career.Player.TrySpend(entryFee, "裏トーナメント参加費", career.Population.Database.currentDay)) return null;
            var st = new TournamentState { name = "裏トーナメント", kind = TournamentKind.UndergroundTournament, prize = 30000000, famePrize = 0f, underground = true, seed = career.Population.Database.currentDay * 977 + 3 };
            st.entries.Add(new TournamentEntry { name = career.Player.teamName, isPlayer = true });
            st.entries.Add(new TournamentEntry { name = BossTeams.Name(BossKind.Mafia), boss = BossKind.Mafia });
            foreach (var club in career.Population.Database.clubs)
                if (club.category == ClubCategory.Underground && club.name != "黒潮會フットサル部" && st.entries.Count < 6)
                    st.entries.Add(new TournamentEntry { name = club.name, clubId = club.id });
            while (st.entries.Count < 7) st.entries.Add(new TournamentEntry { name = $"裏の寄せ集め{st.entries.Count}", clubId = -2 });
            st.entries.Add(new TournamentEntry { name = BossTeams.Name(BossKind.UndergroundKings), boss = BossKind.UndergroundKings });
            Seed(st, godTeamLast: true);
            return new TournamentSystem(career, st);
        }

        /// <summary>地域対抗カップ：4つの街の選抜＋主人公のチーム…ではなく、主人公は住んでいる街の選抜に入る。</summary>
        public static TournamentSystem CreateRegionalCup(IslandCareer career)
        {
            var st = new TournamentState { name = "地域対抗カップ", kind = TournamentKind.RegionalCup, prize = 2000000, famePrize = 8f, seed = career.Population.Database.currentDay * 59 + 7 };
            foreach (Town t in Enum.GetValues(typeof(Town)))
                st.entries.Add(new TournamentEntry { name = NPCLabels.Of(t) + "選抜", town = (int)t, isPlayer = t == career.Player.homeTown });
            Seed(st, godTeamLast: false);
            return new TournamentSystem(career, st);
        }

        /// <summary>島の頂上決戦（2周目以降）。決勝で正体不明の「仮面のチーム」と当たる。</summary>
        public static TournamentSystem CreateSummitCup(IslandCareer career)
        {
            if (career.Player.cycle < 2 || career.PreviousLife == null) return null;
            var st = new TournamentState { name = "島の頂上決戦", kind = TournamentKind.SummitCup, prize = 30000000, famePrize = 20f, seed = career.Population.Database.currentDay * 211 + 5 };
            st.entries.Add(new TournamentEntry { name = career.Player.teamName, isPlayer = true });
            foreach (var club in StrongestClubs(career, 1, false)) st.entries.Add(new TournamentEntry { name = club.name, clubId = club.id });
            st.entries.Add(new TournamentEntry { name = BossTeams.Name(BossKind.Gifted), boss = BossKind.Gifted });
            st.entries.Add(new TournamentEntry { name = BossTeams.Name(BossKind.PastSelf), boss = BossKind.PastSelf });
            Seed(st, godTeamLast: true);
            return new TournamentSystem(career, st);
        }

        /// <summary>転生の謎を解く最終決戦（3周目以降）。島のボスとの一騎打ち。</summary>
        public static TournamentSystem CreateReincarnationFinal(IslandCareer career)
        {
            if (career.Player.cycle < 3) return null;
            var st = new TournamentState { name = "転生の謎を解く最終決戦", kind = TournamentKind.ReincarnationFinal, prize = 0, famePrize = 30f, seed = career.Population.Database.currentDay * 3 + 11 };
            st.entries.Add(new TournamentEntry { name = career.Player.teamName, isPlayer = true });
            st.entries.Add(new TournamentEntry { name = BossTeams.Name(BossKind.IslandMaster), boss = BossKind.IslandMaster });
            Seed(st, godTeamLast: true);
            return new TournamentSystem(career, st);
        }

        static void Seed(TournamentState st, bool godTeamLast)
        {
            // 主人公は最初の試合、最後のボスは逆の山の端に置き、決勝で当たるようにする
            st.alive.Clear();
            for (int i = 0; i < st.entries.Count; i++) st.alive.Add(i);
            var rng = new SeededRandom(st.seed);
            int player = st.PlayerIndex;
            int last = godTeamLast ? st.entries.Count - 1 : -1;
            var middle = st.alive.FindAll(i => i != player && i != last);
            rng.Shuffle(middle);
            st.alive.Clear();
            if (player >= 0) st.alive.Add(player);
            st.alive.AddRange(middle);
            if (last >= 0) st.alive.Add(last);
        }

        static List<SoccerClub> StrongestClubs(IslandCareer career, int count, bool underground)
        {
            var db = career.Population.Database;
            var list = new List<KeyValuePair<SoccerClub, double>>();
            foreach (var c in db.clubs)
            {
                if (c.id == career.Player.teamClubId || c.category == ClubCategory.Prison) continue;
                if ((c.category == ClubCategory.Underground) != underground) continue;
                double sum = 0;
                int n = 0;
                foreach (var m in db.GetClubMembers(c.id)) { sum += m.PublicOverall; n++; }
                if (n >= 8) list.Add(new KeyValuePair<SoccerClub, double>(c, sum / n));
            }
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var result = new List<SoccerClub>();
            for (int i = 0; i < list.Count && result.Count < count; i++) result.Add(list[i].Key);
            return result;
        }

        // ------------------------------------------------------------------
        // チームを組む
        // ------------------------------------------------------------------

        public MatchTeam BuildTeam(int entryIndex, SeededRandom rng)
        {
            var e = State.entries[entryIndex];
            var db = _career.Population.Database;
            MatchTeam t;
            if (e.boss != BossKind.None) t = BossTeams.Build(e.boss, _career, rng);
            else if (e.town >= 0)
            {
                var ids = new List<int>();
                var cands = db.Query(n => (int)n.hometown == e.town && n.AppearsInOfficialRanking && n.clubId >= 0 && n.age >= 15 && n.age <= 50);
                cands.Sort((a, b) => b.PublicOverall.CompareTo(a.PublicOverall));
                bool gk = false;
                foreach (var n in cands)
                {
                    if (ids.Count >= 11) break;
                    if (n.position == Position.GK) { if (gk) continue; gk = true; }
                    ids.Add(n.id);
                }
                t = MatchFactory.FromLineup(e.name, ids, db, State.underground, rng);
                t.town = e.town;
                if (e.isPlayer) MatchFactory.InsertProtagonist(t, _career.Player, 11);
            }
            else if (e.isPlayer) t = PlayerTeam(rng);
            else if (e.clubId >= 0)
            {
                var members = new List<NPCData>(db.GetClubMembers(e.clubId));
                members.RemoveAll(n => n.age < 13 || n.age > 60 || n.isIncarcerated);
                members.Sort((a, b) => (State.underground ? b.Overall : b.PublicOverall).CompareTo(State.underground ? a.Overall : a.PublicOverall));
                var ids = new List<int>();
                bool gk = false;
                foreach (var n in members)
                {
                    if (ids.Count >= 11) break;
                    if (n.position == Position.GK) { if (gk) continue; gk = true; }
                    ids.Add(n.id);
                }
                t = MatchFactory.FromLineup(e.name, ids, db, State.underground, rng);
                Fill(t, rng, 50);
            }
            else t = MatchFactory.Generic(e.name, 52, 11, rng);
            t.name = e.name;
            return t;
        }

        /// <summary>主人公のチーム：契約した選手＋主人公。足りなければ島の助っ人で埋める。</summary>
        MatchTeam PlayerTeam(SeededRandom rng)
        {
            var ids = new List<int>();
            foreach (var c in _career.Player.contracts) ids.Add(c.npcId);
            var t = MatchFactory.FromLineup(_career.Player.teamName, ids, _career.Population.Database, State.underground, rng);
            MatchFactory.InsertProtagonist(t, _career.Player, 11);
            Fill(t, rng, 45);
            MatchFactory.ApplyPartnerBoost(_career, t);
            return t;
        }

        static void Fill(MatchTeam t, SeededRandom rng, int strength)
        {
            if (t.players.Count >= 11) return;
            bool hasGk = t.players.Exists(p => p.position == Position.GK);
            var fill = MatchFactory.Generic("助っ人", strength, 11, rng);
            foreach (var p in fill.players)
            {
                if (t.players.Count >= 11) break;
                if (p.position == Position.GK && hasGk) continue;
                if (p.position == Position.GK) hasGk = true;
                t.players.Add(p);
            }
        }

        // ------------------------------------------------------------------
        // 進行
        // ------------------------------------------------------------------

        /// <summary>この回戦の主人公の試合（敗退済み・終了なら null）。</summary>
        public MatchSetup PlayerMatchSetup()
        {
            if (State.finished || !State.PlayerAlive) return null;
            int idx = State.alive.IndexOf(State.PlayerIndex);
            int opp = State.alive[idx % 2 == 0 ? idx + 1 : idx - 1];
            var rng = new SeededRandom(State.seed + State.round * 1000);
            bool playerHome = idx % 2 == 0;
            var me = BuildTeam(State.PlayerIndex, rng);
            var them = BuildTeam(opp, rng);
            var setup = new MatchSetup
            {
                title = $"{State.name} {State.RoundName}",
                category = State.underground ? MatchCategory.UndergroundFutsal : State.kind == TournamentKind.RegionalCup ? MatchCategory.RegionalDerby : MatchCategory.IslandSelect,
                home = playerHome ? me : them,
                away = playerHome ? them : me,
                protagonistSide = playerHome ? Side.Home : Side.Away,
                seed = rng.Range(1, int.MaxValue),
                crowd = State.underground ? 150 : State.kind == TournamentKind.GodCup ? 8000 : 3000,
                underground = State.underground,
                unbeatableBroken = BossTeams.KnowsUndergroundKingsWeakness(_career),
                celebration = _career.Player.SelectedPerformance,
            };
            return setup;
        }

        /// <summary>
        /// 回戦を終える。主人公が出ていれば playerResult を渡す（MatchSession で遊んだ結果）。
        /// ほかの試合はCPU同士で計算する。
        /// </summary>
        public List<TournamentMatchRecord> CompleteRound(MatchResult playerResult = null)
        {
            var records = new List<TournamentMatchRecord>();
            if (State.finished) return records;
            var next = new List<int>();
            var rng = new SeededRandom(State.seed + State.round * 7777);

            for (int i = 0; i + 1 < State.alive.Count; i += 2)
            {
                int a = State.alive[i], b = State.alive[i + 1];
                var rec = new TournamentMatchRecord { round = State.round, a = a, b = b };
                bool involvesPlayer = a == State.PlayerIndex || b == State.PlayerIndex;
                MatchResult r;
                MatchTeam ta, tb;
                if (involvesPlayer && playerResult != null)
                {
                    r = playerResult;
                    ta = tb = null;
                }
                else
                {
                    ta = BuildTeam(a, rng);
                    tb = BuildTeam(b, rng);
                    var setup = new MatchSetup
                    {
                        title = State.name, home = ta, away = tb, seed = rng.Range(1, int.MaxValue), underground = State.underground,
                        protagonistSide = involvesPlayer ? (a == State.PlayerIndex ? Side.Home : Side.Away) : (Side?)null,
                        unbeatableBroken = BossTeams.KnowsUndergroundKingsWeakness(_career),
                    };
                    r = new MatchEngine(setup) { Controller = new AutoPilot(rng.Range(1, 9999)) }.RunToEnd();
                }
                rec.scoreA = r.homeScore;
                rec.scoreB = r.awayScore;
                // 主人公がアウェイ側だった場合、記録はエントリー順に揃える
                if (involvesPlayer && playerResult != null && b == State.PlayerIndex && r.protagonistSide == Side.Home)
                { rec.scoreA = r.awayScore; rec.scoreB = r.homeScore; }

                if (rec.scoreA == rec.scoreB)
                {
                    rec.penalties = true;
                    if (ta == null) { ta = BuildTeam(a, rng); tb = BuildTeam(b, rng); }
                    Penalties(ta, tb, rng, out rec.pkA, out rec.pkB);
                    rec.winner = rec.pkA > rec.pkB ? a : b;
                }
                else rec.winner = rec.scoreA > rec.scoreB ? a : b;

                next.Add(rec.winner);
                State.history.Add(rec);
                records.Add(rec);
            }

            State.alive = next;
            State.round++;
            if (State.alive.Count <= 1)
            {
                State.finished = true;
                State.champion = State.alive.Count == 1 ? State.alive[0] : -1;
                if (State.PlayerWon) GiveRewards();
            }
            return records;
        }

        /// <summary>主人公の試合をおまかせで遊んで、回戦を終える（テスト・スキップ用）。</summary>
        public List<TournamentMatchRecord> AutoPlayRound()
        {
            var setup = PlayerMatchSetup();
            MatchResult r = null;
            if (setup != null)
            {
                var session = new MatchSession(_career, setup);
                r = session.AutoPlay();
            }
            return CompleteRound(r);
        }

        void GiveRewards()
        {
            int day = _career.Population.Database.currentDay;
            _career.Player.Earn(State.prize, State.name + " 優勝賞金", day);
            if (State.famePrize > 0f) _career.Fame.ReportTournamentWin(State.name, State.famePrize);
        }

        /// <summary>PK戦：5人ずつ、決まらなければサドンデス。</summary>
        static void Penalties(MatchTeam a, MatchTeam b, SeededRandom rng, out int pa, out int pb)
        {
            pa = pb = 0;
            var ka = a.Keeper; var kb = b.Keeper;
            var sa = Shooters(a); var sb = Shooters(b);
            for (int k = 0; k < 20; k++)
            {
                if (Kick(sa[k % sa.Count], kb, rng)) pa++;
                if (Kick(sb[k % sb.Count], ka, rng)) pb++;
                if (k >= 4 && pa != pb) return;
            }
            if (pa == pb) pa++; // 決着がつかなければコイントス
        }

        static List<MatchPlayer> Shooters(MatchTeam t)
        {
            var l = new List<MatchPlayer>(t.players);
            l.Sort((x, y) => y.stats.shooting.CompareTo(x.stats.shooting));
            return l;
        }

        static bool Kick(MatchPlayer shooter, MatchPlayer keeper, SeededRandom rng)
        {
            float p = 0.75f + (shooter.stats.shooting - (keeper?.stats.goalkeeping ?? 30)) / 200f;
            return rng.Chance(Math.Max(0.3f, Math.Min(0.95f, p)));
        }
    }
}
