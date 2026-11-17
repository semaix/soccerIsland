using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    public class ExamResult
    {
        public int npcId; // -1 は主人公
        public int before;
        public int after;
        public bool Improved => ExamGrades.IsBetter(after, before);
        /// <summary>3級以上の大幅アップ（化けた選手）。</summary>
        public bool BigJump => before >= 0 && after >= 0 && before - after >= 3;
    }

    /// <summary>
    /// サッカー協会の能力検定。毎月15日（30日周期の15日目）に開催。
    /// 主人公はいつでも協会で受けられる（受験料あり、30日に1回）。
    /// 島民は検定日に一部が受け直し、級が更新される。
    /// </summary>
    public class AbilityExam
    {
        public long fee = 5000;
        public int cooldownDays = 30;
        public float monthlyRetakeRate = 0.05f;

        readonly PlayerState _player;
        readonly NPCDatabase _db;
        readonly FameSystem _fame;
        readonly SeededRandom _rng;

        public AbilityExam(PlayerState player, NPCDatabase db, FameSystem fame, SeededRandom rng)
        {
            _player = player;
            _db = db;
            _fame = fame;
            _rng = rng;
        }

        public static bool IsExamDay(int day) => day % 30 == 15;

        public bool CanTakeExam => _db.currentDay - _player.lastExamDay >= cooldownDays && _player.CanAfford(fee);

        /// <summary>主人公が受験する。緊張で多少ぶれる（メンタルが高いとぶれにくい）。</summary>
        public ExamResult TakeExam()
        {
            if (!CanTakeExam) return null;
            _player.TrySpend(fee, "能力検定 受験料", _db.currentDay);
            _player.lastExamDay = _db.currentDay;
            int overall = _player.abilities.Overall(_player.position);
            float nerves = Math.Max(1f, 6f - _player.abilities.mental / 20f);
            int score = (int)Math.Round(overall + _rng.Gaussian(0f, nerves));
            var r = new ExamResult { npcId = -1, before = _player.examGrade, after = ExamGrades.FromOverall(score) };
            if (r.Improved)
            {
                int gained = r.before < 0 ? 1 : r.before - r.after;
                _player.examGrade = r.after;
                _fame.AddFame(gained * 1.5f);
            }
            return r;
        }

        /// <summary>検定日に島民の一部が受け直す。</summary>
        public List<ExamResult> RunMonthlyExam(float? retakeRate = null)
        {
            var results = new List<ExamResult>();
            float rate = retakeRate ?? monthlyRetakeRate;
            foreach (var n in _db.npcs)
            {
                if (!n.registeredWithAssociation || n.isUnderground || n.isIncarcerated || n.age < 12 || n.age > 50) continue;
                if (!_rng.Chance(rate)) continue;
                int before = n.examGrade;
                n.examGrade = ExamGrades.FromOverall(n.PublicOverall + _rng.Range(-2, 3));
                n.examYear = _db.currentYear;
                results.Add(new ExamResult { npcId = n.id, before = before, after = n.examGrade });
            }
            return results;
        }

        /// <summary>協会が公表する上位合格者（2級以上）。</summary>
        public List<NPCData> PublishedTopGrades()
        {
            var list = _db.Query(n => n.examGrade >= 0 && n.examGrade <= 2 && n.AppearsInOfficialRanking);
            list.Sort((a, b) => a.examGrade != b.examGrade ? a.examGrade.CompareTo(b.examGrade) : b.marketValue.CompareTo(a.marketValue));
            return list;
        }
    }
}
