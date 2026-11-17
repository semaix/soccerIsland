namespace SoccerIsland.NPC
{
    /// <summary>
    /// サッカー協会の能力検定の級（主人公が受ける）。0 = 初段、1〜10 = 1級〜10級。-1 = 未受験。
    /// 島民の級は公開しない（相手の実力の手がかりは市場価値だけ）。
    /// </summary>
    public static class ExamGrades
    {
        public const int None = -1;
        public const int Dan = 0;
        static readonly int[] Thresholds = { 92, 85, 78, 70, 62, 54, 46, 38, 30, 22 }; // 初段, 1級..9級

        public static int FromOverall(int overall)
        {
            for (int i = 0; i < Thresholds.Length; i++) if (overall >= Thresholds[i]) return i;
            return 10;
        }

        public static string Label(int grade)
        {
            if (grade < 0) return "未受験";
            if (grade == Dan) return "初段";
            return grade + "級";
        }

        /// <summary>a が b より上の級か（数字が小さいほど上）。</summary>
        public static bool IsBetter(int a, int b) => a >= 0 && (b < 0 || a < b);
    }
}
