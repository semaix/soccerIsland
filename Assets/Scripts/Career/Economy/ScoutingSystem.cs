using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    public enum ScoutResult
    {
        Success,
        NotEnoughMoney,       // お金が足りない
        NotAcquainted,        // 話したことがない
        LowAffinity,          // まだ信頼されていない
        Incarcerated,         // 服役中
        AlreadyInTeam,
        TooYoung,             // 15歳未満
        NeedsBlackMarket,     // 裏の人間は裏ルートでしか獲得できない
        Refused,              // 断られた（エゴイストは弱いチームを嫌う など）
    }

    /// <summary>
    /// スカウト。獲得には市場価値（＋性格による上乗せ）を払う。
    /// 話しかけて話題になるほど市場価値が上がるので、「いつ声をかけるか」も駆け引きになる。
    /// </summary>
    public class ScoutingSystem
    {
        public int minimumAge = 15;
        public int maxRosterSize = 25;

        readonly NPCDatabase _db;
        readonly NPCKnowledgeBook _book;
        readonly MarketValueSystem _market;
        readonly PlayerState _player;
        readonly List<EconomyEvent> _events;

        public ScoutingSystem(NPCDatabase db, NPCKnowledgeBook book, MarketValueSystem market, PlayerState player, List<EconomyEvent> events)
        {
            _db = db;
            _book = book;
            _market = market;
            _player = player;
            _events = events;
        }

        /// <summary>獲得に必要な金額。</summary>
        public long Price(NPCData npc)
        {
            double p = npc.marketValue;
            switch (npc.soccerPersonality)
            {
                case SoccerPersonality.Egoist: p *= 1.2; break;   // 足元を見てくる
                case SoccerPersonality.Devoted: p *= 0.9; break;  // 必要とされるのが嬉しい
                case SoccerPersonality.God: p *= 1.5; break;
            }
            if (!npc.registeredWithAssociation) p *= 1.5;         // 裏ルートの手数料
            return MarketValueCalculator.RoundValue(p);
        }

        /// <summary>契約に必要な好感度。</summary>
        public int RequiredAffinity(NPCData npc)
        {
            int a = 30;
            if (npc.personality == Personality.Gloomy) a += 10;
            if (npc.soccerPersonality == SoccerPersonality.Devoted) a -= 15;
            if (npc.soccerPersonality == SoccerPersonality.Phantom) a = 90;
            if (npc.isUnderground) a += 20;
            return a;
        }

        public ScoutResult Check(NPCData npc)
        {
            if (npc == null) return ScoutResult.NotAcquainted;
            if (_player.FindContract(npc.id) != null) return ScoutResult.AlreadyInTeam;
            if (npc.isIncarcerated) return ScoutResult.Incarcerated;
            if (npc.age < minimumAge) return ScoutResult.TooYoung;
            var entry = _book.Get(npc.id);
            if (entry == null || !entry.Knows(NPCInfoFlags.Name)) return ScoutResult.NotAcquainted;
            if (npc.isUnderground && (!_player.blackMarketAccess || !entry.Knows(NPCInfoFlags.UndergroundSecret)))
                return ScoutResult.NeedsBlackMarket;
            if (entry.affinity < RequiredAffinity(npc)) return ScoutResult.LowAffinity;
            if (_player.contracts.Count >= maxRosterSize) return ScoutResult.Refused;
            // エゴイストは自分より無名なチームには来ない
            if (npc.soccerPersonality == SoccerPersonality.Egoist && npc.fame > _player.fame + 30) return ScoutResult.Refused;
            if (!_player.CanAfford(Price(npc))) return ScoutResult.NotEnoughMoney;
            return ScoutResult.Success;
        }

        public ScoutResult Scout(int npcId)
        {
            var npc = _db.Get(npcId);
            var result = Check(npc);
            if (result != ScoutResult.Success) return result;

            long price = Price(npc);
            int day = _db.currentDay;
            _player.TrySpend(price, "スカウト: " + npc.FullName, day);

            EnsureTeamClub();
            _player.contracts.Add(new Contract
            {
                npcId = npc.id,
                salary = Math.Max(npc.salary, MarketValueCalculator.RoundValue(npc.marketValue * 0.06)),
                signedDay = day,
                transferFee = price,
                previousClubId = npc.clubId,
            });
            npc.clubId = _player.teamClubId;
            npc.registeredWithAssociation = npc.registeredWithAssociation || !npc.isUnderground;
            _db.MarkDirty();

            // 移籍は話題になる
            _market.AddHype(npc, 0.1f, "transfer");
            _events.Add(new EconomyEvent { type = EconomyEventType.Scouted, day = day, npcId = npc.id, amount = price });
            return ScoutResult.Success;
        }

        /// <summary>放出。元のクラブに戻る。移籍金は戻らない。</summary>
        public bool Release(int npcId, EconomyEventType reason = EconomyEventType.Released)
        {
            var c = _player.FindContract(npcId);
            var npc = _db.Get(npcId);
            if (c == null || npc == null) return false;
            _player.contracts.Remove(c);
            npc.clubId = _db.GetClub(c.previousClubId) != null ? c.previousClubId : -1;
            _db.MarkDirty();
            _events.Add(new EconomyEvent { type = reason, day = _db.currentDay, npcId = npcId });
            return true;
        }

        /// <summary>30日ごとの給料日。払えないと好感度が下がり、2か月続くと退団。</summary>
        public void Payroll()
        {
            int day = _db.currentDay;
            var leaving = new List<int>();
            foreach (var c in _player.contracts)
            {
                long monthly = c.salary / 12;
                var npc = _db.Get(c.npcId);
                if (_player.TrySpend(monthly, "給料: " + (npc != null ? npc.FullName : c.npcId.ToString()), day))
                {
                    c.unpaidMonths = 0;
                    continue;
                }
                c.unpaidMonths++;
                var entry = _book.GetOrCreate(c.npcId);
                entry.affinity = Math.Max(0, entry.affinity - 20);
                _events.Add(new EconomyEvent { type = EconomyEventType.PayrollFailed, day = day, npcId = c.npcId, amount = monthly });
                if (c.unpaidMonths >= 2) leaving.Add(c.npcId);
            }
            foreach (var id in leaving) Release(id, EconomyEventType.PlayerLeft);
        }

        void EnsureTeamClub()
        {
            if (_player.teamClubId >= 0 && _db.GetClub(_player.teamClubId) != null) return;
            int id = 0;
            foreach (var c in _db.clubs) id = Math.Max(id, c.id + 1);
            _db.AddClub(new SoccerClub { id = id, name = _player.teamName, town = _player.homeTown, category = ClubCategory.Town });
            _player.teamClubId = id;
            _db.MarkDirty();
        }
    }
}
