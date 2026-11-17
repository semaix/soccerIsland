using System;
using System.Collections.Generic;
using SoccerIsland.Career;
using SoccerIsland.NPC;

namespace SoccerIsland.Match
{
    public enum BossKind
    {
        None = -1,
        Mafia,           // マフィア（黒潮會）：威圧＋審判買収
        Gifted,          // ギフテッド選抜：天才の読み
        PrisonGeniuses,  // 刑務所の天才たち：威圧＋鉄壁
        GodTeam,         // 神様のチーム：天才の読み＋鉄壁
        UndergroundKings,// 裏の王者：絶対王者（弱点を見つけないと勝てない）＋審判買収
    }

    /// <summary>ボス戦の相手チームを島民から作る。裏の試合なので本気の実力で来る。</summary>
    public static class BossTeams
    {
        public static string Name(BossKind k)
        {
            switch (k)
            {
                case BossKind.Mafia: return "黒潮會";
                case BossKind.Gifted: return "ギフテッド選抜";
                case BossKind.PrisonGeniuses: return "鉄格子FC 天才房";
                case BossKind.GodTeam: return "神様のチーム";
                case BossKind.UndergroundKings: return "裏の王者";
                default: return "";
            }
        }

        public static MatchTeam Build(BossKind kind, IslandCareer career, SeededRandom rng)
        {
            var db = career.Population.Database;
            List<NPCData> pool;
            var t = new MatchTeam { name = Name(kind) };
            switch (kind)
            {
                case BossKind.Mafia:
                    pool = db.Query(n => n.occupation == Occupation.Mafia && !n.isIncarcerated && n.age >= 16 && n.age <= 55);
                    t.traits.Add(BossTrait.Intimidation);
                    t.traits.Add(BossTrait.BribedReferee);
                    t.tactic = TeamTactic.HighPress;
                    break;
                case BossKind.Gifted:
                    pool = db.Query(n => !n.isIncarcerated && !n.isUnderground && n.age >= 14 && n.age <= 40 &&
                                         (n.talent >= 80 || n.soccerPersonality == SoccerPersonality.Gifted));
                    t.traits.Add(BossTrait.GeniusVision);
                    t.tactic = TeamTactic.Possession;
                    break;
                case BossKind.PrisonGeniuses:
                    pool = db.Query(n => n.isIncarcerated);
                    t.traits.Add(BossTrait.Intimidation);
                    t.traits.Add(BossTrait.IronWall);
                    t.tactic = TeamTactic.Counter;
                    break;
                case BossKind.GodTeam:
                    pool = db.Query(n => !n.isIncarcerated && n.age >= 15 && n.age <= 45 &&
                                         (n.soccerPersonality == SoccerPersonality.God || n.potential >= 85));
                    t.traits.Add(BossTrait.GeniusVision);
                    t.traits.Add(BossTrait.IronWall);
                    t.tactic = TeamTactic.CentralBreak;
                    break;
                default:
                    pool = db.Query(n => (n.isUnderground || n.soccerPersonality == SoccerPersonality.Phantom) && !n.isIncarcerated && n.age >= 16 && n.age <= 50);
                    t.traits.Add(BossTrait.Unbeatable);
                    t.traits.Add(BossTrait.BribedReferee);
                    t.tactic = TeamTactic.HighPress;
                    break;
            }

            pool.Sort((a, b) => b.Overall.CompareTo(a.Overall));
            // 神様のチームはゴッドを必ず入れる
            if (kind == BossKind.GodTeam)
            {
                var god = pool.Find(n => n.soccerPersonality == SoccerPersonality.God);
                if (god != null) { pool.Remove(god); pool.Insert(0, god); }
            }
            bool hasKeeper = false;
            foreach (var n in pool)
            {
                if (t.players.Count >= 11) break;
                if (n.position == Position.GK) { if (hasKeeper) continue; hasKeeper = true; }
                t.players.Add(MatchFactory.FromNPC(n, true, rng));
            }
            if (!hasKeeper)
            {
                // 一番キーパー向きの選手をGKに
                MatchPlayer best = null;
                foreach (var p in t.players) if (best == null || p.stats.goalkeeping > best.stats.goalkeeping) best = p;
                if (best != null) { best.position = Position.GK; best.naturalSet = false; best.RememberNaturalPosition(); }
            }
            if (t.players.Count < 11)
            {
                var fill = MatchFactory.Generic(t.name + "の手下", 60, 11 - t.players.Count + (hasKeeper || t.players.Count > 0 ? 1 : 0), rng);
                foreach (var p in fill.players)
                {
                    if (t.players.Count >= 11) break;
                    if (p.position == Position.GK && (hasKeeper || t.players.Exists(x => x.position == Position.GK))) continue;
                    t.players.Add(p);
                }
            }
            return t;
        }

        /// <summary>
        /// 裏の王者の弱点：謎のプレーヤー（幻影）の正体と裏の顔を知っていること。
        /// 知らないと能力が大幅に上がり、どうしても勝てない。
        /// </summary>
        public static bool KnowsUndergroundKingsWeakness(IslandCareer career)
        {
            foreach (var n in career.Population.Database.npcs)
            {
                if (n.soccerPersonality != SoccerPersonality.Phantom) continue;
                var e = career.Population.Knowledge.Get(n.id);
                if (e != null && e.Knows(NPCInfoFlags.Name) && e.Knows(NPCInfoFlags.SoccerPersonality)) return true;
            }
            return false;
        }
    }
}
