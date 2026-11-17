using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    public enum LifeEventType
    {
        Awakened,   // 覚醒した（下手だった選手が急に上手くなる）
        Released,   // 出所した
        Retired,    // 引退した
        Graduated,  // 学校を卒業して就職した
    }

    public struct LifeEvent
    {
        public LifeEventType type;
        public int npcId;
        public int year;
    }

    /// <summary>年が変わるときの処理：加齢・能力の再計算・覚醒・出所・引退。</summary>
    public class NPCLifecycleSystem
    {
        readonly NPCDatabase _db;
        readonly MarketValueSystem _market;
        readonly SeededRandom _rng;

        public int retireAge = 70;

        public NPCLifecycleSystem(NPCDatabase db, MarketValueSystem market, SeededRandom rng)
        {
            _db = db;
            _market = market;
            _rng = rng;
        }

        public List<LifeEvent> AdvanceYear()
        {
            _db.currentYear++;
            var events = new List<LifeEvent>();
            bool clubChanged = false;

            foreach (var npc in _db.npcs)
            {
                npc.age = _db.currentYear - npc.birthYear;
                bool wasAwake = npc.hasAwakened;
                GrowthCurve.RecalculateAbilities(npc);

                if (npc.growthType == GrowthType.Awakening && !wasAwake && npc.hasAwakened)
                {
                    events.Add(new LifeEvent { type = LifeEventType.Awakened, npcId = npc.id, year = _db.currentYear });
                    npc.hype += 0.3f;
                }

                if (npc.isIncarcerated && _db.currentYear >= npc.releaseYear)
                {
                    npc.isIncarcerated = false;
                    npc.occupation = Occupation.Unemployed;
                    npc.appearance.outfit = Occupation.Unemployed;
                    npc.clubId = -1;
                    clubChanged = true;
                    events.Add(new LifeEvent { type = LifeEventType.Released, npcId = npc.id, year = _db.currentYear });
                }

                if (npc.occupation == Occupation.Student && npc.age >= 23)
                {
                    npc.occupation = _rng.Chance(0.6f) ? Occupation.Salaryman : Occupation.ShopClerk;
                    npc.appearance.outfit = npc.occupation;
                    npc.jobIncome = 300 * 10000;
                    events.Add(new LifeEvent { type = LifeEventType.Graduated, npcId = npc.id, year = _db.currentYear });
                }

                if (npc.clubId >= 0 && npc.age >= retireAge && _rng.Chance(0.3f))
                {
                    npc.clubId = -1;
                    clubChanged = true;
                    events.Add(new LifeEvent { type = LifeEventType.Retired, npcId = npc.id, year = _db.currentYear });
                }

                _market.Refresh(npc, "new_year");
            }

            if (clubChanged) _db.MarkDirty();
            return events;
        }
    }
}
