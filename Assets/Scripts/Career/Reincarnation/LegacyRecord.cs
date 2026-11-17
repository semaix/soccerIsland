using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    /// <summary>前世のチームメイト（引退した時点の姿）。</summary>
    [Serializable]
    public class LegacyTeammate
    {
        public int npcId;
        public string name;
        public Position position;
        public Personality personality;
        public SoccerPersonality soccerPersonality;
        public NPCAbilities stats = new NPCAbilities();
    }

    /// <summary>
    /// 前世の記録。引退した主人公とチームの最後の姿。
    /// 次の周回ではこれが「仮面のチーム」（ラスボス）になる。プレイヤーには正体を明かさない。
    /// </summary>
    [Serializable]
    public class LegacyRecord
    {
        public int cycle;
        public int islandSeed;
        public string playerName;
        public Town homeTown;
        public int retiredAge;
        public int retiredYear;
        public Position position;
        public int level;
        public float fame;
        public NPCAbilities abilities = new NPCAbilities();
        public string teamName;
        public List<LegacyTeammate> teammates = new List<LegacyTeammate>();
        /// <summary>前世で知り合った「化ける選手」。次の人生で既視感になる。</summary>
        public List<int> bloomers = new List<int>();
        public List<string> trophies = new List<string>();
    }
}
