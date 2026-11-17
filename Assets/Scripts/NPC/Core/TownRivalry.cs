using System;

namespace SoccerIsland.NPC
{
    /// <summary>
    /// 街同士の関係。プラスは仲良し、マイナスはライバル（-100〜100）。
    /// ・古鐘町（ヤンキー）と煌浜（チャラい）は犬猿の仲
    /// ・天峰台は島1番のプライドで、どの街も見下している（どの街からも嫌われている）
    /// ・勇心町と古鐘町は人情でつながっている
    /// </summary>
    public static class TownRivalry
    {
        public static int Between(Town a, Town b)
        {
            if (a == b) return 60;
            Town x = a < b ? a : b, y = a < b ? b : a;
            if (x == Town.Furugane && y == Town.Kirahama) return -70;
            if (x == Town.Yushin && y == Town.Furugane) return 30;
            if (y == Town.Tenpou || x == Town.Tenpou) return -40;
            if (x == Town.Yushin && y == Town.Kirahama) return -10;
            return 0;
        }

        public static bool AreRivals(Town a, Town b) => Between(a, b) <= -40;

        /// <summary>試合前のテンションの上乗せ。ライバルほど燃える。天峰台はいつもプライドで燃える。</summary>
        public static float DerbyTension(Town self, Town opponent)
        {
            float t = Math.Max(0, -Between(self, opponent)) / 5f;
            if (self == Town.Tenpou) t += 5f;
            return t;
        }

        public static string Flavor(Town a, Town b)
        {
            if (Between(a, b) <= -60) return "因縁の対決";
            if (Between(a, b) <= -40) return "ライバル対決";
            if (Between(a, b) >= 30) return "人情ダービー";
            return "";
        }
    }
}
