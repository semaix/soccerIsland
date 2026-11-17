using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    /// <summary>
    /// 引退と転生（周回プレイ）。
    /// ・引退すると前世の記録が残り、同じ島で次の人生が始まる（スタートの街は変わる）
    /// ・本人は気づいていないが、前世で知った「化ける選手」に既視感がある
    /// ・前世の自分とそのチームが、正体を隠したラスボスとして現れる
    /// ・2周目から恋愛、3周目で転生の謎と島のボス
    /// </summary>
    public static class ReincarnationSystem
    {
        public const int MinRetireAge = 30;
        public const int ForcedRetireAge = 45;

        public static bool CanRetire(PlayerState p) => p.age >= MinRetireAge;
        public static bool MustRetire(PlayerState p) => p.age >= ForcedRetireAge;

        /// <summary>引退して前世の記録を作る。</summary>
        public static LegacyRecord Retire(IslandCareer career, bool force = false)
        {
            var p = career.Player;
            if (!force && !CanRetire(p)) return null;
            var db = career.Population.Database;
            var rec = new LegacyRecord
            {
                cycle = p.cycle, islandSeed = db.seed, playerName = p.name, homeTown = p.homeTown,
                retiredAge = p.age, retiredYear = db.currentYear, position = p.position, level = p.level, fame = p.fame,
                abilities = p.abilities.Clone(), teamName = p.teamName,
            };
            foreach (var c in p.contracts)
            {
                var n = db.Get(c.npcId);
                if (n == null) continue;
                rec.teammates.Add(new LegacyTeammate
                {
                    npcId = n.id, name = n.FullName, position = n.position, personality = n.personality,
                    soccerPersonality = n.soccerPersonality, stats = n.abilities.Clone(),
                });
            }
            foreach (var e in career.Population.Knowledge.entries)
            {
                var n = db.Get(e.npcId);
                if (n == null) continue;
                bool bloomer = (n.growthType == GrowthType.Awakening || n.growthType == GrowthType.Late) && n.potential >= 70;
                if (bloomer) rec.bloomers.Add(n.id);
            }
            return rec;
        }

        /// <summary>次の人生を始める。島は同じシードで作り直される（同じ島民がいる）。</summary>
        public static IslandCareer StartNextCycle(LegacyRecord legacy, List<LegacyRecord> pastLives, NPCGenerationSettings settings = null)
        {
            settings = settings ?? NPCGenerationSettings.CreateDefault();
            settings.seed = legacy.islandSeed;
            var population = IslandPopulation.CreateNew(settings);
            var world = new IslandWorld(population);

            var player = new PlayerState
            {
                name = legacy.playerName,
                cycle = legacy.cycle + 1,
                homeTown = NextTown(legacy.homeTown),
            };
            var lives = new List<LegacyRecord>();
            if (pastLives != null) lives.AddRange(pastLives);
            lives.Add(legacy);

            var career = new IslandCareer(world, new CareerSaveData { player = player, legacies = lives });

            // 既視感：初めて会うはずなのに、名前と「いつか化ける」ことを知っている
            foreach (var id in legacy.bloomers)
            {
                if (population.Database.Get(id) == null) continue;
                population.Knowledge.Reveal(id, NPCInfoFlags.Name | NPCInfoFlags.GrowthHint);
                player.dejaVu.Add(id);
            }
            return career;
        }

        /// <summary>前の人生とは違う街から始まる。</summary>
        public static Town NextTown(Town t)
        {
            var towns = (Town[])Enum.GetValues(typeof(Town));
            return towns[((int)t + 1) % towns.Length];
        }

        public static bool RomanceUnlocked(PlayerState p) => p.cycle >= 2;
        public static bool IslandMysteryUnlocked(PlayerState p) => p.cycle >= 3;
    }
}
