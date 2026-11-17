using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    /// <summary>
    /// 主人公の成長。試合で稼いだ経験値でレベルが上がり、能力値・ガッツが伸びる。
    /// </summary>
    public static class PlayerGrowth
    {
        public const int MaxLevel = 50;

        /// <summary>次のレベルまでに必要な経験値。</summary>
        public static int ExpToNext(int level) => (int)Math.Round(40 * Math.Pow(level, 1.4));

        /// <summary>経験値を足す。上がったレベルの一覧を返す。</summary>
        public static List<int> AddExperience(PlayerState p, int amount)
        {
            var ups = new List<int>();
            if (amount <= 0) return ups;
            p.exp += amount;
            while (p.level < MaxLevel && p.exp >= ExpToNext(p.level))
            {
                p.exp -= ExpToNext(p.level);
                p.level++;
                Grow(p);
                ups.Add(p.level);
            }
            return ups;
        }

        static void Grow(PlayerState p)
        {
            var a = p.abilities;
            for (int i = 0; i < NPCAbilities.StatCount; i++)
            {
                if (i == 9 && p.position != Position.GK) continue; // キーパー能力はGKだけ
                a[i] = a[i] + 1;
            }
            // ポジションの得意分野はさらに伸びる
            switch (p.position)
            {
                case Position.FW: a.shooting += 1; a.speed += 1; break;
                case Position.MF: a.passing += 1; a.technique += 1; break;
                case Position.DF: a.defense += 1; a.physical += 1; break;
                case Position.GK: a.goalkeeping += 2; break;
            }
            for (int i = 0; i < NPCAbilities.StatCount; i++) a[i] = NPCAbilities.Clamp(a[i]);
        }
    }
}
