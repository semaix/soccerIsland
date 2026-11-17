using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.World
{
    /// <summary>
    /// 協会の公式リーグ。草サッカー・職場対抗戦（協会加盟クラブ同士）が公式戦。
    /// 1年ごとにシーズンが終わり、優勝・得点王・MVP・新人王が発表される。
    /// </summary>
    public class LeagueSystem
    {
        public int rookieMaxAge = 21;

        readonly NPCDatabase _db;
        readonly WorldState _state;

        public LeagueState League => _state.league;

        public LeagueSystem(NPCDatabase db, WorldState state)
        {
            _db = db;
            _state = state;
            if (League.season == 0) League.season = db.currentYear;
        }

        public bool IsLeagueMatch(Match m)
        {
            if (m.category != MatchCategory.Kusa && m.category != MatchCategory.Workplace) return false;
            var h = _db.GetClub(m.homeClubId);
            var a = _db.GetClub(m.awayClubId);
            return h != null && a != null && h != a && h.registeredWithAssociation && a.registeredWithAssociation;
        }

        /// <summary>終わった試合を順位表と得点ランキングに反映する。</summary>
        public void Record(Match m)
        {
            if (!m.played || !IsLeagueMatch(m)) return;
            var h = Standing(m.homeClubId, m.homeName);
            var a = Standing(m.awayClubId, m.awayName);
            h.played++; a.played++;
            h.goalsFor += m.homeScore; h.goalsAgainst += m.awayScore;
            a.goalsFor += m.awayScore; a.goalsAgainst += m.homeScore;
            if (m.homeScore > m.awayScore) { h.won++; a.lost++; }
            else if (m.homeScore < m.awayScore) { a.won++; h.lost++; }
            else { h.drawn++; a.drawn++; }
            foreach (var id in m.homeScorers) AddGoal(id);
            foreach (var id in m.awayScorers) AddGoal(id);
        }

        ClubStanding Standing(int clubId, string name)
        {
            foreach (var s in League.table) if (s.clubId == clubId) return s;
            var n = new ClubStanding { clubId = clubId, name = name };
            League.table.Add(n);
            return n;
        }

        void AddGoal(int npcId)
        {
            foreach (var s in League.scorers) if (s.npcId == npcId) { s.goals++; return; }
            League.scorers.Add(new ScorerEntry { npcId = npcId, goals = 1 });
        }

        public List<ClubStanding> Table()
        {
            var t = new List<ClubStanding>(League.table);
            t.Sort((x, y) =>
            {
                if (x.Points != y.Points) return y.Points.CompareTo(x.Points);
                if (x.GoalDifference != y.GoalDifference) return y.GoalDifference.CompareTo(x.GoalDifference);
                return y.goalsFor.CompareTo(x.goalsFor);
            });
            return t;
        }

        public List<ScorerEntry> TopScorers(int top)
        {
            var t = new List<ScorerEntry>(League.scorers);
            t.Sort((x, y) => x.goals != y.goals ? y.goals.CompareTo(x.goals) : x.npcId.CompareTo(y.npcId));
            if (t.Count > top) t.RemoveRange(top, t.Count - top);
            return t;
        }

        /// <summary>シーズンを締めて表彰する。次のシーズンを始める。</summary>
        public SeasonAwards CloseSeason(int protagonistAge)
        {
            var awards = new SeasonAwards { season = League.season, announcedDay = _db.currentDay };
            var table = Table();
            if (table.Count > 0) { awards.championClubId = table[0].clubId; awards.championName = table[0].name; }

            var scorers = TopScorers(int.MaxValue);
            if (scorers.Count > 0) { awards.topScorerId = scorers[0].npcId; awards.topScorerGoals = scorers[0].goals; }

            // MVP：優勝クラブで一番点を取った選手（いなければ得点王）
            foreach (var s in scorers)
            {
                bool inChampion = s.npcId == -1 ? false : (_db.Get(s.npcId)?.clubId ?? -99) == awards.championClubId;
                if (inChampion) { awards.mvpId = s.npcId; break; }
            }
            if (awards.mvpId == int.MinValue) awards.mvpId = awards.topScorerId;

            // 新人王：21歳以下で一番点を取った選手
            foreach (var s in scorers)
            {
                int age = s.npcId == -1 ? protagonistAge : (_db.Get(s.npcId)?.age ?? 99);
                if (age <= rookieMaxAge) { awards.rookieId = s.npcId; awards.rookieGoals = s.goals; break; }
            }

            League.history.Add(awards);
            League.table.Clear();
            League.scorers.Clear();
            League.season++;
            return awards;
        }
    }
}
