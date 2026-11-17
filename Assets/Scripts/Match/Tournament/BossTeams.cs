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
        PastSelf,        // 仮面のチーム：前世の主人公とそのチーム（正体は明かさない）
        IslandMaster,    // 島のボス：転生の謎を握る者（3周目）
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
                case BossKind.PastSelf: return "仮面のチーム";
                case BossKind.IslandMaster: return "島主の一団";
                default: return "";
            }
        }

        public static MatchTeam Build(BossKind kind, IslandCareer career, SeededRandom rng)
        {
            if (kind == BossKind.PastSelf) return PastSelf(career.PreviousLife, rng);
            if (kind == BossKind.IslandMaster) return IslandMaster(career, rng);
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
        /// 仮面のチーム：前世の主人公と、前世で集めたチームが引退した時の姿で現れる。
        /// 名前はすべて伏せる（プレイヤーには誰がラスボスかわからない）。
        /// 前世の自分なので、こちらの手の内を読んでくる。
        /// </summary>
        public static MatchTeam PastSelf(LegacyRecord legacy, SeededRandom rng)
        {
            var t = new MatchTeam { name = Name(BossKind.PastSelf) };
            t.traits.Add(BossTrait.GeniusVision);
            t.traits.Add(BossTrait.Intimidation);
            t.tactic = TeamTactic.CentralBreak;
            if (legacy == null) return Generic(t, rng, 70);

            var leader = new MatchPlayer
            {
                npcId = -900, name = "仮面の男", position = legacy.position, stats = legacy.abilities.Clone(),
                personality = Personality.Extrovert, soccerPersonality = SoccerPersonality.Devoted,
                maxGuts = 300 + legacy.abilities.stamina * 6 + legacy.level * 10, tension = 80f,
                specials = MatchFactory.ProtagonistSpecials(legacy.level), // こちらと同じ技を使ってくる……
            };
            leader.guts = leader.maxGuts;
            t.players.Add(leader);
            int no = 1;
            foreach (var m in legacy.teammates)
            {
                if (t.players.Count >= 11) break;
                var p = new MatchPlayer
                {
                    npcId = -900 - no, name = $"仮面の選手{no}", position = m.position, stats = m.stats.Clone(),
                    personality = m.personality, soccerPersonality = m.soccerPersonality,
                    maxGuts = 300 + m.stats.stamina * 6, tension = 70f,
                };
                p.guts = p.maxGuts;
                t.players.Add(p);
                no++;
            }
            return Generic(t, rng, Math.Max(55, legacy.abilities.Overall(legacy.position) - 10));
        }

        /// <summary>島のボス：転生を繰り返させている「島主」と、その配下。</summary>
        public static MatchTeam IslandMaster(IslandCareer career, SeededRandom rng)
        {
            var t = new MatchTeam { name = Name(BossKind.IslandMaster) };
            t.traits.Add(BossTrait.Intimidation);
            t.traits.Add(BossTrait.GeniusVision);
            t.traits.Add(BossTrait.IronWall);
            t.tactic = TeamTactic.HighPress;
            var master = new MatchPlayer
            {
                npcId = -950, name = "島主", position = Position.MF, stats = new NPCAbilities(),
                personality = Personality.Gloomy, soccerPersonality = SoccerPersonality.God,
                tension = 90f,
            };
            for (int i = 0; i < NPCAbilities.StatCount; i++) master.stats[i] = 96;
            master.stats.goalkeeping = 40;
            master.maxGuts = master.guts = 1400;
            master.specials.Add(new SpecialMove("輪廻", SpecialKind.Shot, 34, 240, "何度生まれ変わっても、同じことだ。"));
            master.specials.Add(new SpecialMove("因果の糸", SpecialKind.Dribble, 20, 150, "お前の動きは、前の人生で見た。"));
            t.players.Add(master);

            // 配下は島の歴代最強たち（前世の記録の自分たちも含む）
            var db = career.Population.Database;
            var pool = db.Query(n => !n.isIncarcerated && n.age >= 16 && n.age <= 45);
            pool.Sort((a, b) => b.Overall.CompareTo(a.Overall));
            bool gk = false;
            foreach (var n in pool)
            {
                if (t.players.Count >= 11) break;
                if (n.position == Position.GK) { if (gk) continue; gk = true; }
                t.players.Add(MatchFactory.FromNPC(n, true, rng));
            }
            return Generic(t, rng, 75);
        }

        /// <summary>11人に満たなければ埋め、キーパーを決める。</summary>
        static MatchTeam Generic(MatchTeam t, SeededRandom rng, int strength)
        {
            bool hasGk = t.players.Exists(p => p.position == Position.GK);
            if (t.players.Count < 11)
            {
                var fill = MatchFactory.Generic(t.name, strength, 11, rng);
                foreach (var p in fill.players)
                {
                    if (t.players.Count >= 11) break;
                    if (p.position == Position.GK && hasGk) continue;
                    if (p.position == Position.GK) hasGk = true;
                    t.players.Add(p);
                }
            }
            if (!t.players.Exists(p => p.position == Position.GK))
            {
                MatchPlayer best = null;
                foreach (var p in t.players) if (p.npcId != -900 && p.npcId != -950 && (best == null || p.stats.goalkeeping > best.stats.goalkeeping)) best = p;
                if (best != null) { best.position = Position.GK; best.naturalSet = false; }
            }
            foreach (var p in t.players) p.RememberNaturalPosition();
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
