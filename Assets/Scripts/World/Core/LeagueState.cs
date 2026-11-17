using System;
using System.Collections.Generic;

namespace SoccerIsland.World
{
    [Serializable]
    public class ClubStanding
    {
        public int clubId;
        public string name;
        public int played, won, drawn, lost, goalsFor, goalsAgainst;
        public int Points => won * 3 + drawn;
        public int GoalDifference => goalsFor - goalsAgainst;
    }

    [Serializable]
    public class ScorerEntry
    {
        /// <summary>-1 は主人公。</summary>
        public int npcId;
        public int goals;
    }

    /// <summary>年間表彰。</summary>
    [Serializable]
    public class SeasonAwards
    {
        public int season;
        public int announcedDay;
        public int championClubId = -1;
        public string championName;
        public int topScorerId = int.MinValue;
        public int topScorerGoals;
        public int mvpId = int.MinValue;
        public int rookieId = int.MinValue;
        public int rookieGoals;

        public bool Includes(int npcId) => topScorerId == npcId || mvpId == npcId || rookieId == npcId;
    }

    /// <summary>サッカー協会の公式リーグ（町クラブ・職場チームの試合）。</summary>
    [Serializable]
    public class LeagueState
    {
        public int season;
        public List<ClubStanding> table = new List<ClubStanding>();
        public List<ScorerEntry> scorers = new List<ScorerEntry>();
        public List<SeasonAwards> history = new List<SeasonAwards>();

        public SeasonAwards LastAwards => history.Count > 0 ? history[history.Count - 1] : null;
    }
}
