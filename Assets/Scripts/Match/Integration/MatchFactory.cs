using System;
using System.Collections.Generic;
using SoccerIsland.Career;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Match
{
    /// <summary>島のデータから試合を組み立てる。</summary>
    public static class MatchFactory
    {
        /// <summary>島民を試合用の選手にする。表の試合では「表の実力」しか出さない。</summary>
        public static MatchPlayer FromNPC(NPCData n, bool underground, SeededRandom rng)
        {
            var stats = n.abilities.Clone();
            float rate = underground ? 1f : n.publicPerformanceRate;
            if (rate < 1f)
                for (int i = 0; i < NPCAbilities.StatCount; i++) stats[i] = (int)Math.Round(stats[i] * rate);
            var p = new MatchPlayer
            {
                npcId = n.id, name = n.FullName, position = n.position, stats = stats,
                personality = n.personality, soccerPersonality = n.soccerPersonality,
                maxGuts = 300 + stats.stamina * 6,
                tension = 55f + rng.Range(-10f, 10f) + (n.soccerPersonality == SoccerPersonality.MoodMaker ? 10f : 0f),
            };
            p.guts = p.maxGuts;
            return p;
        }

        public static MatchPlayer FromProtagonist(PlayerState s)
        {
            var p = new MatchPlayer
            {
                npcId = -1, name = s.name, isProtagonist = true, position = s.position, stats = s.abilities.Clone(),
                personality = Personality.Extrovert, soccerPersonality = SoccerPersonality.Devoted,
                maxGuts = 300 + s.abilities.stamina * 6, tension = 65f,
            };
            p.guts = p.maxGuts;
            return p;
        }

        /// <summary>本土選抜など、島民ではない相手。</summary>
        public static MatchTeam Generic(string name, int strength, int size, SeededRandom rng)
        {
            var t = new MatchTeam { name = name };
            Position[] positions = { Position.GK, Position.DF, Position.DF, Position.DF, Position.DF, Position.MF, Position.MF, Position.MF, Position.MF, Position.FW, Position.FW };
            for (int i = 0; i < size; i++)
            {
                var stats = new NPCAbilities();
                for (int k = 0; k < NPCAbilities.StatCount; k++) stats[k] = strength + rng.Range(-6, 7);
                var pos = size >= 11 ? positions[i] : (i == 0 ? Position.GK : i <= 2 ? Position.DF : Position.FW);
                stats.goalkeeping = pos == Position.GK ? strength + 5 : 15;
                var p = new MatchPlayer
                {
                    npcId = -1000 - i, name = $"{name} {i + 1}番", position = pos, stats = stats,
                    personality = (Personality)rng.Range(0, 3), soccerPersonality = (SoccerPersonality)rng.Range(0, 6),
                    maxGuts = 300 + stats.stamina * 6, tension = 60f,
                };
                p.guts = p.maxGuts;
                t.players.Add(p);
            }
            return t;
        }

        public static MatchTeam FromLineup(string name, IEnumerable<int> ids, NPCDatabase db, bool underground, SeededRandom rng)
        {
            var t = new MatchTeam { name = name };
            foreach (var id in ids)
            {
                var n = db.Get(id);
                if (n != null) t.players.Add(FromNPC(n, underground, rng));
            }
            EnsureKeeper(t);
            return t;
        }

        /// <summary>主人公をチームに入れる。11人いれば同じポジションで一番弱い選手と交代。</summary>
        public static void InsertProtagonist(MatchTeam t, PlayerState s, int teamSize)
        {
            var pro = FromProtagonist(s);
            if (t.players.Count >= teamSize)
            {
                MatchPlayer weakest = null;
                foreach (var p in t.players)
                {
                    if (p.position == Position.GK) continue;
                    bool samePos = p.position == pro.position;
                    if (weakest == null || (samePos && weakest.position != pro.position) ||
                        (samePos == (weakest.position == pro.position) && p.stats.Overall(p.position) < weakest.stats.Overall(weakest.position)))
                        weakest = p;
                }
                if (weakest != null) t.players.Remove(weakest);
            }
            t.players.Add(pro);
        }

        static void EnsureKeeper(MatchTeam t)
        {
            foreach (var p in t.players) if (p.position == Position.GK) return;
            MatchPlayer best = null;
            foreach (var p in t.players) if (best == null || p.stats.goalkeeping > best.stats.goalkeeping) best = p;
            if (best != null) best.position = Position.GK;
        }

        static int Crowd(MatchCategory c, float fame)
        {
            switch (c)
            {
                case MatchCategory.IslandSelect: return 5000;
                case MatchCategory.RegionalDerby: return 2000;
                case MatchCategory.UndergroundFutsal: return 60;
                case MatchCategory.PrisonLeague: return 200;
                case MatchCategory.Beach: return 300 + (int)(fame * 10);
                default: return 80 + (int)(fame * 20);
            }
        }

        /// <summary>
        /// 島の試合予定から試合を作る。主人公のチームの試合なら主人公側が自動で決まる。
        /// それ以外の試合に「助っ人」として出るときは protagonistSide を指定する。
        /// </summary>
        public static MatchSetup FromWorldMatch(IslandCareer career, SoccerIsland.World.Match m, Side? protagonistSide = null, int seed = 0)
        {
            var db = career.Population.Database;
            var rng = new SeededRandom(seed != 0 ? seed : m.id * 7919 + db.currentDay);
            bool underground = m.IsUnderground;
            int size = MatchLabels.TeamSize(m.category);

            var home = FromLineup(m.homeName, m.homeLineup, db, underground, rng);
            var away = m.awayLineup.Count > 0 ? FromLineup(m.awayName, m.awayLineup, db, underground, rng) : Generic(m.awayName, 66, size, rng);

            if (!protagonistSide.HasValue && career.Player.teamClubId >= 0)
            {
                if (m.homeClubId == career.Player.teamClubId) protagonistSide = Side.Home;
                else if (m.awayClubId == career.Player.teamClubId) protagonistSide = Side.Away;
            }
            if (protagonistSide == Side.Home) InsertProtagonist(home, career.Player, size);
            if (protagonistSide == Side.Away) InsertProtagonist(away, career.Player, size);

            return new MatchSetup
            {
                title = MatchLabels.Of(m.category), category = m.category, home = home, away = away,
                protagonistSide = protagonistSide, seed = rng.Range(1, int.MaxValue), underground = underground,
                crowd = Crowd(m.category, career.Player.fame), worldMatchId = m.id,
            };
        }

        /// <summary>主人公のチームの、今日の試合（なければ null）。</summary>
        public static SoccerIsland.World.Match TodaysTeamMatch(IslandCareer career)
        {
            int club = career.Player.teamClubId;
            if (club < 0) return null;
            foreach (var m in career.World.TodaysMatches())
                if (!m.played && (m.homeClubId == club || m.awayClubId == club)) return m;
            return null;
        }
    }
}
