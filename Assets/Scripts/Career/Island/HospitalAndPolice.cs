using System;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    /// <summary>
    /// 勇心町の病院。けがの治りが半分になる。医者・看護師と仲が良ければ無料。
    /// </summary>
    public class Hospital
    {
        public long fee = 30000;
        /// <summary>主人公が病院にいるか（場所の制限を使うとき IslandCareer が差し込む）。</summary>
        public Func<bool> atHospital;

        readonly PlayerState _player;
        readonly IslandPopulation _pop;

        public Hospital(PlayerState player, IslandPopulation pop)
        {
            _player = player;
            _pop = pop;
        }

        /// <summary>知り合いの医者・看護師（好感度50以上）がいれば無料で診てくれる。</summary>
        public NPCData FriendlyDoctor()
        {
            foreach (var e in _pop.Knowledge.entries)
            {
                if (e.affinity < 50) continue;
                var n = _pop.Database.Get(e.npcId);
                if (n != null && (n.occupation == Occupation.Doctor || n.occupation == Occupation.Nurse)) return n;
            }
            return null;
        }

        public string TreatProtagonist()
        {
            int day = _pop.Database.currentDay;
            if (atHospital != null && !atHospital()) return "病院（勇心町）まで行かないと";
            if (!_player.IsInjured(day)) return "どこも悪くない";
            if (!Pay()) return "お金が足りない";
            int left = _player.injuredUntilDay - day;
            _player.injuredUntilDay = day + Math.Max(0, left / 2);
            return _player.IsInjured(day) ? $"治療を受けた。あと{_player.injuredUntilDay - day}日で治る。" : "治療を受けて、すっかり良くなった！";
        }

        /// <summary>島民（チームの選手など）を治療に連れていく。</summary>
        public string TreatIslander(int npcId)
        {
            int day = _pop.Database.currentDay;
            var n = _pop.Database.Get(npcId);
            if (atHospital != null && !atHospital()) return "病院（勇心町）まで連れていかないと";
            if (n == null || !n.IsInjured(day)) return "けがはしていない";
            if (!Pay()) return "お金が足りない";
            int left = n.injuredUntilDay - day;
            n.injuredUntilDay = day + Math.Max(0, left / 2);
            var e = _pop.Knowledge.GetOrCreate(npcId);
            e.affinity = Math.Min(100, e.affinity + 8);
            return $"{n.FullName}を病院に連れていった。とても感謝されている。";
        }

        bool Pay() => FriendlyDoctor() != null || _player.TrySpend(fee, "病院", _pop.Database.currentDay);
    }

    /// <summary>
    /// 交番。闇金を通報できる（闇金は違法なので、うまくいけば借金が消える）。
    /// 警官と仲が良いほど動いてくれる。ただしマフィアには恨まれる。
    /// </summary>
    public class PoliceBox
    {
        public int cooldownDays = 7;

        readonly PlayerState _player;
        readonly IslandPopulation _pop;
        readonly EconomyState _economy;
        readonly System.Collections.Generic.List<FameEvent> _fameEvents;
        readonly SeededRandom _rng;
        int _lastReportDay = -100;

        public PoliceBox(PlayerState player, IslandPopulation pop, EconomyState economy, System.Collections.Generic.List<FameEvent> fameEvents, SeededRandom rng)
        {
            _player = player;
            _pop = pop;
            _economy = economy;
            _fameEvents = fameEvents;
            _rng = rng;
        }

        public NPCData FriendlyCop()
        {
            foreach (var e in _pop.Knowledge.entries)
            {
                if (e.affinity < 50) continue;
                var n = _pop.Database.Get(e.npcId);
                if (n != null && n.occupation == Occupation.Police && !n.isUnderground) return n;
            }
            return null;
        }

        public float ReportChance()
        {
            float c = FriendlyCop() != null ? 0.85f : 0.4f;
            foreach (var l in _economy.loans) if (l.lender == Lender.LoanShark && l.pressureLevel >= 2) { c += 0.1f; break; }
            return Math.Min(0.95f, c);
        }

        /// <summary>闇金を通報する。成功すれば闇金の借金はすべて消える。</summary>
        public string ReportLoanShark()
        {
            int day = _pop.Database.currentDay;
            if (!_economy.loans.Exists(l => l.lender == Lender.LoanShark)) return "通報することがない";
            if (day - _lastReportDay < cooldownDays) return "「この前の件は今調べてるところです」";
            _lastReportDay = day;

            if (_rng.Chance(ReportChance()))
            {
                _economy.loans.RemoveAll(l => l.lender == Lender.LoanShark);
                // マフィアに恨まれる
                foreach (var e in _pop.Knowledge.entries)
                {
                    var n = _pop.Database.Get(e.npcId);
                    if (n != null && n.occupation == Occupation.Mafia) e.affinity = Math.Max(0, e.affinity - 25);
                }
                _fameEvents.Add(new FameEvent { type = FameEventType.LoanSharkBusted, day = day, npcId = -1 });
                return "警察が動いた！ 闇金は摘発され、借金は消えた。……マフィアの視線が冷たい。";
            }
            foreach (var l in _economy.loans) if (l.lender == Lender.LoanShark) l.pressureLevel = Math.Min(3, l.pressureLevel + 1);
            return "「証拠がないとねえ……」 通報したことが闇金にばれたらしい。";
        }
    }
}
