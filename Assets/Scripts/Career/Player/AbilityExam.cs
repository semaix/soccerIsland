using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    public class ExamResult
    {
        public int before;
        public int after;
        public bool Improved => ExamGrades.IsBetter(after, before);
    }

    /// <summary>
    /// サッカー協会の能力検定（主人公用）。協会で受けられる（受験料あり、30日に1回）。
    /// 級が上がると知名度が上がる。島民の級は公開されない。
    /// </summary>
    public class AbilityExam
    {
        public long fee = 5000;
        public int cooldownDays = 30;

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
            var r = new ExamResult { before = _player.examGrade, after = ExamGrades.FromOverall(score) };
            if (r.Improved)
            {
                int gained = r.before < 0 ? 1 : r.before - r.after;
                _player.examGrade = r.after;
                _fame.AddFame(gained * 1.5f);
            }
            return r;
        }
    }
}
