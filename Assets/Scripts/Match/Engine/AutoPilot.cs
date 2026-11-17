using SoccerIsland.NPC;

namespace SoccerIsland.Match
{
    /// <summary>
    /// 主人公のコマンドを自動で選ぶ。「おまかせ」操作やテストに使う。
    /// 性格づけ（見せたがり・オフサイド狙い・八百長中）を切り替えられる。
    /// </summary>
    public class AutoPilot : IMatchController
    {
        /// <summary>華麗なドリブルを多用する（ファンに見せる）。</summary>
        public bool showboat;
        /// <summary>守備でオフサイドトラップを狙う。</summary>
        public bool trapper;
        /// <summary>わざと負けようとする。subtle なら目立たないように手を抜く。</summary>
        public bool throwing;
        public bool subtle = true;
        /// <summary>何もしない（消極的）。テスト用。</summary>
        public bool passive;

        readonly SeededRandom _rng;

        public AutoPilot(int seed = 7) { _rng = new SeededRandom(seed); }

        public MatchChoice Decide(MatchEngine engine, MatchPrompt p)
        {
            return p.kind == PromptKind.Attack ? Attack(p) : Defend(p);
        }

        MatchChoice Attack(MatchPrompt p)
        {
            if (passive) return Pick(p, MatchCommand.Keep, MatchCommand.BackPass);
            if (throwing)
            {
                if (!subtle && p.options.Contains(MatchCommand.MissOnPurpose)) return new MatchChoice(MatchCommand.MissOnPurpose);
                if (!subtle && _rng.Chance(0.5f)) return Pick(p, MatchCommand.SloppyPass);
                return Pick(p, MatchCommand.BackPass, MatchCommand.Keep);
            }
            if (p.zone >= 5)
            {
                if (p.options.Contains(MatchCommand.SpecialShot) && _rng.Chance(0.6f)) return new MatchChoice(MatchCommand.SpecialShot);
                return new MatchChoice(MatchCommand.Shoot);
            }
            if (p.zone == 4 && _rng.Chance(0.35f)) return new MatchChoice(MatchCommand.Shoot);
            if (showboat) return Pick(p, MatchCommand.FlairDribble);
            if (p.opponent == null) return new MatchChoice(MatchCommand.Dribble);
            float r = _rng.Value;
            if (r < 0.45f) return Pick(p, MatchCommand.Pass);
            if (r < 0.85f) return Pick(p, MatchCommand.Dribble);
            return Pick(p, MatchCommand.OneTwo, MatchCommand.Pass);
        }

        MatchChoice Defend(MatchPrompt p)
        {
            if (passive) return new MatchChoice(MatchCommand.Wait);
            if (throwing && subtle) return new MatchChoice(_rng.Chance(0.5f) ? MatchCommand.PassCut : MatchCommand.Wait);
            if (trapper && p.options.Contains(MatchCommand.OffsideTrap) && _rng.Chance(0.7f)) return new MatchChoice(MatchCommand.OffsideTrap);
            if (p.options.Contains(MatchCommand.Block) && _rng.Chance(0.4f)) return new MatchChoice(MatchCommand.Block);
            return new MatchChoice(_rng.Chance(0.6f) ? MatchCommand.Tackle : MatchCommand.PassCut);
        }

        static MatchChoice Pick(MatchPrompt p, params MatchCommand[] prefs)
        {
            foreach (var c in prefs) if (p.options.Contains(c)) return new MatchChoice(c);
            return new MatchChoice(p.options[0]);
        }
    }
}
