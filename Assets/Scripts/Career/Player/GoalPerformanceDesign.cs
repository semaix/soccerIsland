using System;
using System.Collections.Generic;

namespace SoccerIsland.Career
{
    /// <summary>ゴールパフォーマンスの動き。</summary>
    public enum PerformanceMove
    {
        Run,             // 走り回る
        KneeSlide,       // ひざスライディング
        Backflip,        // バク宙
        Cartwheel,       // 側転
        Dance,           // ダンス
        PointToSky,      // 天を指さす
        HeartHands,      // ハート
        ShushCrowd,      // 観客を黙らせる
        CornerFlagKick,  // コーナーフラッグを蹴る
        Salute,          // 敬礼
        Robot,           // ロボットダンス
        Bow,             // お辞儀
        TeammateHug,     // 仲間と抱き合う
        ShirtOverHead,   // ユニフォームを脱ぐ（イエローカード！）
        FanWave,         // ファンに手を振る
    }

    [Serializable]
    public class GoalPerformanceDesign
    {
        public const int MaxMoves = 6;
        public const int MaxNameLength = 16;

        public string name = "マイパフォーマンス";
        public List<PerformanceMove> moves = new List<PerformanceMove>();
        /// <summary>使った回数（同じパフォーマンスばかりだとファンが飽きる）。</summary>
        public int timesUsed;

        public string Validate()
        {
            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0) return "名前をつけてください";
            if (name.Length > MaxNameLength) return $"名前は{MaxNameLength}文字までです";
            if (moves.Count == 0) return "動きを1つ以上入れてください";
            if (moves.Count > MaxMoves) return $"動きは{MaxMoves}個までです";
            return null;
        }

        public bool IsValid => Validate() == null;

        public static float Flair(PerformanceMove m)
        {
            switch (m)
            {
                case PerformanceMove.Backflip: return 3f;
                case PerformanceMove.Cartwheel: return 2.5f;
                case PerformanceMove.Dance: return 2f;
                case PerformanceMove.Robot: return 2f;
                case PerformanceMove.KneeSlide: return 1.5f;
                case PerformanceMove.ShirtOverHead: return 2.5f;
                case PerformanceMove.CornerFlagKick: return 1.5f;
                case PerformanceMove.ShushCrowd: return 1.5f;
                case PerformanceMove.HeartHands: return 1.2f;
                case PerformanceMove.FanWave: return 1.2f;
                case PerformanceMove.PointToSky: return 1f;
                case PerformanceMove.Salute: return 1f;
                case PerformanceMove.Bow: return 0.8f;
                case PerformanceMove.TeammateHug: return 0.8f;
                default: return 0.5f;
            }
        }

        /// <summary>派手さ（0〜10程度）。種類が多いほど高く、使い回すほど下がる。</summary>
        public float Score()
        {
            if (!IsValid) return 0f;
            float sum = 0f;
            var distinct = new HashSet<PerformanceMove>();
            foreach (var m in moves) { sum += Flair(m); distinct.Add(m); }
            float variety = 1f + 0.15f * (distinct.Count - 1);
            float freshness = 1f / (1f + timesUsed * 0.15f);
            return Math.Min(10f, sum * variety * freshness);
        }

        public bool RisksYellowCard => moves.Contains(PerformanceMove.ShirtOverHead);
        /// <summary>相手サポーターを怒らせる（ゴシップのネタ）。</summary>
        public bool IsProvocative => moves.Contains(PerformanceMove.ShushCrowd) || moves.Contains(PerformanceMove.CornerFlagKick);
    }
}
