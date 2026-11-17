using SoccerIsland.NPC;

namespace SoccerIsland.Match
{
    /// <summary>
    /// 自チームのコマンドを自動で選ぶ。「おまかせ」操作やテストに使う。
    /// 性格づけ（見せたがり・オフサイド狙い・八百長中・消極的）を切り替えられる。
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
            switch (p.kind)
            {
                case PromptKind.Attack: return Attack(p);
                case PromptKind.Defend: return Defend(p);
                case PromptKind.Keeper:
                    if (throwing) return new MatchChoice(MatchCommand.Catch);
                    if (p.options.Contains(MatchCommand.SpecialSave) && _rng.Chance(0.5f)) return new MatchChoice(MatchCommand.SpecialSave);
                    return new MatchChoice(_rng.Chance(0.5f) ? MatchCommand.Catch : MatchCommand.Punch);
                case PromptKind.Aerial:
                    if (passive || throwing) return new MatchChoice(MatchCommand.Trap);
                    return new MatchChoice(_rng.Chance(0.5f) ? MatchCommand.Header : _rng.Chance(0.5f) ? MatchCommand.Volley : MatchCommand.Trap);
                default:
                    return new MatchChoice(_rng.Chance(0.6f) ? MatchCommand.Clear : MatchCommand.Compete);
            }
        }

        MatchChoice Attack(MatchPrompt p)
        {
            bool me = p.actor.isProtagonist;
            if (passive && me) return Pick(p, MatchCommand.Keep, MatchCommand.BackPass);
            if (throwing)
            {
                if (me && !subtle && p.options.Contains(MatchCommand.MissOnPurpose)) return new MatchChoice(MatchCommand.MissOnPurpose);
                if (me && !subtle && _rng.Chance(0.5f)) return Pick(p, MatchCommand.SloppyPass);
                return Pick(p, MatchCommand.BackPass, MatchCommand.Keep);
            }
            if (p.zone >= 5)
            {
                if (p.options.Contains(MatchCommand.SpecialShot) && _rng.Chance(0.6f)) return new MatchChoice(MatchCommand.SpecialShot);
                if (p.options.Contains(MatchCommand.Centering) && !me && _rng.Chance(0.3f)) return new MatchChoice(MatchCommand.Centering);
                return new MatchChoice(MatchCommand.Shoot);
            }
            if (p.zone == 4 && _rng.Chance(0.35f)) return new MatchChoice(MatchCommand.Shoot);
            if (showboat && me) return Pick(p, MatchCommand.FlairDribble);
            if (p.opponent == null) return new MatchChoice(MatchCommand.Dribble);
            if (p.options.Contains(MatchCommand.SpecialDribble) && _rng.Chance(0.3f)) return new MatchChoice(MatchCommand.SpecialDribble);
            float r = _rng.Value;
            if (r < 0.45f) return Pick(p, MatchCommand.Pass);
            if (r < 0.85f) return Pick(p, MatchCommand.Dribble);
            return Pick(p, MatchCommand.OneTwo, MatchCommand.Pass);
        }

        MatchChoice Defend(MatchPrompt p)
        {
            bool me = p.actor.isProtagonist;
            if (passive && me) return new MatchChoice(MatchCommand.Wait);
            if (throwing && subtle) return new MatchChoice(_rng.Chance(0.5f) ? MatchCommand.PassCut : MatchCommand.Wait);
            if (trapper && p.options.Contains(MatchCommand.OffsideTrap) && _rng.Chance(0.7f)) return new MatchChoice(MatchCommand.OffsideTrap);
            if (p.options.Contains(MatchCommand.SpecialTackle) && _rng.Chance(0.3f)) return new MatchChoice(MatchCommand.SpecialTackle);
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
