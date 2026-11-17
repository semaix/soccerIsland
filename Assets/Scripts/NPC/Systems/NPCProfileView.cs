using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    /// <summary>
    /// プレイヤーに見せるプロフィール。知らない項目は「？？？」。
    /// 能力値はどれだけ仲良くなっても表示しない（スカウトの面白さのため）。
    /// </summary>
    public class NPCProfileView
    {
        public const string Unknown = "？？？";

        public int npcId;
        public string name = Unknown;
        public string age = Unknown;
        public string gender = Unknown;
        public string town = Unknown;
        public string occupation = Unknown;
        public string club = Unknown;
        public string position = Unknown;
        public string personality = Unknown;
        public string soccerPersonality = Unknown;
        public string salary = Unknown;
        public string marketValue = Unknown;
        public string talentRank = Unknown;
        public string effortRank = Unknown;
        public string phoneNumber = Unknown;
        public int affinity;
        public bool isRegisteredKnown;
        public List<string> notes = new List<string>();

        public static NPCProfileView Build(NPCData npc, NPCKnowledgeBook book, NPCDatabase db)
        {
            var v = new NPCProfileView { npcId = npc.id };
            var e = book.Get(npc.id);
            if (e == null) return v;
            v.affinity = e.affinity;

            if (e.Knows(NPCInfoFlags.Name)) { v.name = npc.FullName; v.gender = NPCLabels.Of(npc.gender); v.town = NPCLabels.Of(npc.hometown); }
            if (e.Knows(NPCInfoFlags.Age)) v.age = npc.age + "歳";
            if (e.Knows(NPCInfoFlags.Occupation)) v.occupation = NPCLabels.Of(npc.occupation);
            if (e.Knows(NPCInfoFlags.Club)) v.club = db.GetClub(npc.clubId)?.name ?? "無所属";
            if (e.Knows(NPCInfoFlags.Position)) v.position = npc.position.ToString();
            if (e.Knows(NPCInfoFlags.Personality)) v.personality = NPCLabels.Of(npc.personality);
            if (e.Knows(NPCInfoFlags.SoccerPersonality)) v.soccerPersonality = NPCLabels.Of(npc.soccerPersonality);
            if (e.Knows(NPCInfoFlags.Salary)) v.salary = NPCLabels.Yen(npc.salary);
            if (e.Knows(NPCInfoFlags.MarketValue)) { v.marketValue = NPCLabels.Yen(npc.marketValue); v.isRegisteredKnown = true; }
            if (e.Knows(NPCInfoFlags.Talent)) v.talentRank = NPCLabels.Rank(npc.talent);
            if (e.Knows(NPCInfoFlags.Effort)) v.effortRank = NPCLabels.Rank(npc.effort);
            if (e.Knows(NPCInfoFlags.PhoneNumber) && !string.IsNullOrEmpty(npc.phoneNumber)) v.phoneNumber = npc.phoneNumber;
            if (e.Knows(NPCInfoFlags.UndergroundSecret) && npc.isUnderground) v.notes.Add("裏の顔を持っている");
            if (e.Knows(NPCInfoFlags.MarketValue) && !npc.AppearsInOfficialRanking) v.notes.Add("サッカー協会に載っていない");
            if (npc.isIncarcerated) v.notes.Add($"服役中（{npc.releaseYear}年 出所予定）");
            if (npc.isStranded) v.notes.Add("島に漂流してきた。島から出られない");
            return v;
        }
    }
}
