using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Match
{
    /// <summary>
    /// フォーメーションと作戦。
    /// ・フォーメーションで選手をポジションに割り当てる（本来のポジションでないと少し弱くなる）
    /// ・作戦で攻め方・守り方の傾向と能力補正が変わる
    /// ・作戦は性格によって好き嫌いがある（テンションが上下する）
    /// </summary>
    public static class FormationSystem
    {
        public static void Slots(Formation f, out int df, out int mf, out int fw)
        {
            switch (f)
            {
                case Formation.F433: df = 4; mf = 3; fw = 3; break;
                case Formation.F352: df = 3; mf = 5; fw = 2; break;
                case Formation.F541: df = 5; mf = 4; fw = 1; break;
                case Formation.F343: df = 3; mf = 4; fw = 3; break;
                default: df = 4; mf = 4; fw = 2; break;
            }
        }

        public static string Name(Formation f)
        {
            Slots(f, out int df, out int mf, out int fw);
            return $"{df}-{mf}-{fw}";
        }

        public static string Name(TeamTactic t)
        {
            switch (t)
            {
                case TeamTactic.Counter: return "カウンター";
                case TeamTactic.WingAttack: return "サイド攻撃";
                case TeamTactic.CentralBreak: return "中央突破";
                case TeamTactic.Possession: return "パスをつなぐ";
                case TeamTactic.HighPress: return "前線からプレス";
                case TeamTactic.ParkTheBus: return "ゴール前を固める";
                default: return "バランス";
            }
        }

        /// <summary>フォーメーションに合わせて選手を配置する（11人制のみ）。</summary>
        public static void Apply(MatchTeam t, Formation f)
        {
            t.formation = f;
            foreach (var p in t.players) p.RememberNaturalPosition();
            if (t.players.Count < 11) return;

            Slots(f, out int dfN, out int mfN, out int fwN);
            var field = new List<MatchPlayer>();
            foreach (var p in t.players) if (p.naturalPosition != Position.GK) field.Add(p); else p.position = Position.GK;

            var assigned = new HashSet<MatchPlayer>();
            var need = new Dictionary<Position, int> { { Position.DF, dfN }, { Position.MF, mfN }, { Position.FW, fwN } };

            // 1. 本来のポジションに、うまい順に入れる
            foreach (var pos in new[] { Position.FW, Position.DF, Position.MF })
            {
                var natural = field.FindAll(p => p.naturalPosition == pos);
                natural.Sort((a, b) => b.stats.Overall(pos).CompareTo(a.stats.Overall(pos)));
                foreach (var p in natural)
                {
                    if (need[pos] == 0) break;
                    p.position = pos;
                    need[pos]--;
                    assigned.Add(p);
                }
            }
            // 2. 余った選手を、空いた枠に一番合う順で入れる
            foreach (var p in field)
            {
                if (assigned.Contains(p)) continue;
                Position best = Position.MF;
                int bestScore = int.MinValue;
                foreach (var pos in new[] { Position.DF, Position.MF, Position.FW })
                {
                    if (need[pos] == 0) continue;
                    int sc = p.stats.Overall(pos);
                    if (sc > bestScore) { bestScore = sc; best = pos; }
                }
                if (bestScore == int.MinValue) { p.position = p.naturalPosition; continue; } // 枠外（12人以上）
                p.position = best;
                need[best]--;
                assigned.Add(p);
            }
        }

        /// <summary>作戦を変える。性格による反応を返す。</summary>
        public static List<MeetingReaction> SetTactic(MatchTeam t, TeamTactic tactic)
        {
            t.tactic = tactic;
            var list = new List<MeetingReaction>();
            foreach (var p in t.Active)
            {
                if (p.isProtagonist) continue;
                float d = Reaction(p, tactic);
                if (d == 0f) continue;
                p.tension = Math.Max(0f, Math.Min(100f, p.tension + d));
                list.Add(new MeetingReaction
                {
                    player = p, tensionDelta = d,
                    line = d > 0 ? "その作戦、いいね！" : p.soccerPersonality == SoccerPersonality.Egoist ? "そんな地味な作戦で勝てるかよ。" : "……気が進まない。",
                });
            }
            return list;
        }

        static float Reaction(MatchPlayer p, TeamTactic t)
        {
            switch (p.soccerPersonality)
            {
                case SoccerPersonality.Egoist:
                    return t == TeamTactic.CentralBreak || t == TeamTactic.Counter ? 8f : t == TeamTactic.Possession || t == TeamTactic.ParkTheBus ? -10f : 0f;
                case SoccerPersonality.Sense:
                case SoccerPersonality.Gifted:
                    return t == TeamTactic.CentralBreak || t == TeamTactic.Possession ? 8f : t == TeamTactic.ParkTheBus ? -12f : 0f;
                case SoccerPersonality.Fighter:
                    return t == TeamTactic.HighPress ? 12f : t == TeamTactic.Possession ? -5f : 0f;
                case SoccerPersonality.Hysteric:
                    return t == TeamTactic.ParkTheBus ? -8f : t == TeamTactic.HighPress ? 5f : 0f;
                case SoccerPersonality.MoodMaker:
                    return t == TeamTactic.WingAttack || t == TeamTactic.CentralBreak ? 5f : 0f;
                default:
                    return 0f;
            }
        }

        /// <summary>CPUチームの陣形と作戦を、選手の性格から決める。</summary>
        public static void AutoSelect(MatchTeam t)
        {
            int ego = 0, sense = 0, fighter = 0, devoted = 0, n = 0;
            foreach (var p in t.players)
            {
                n++;
                switch (p.soccerPersonality)
                {
                    case SoccerPersonality.Egoist: ego++; break;
                    case SoccerPersonality.Sense:
                    case SoccerPersonality.Gifted: sense++; break;
                    case SoccerPersonality.Fighter: fighter++; break;
                    case SoccerPersonality.Devoted: devoted++; break;
                }
            }
            if (n < 11) { t.tactic = fighter >= 2 ? TeamTactic.HighPress : TeamTactic.Balanced; return; }
            if (fighter >= 4) { Apply(t, Formation.F442); t.tactic = TeamTactic.HighPress; }
            else if (ego >= 3) { Apply(t, Formation.F343); t.tactic = TeamTactic.CentralBreak; }
            else if (sense >= 3) { Apply(t, Formation.F352); t.tactic = TeamTactic.Possession; }
            else if (devoted >= 4) { Apply(t, Formation.F541); t.tactic = TeamTactic.Counter; }
            else { Apply(t, Formation.F442); t.tactic = TeamTactic.Balanced; }
        }

        // ------------------------------------------------------------------
        // 試合中の補正
        // ------------------------------------------------------------------

        /// <summary>攻撃側の補正。zone は攻撃側から見た位置、lane は列。</summary>
        public static float AttackBonus(MatchTeam t, MatchCommand cmd, int zone, int lane)
        {
            float b = 0f;
            Slots(t.formation, out int df, out int mf, out int fw);
            if (t.players.Count >= 11)
            {
                if (cmd == MatchCommand.Shoot || cmd == MatchCommand.SpecialShot || cmd == MatchCommand.Header || cmd == MatchCommand.Volley) b += (fw - 2) * 2f;
                if (cmd == MatchCommand.Pass || cmd == MatchCommand.OneTwo || cmd == MatchCommand.LongPass) b += (mf - 4) * 2f;
            }
            switch (t.tactic)
            {
                case TeamTactic.Counter:
                    if (cmd == MatchCommand.LongPass || (cmd == MatchCommand.Pass && zone <= 3)) b += 5f;
                    break;
                case TeamTactic.WingAttack:
                    if (cmd == MatchCommand.Centering) b += 6f;
                    if (lane != 1 && (cmd == MatchCommand.Dribble || cmd == MatchCommand.FlairDribble)) b += 3f;
                    break;
                case TeamTactic.CentralBreak:
                    if (lane == 1 && (cmd == MatchCommand.Dribble || cmd == MatchCommand.FlairDribble || cmd == MatchCommand.SpecialDribble || cmd == MatchCommand.OneTwo)) b += 5f;
                    break;
                case TeamTactic.Possession:
                    if (cmd == MatchCommand.Pass || cmd == MatchCommand.OneTwo || cmd == MatchCommand.BackPass) b += 6f;
                    break;
                case TeamTactic.ParkTheBus:
                    b -= 4f; // 攻撃に人数をかけられない
                    break;
            }
            return b;
        }

        /// <summary>守備側の補正。attackZone は攻撃側から見た位置（守備側の自陣ゴールに近いほど大きい）。</summary>
        public static float DefenseBonus(MatchTeam t, MatchCommand cmd, int attackZone)
        {
            float b = 0f;
            Slots(t.formation, out int df, out int mf, out int fw);
            if (t.players.Count >= 11 && attackZone >= 4) b += (df - 4) * 2.5f;
            switch (t.tactic)
            {
                case TeamTactic.HighPress:
                    if (attackZone <= 2 && (cmd == MatchCommand.Tackle || cmd == MatchCommand.SpecialTackle || cmd == MatchCommand.PassCut)) b += 6f;
                    break;
                case TeamTactic.ParkTheBus:
                    if (attackZone >= 4) b += cmd == MatchCommand.Block || cmd == MatchCommand.SpecialBlock || cmd == MatchCommand.Clear ? 8f : 3f;
                    break;
                case TeamTactic.Counter:
                    if (attackZone >= 4 && (cmd == MatchCommand.Block || cmd == MatchCommand.SpecialBlock)) b += 4f;
                    break;
            }
            return b;
        }

        /// <summary>ガッツの消耗倍率。</summary>
        public static float GutsDrain(MatchTeam t) => t.tactic == TeamTactic.HighPress ? 1.3f : t.tactic == TeamTactic.Possession ? 0.9f : 1f;

        /// <summary>CPUのコマンド選択の傾向（dribble, pass, longPass, oneTwo, shoot, keep, centering の倍率）。</summary>
        public static float[] AttackWeights(TeamTactic t, int lane)
        {
            switch (t)
            {
                case TeamTactic.Counter: return new[] { 1f, 0.8f, 2.2f, 1f, 1.1f, 0.5f, 1f };
                case TeamTactic.WingAttack: return new[] { 1f, 1f, 1f, 0.8f, 0.9f, 1f, lane != 1 ? 2.5f : 1f };
                case TeamTactic.CentralBreak: return new[] { lane == 1 ? 1.6f : 1.1f, 0.9f, 0.6f, 1.8f, 1.1f, 1f, 0.4f };
                case TeamTactic.Possession: return new[] { 0.6f, 1.7f, 0.5f, 1.3f, 0.7f, 1.5f, 0.8f };
                case TeamTactic.HighPress: return new[] { 1.1f, 1f, 1f, 1f, 1.1f, 0.6f, 1f };
                case TeamTactic.ParkTheBus: return new[] { 0.6f, 1f, 1.5f, 0.6f, 0.5f, 2f, 0.5f };
                default: return new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f };
            }
        }
    }
}
