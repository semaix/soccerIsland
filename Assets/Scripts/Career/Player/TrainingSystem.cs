using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    public enum TrainingMenu
    {
        Shooting,    // シュート練習
        Dribbling,   // ドリブル練習
        Passing,     // パス練習
        Defense,     // 守備練習
        Weights,     // 筋トレ
        Running,     // 走り込み
        Tactics,     // 戦術研究（メンタル）
        Goalkeeping, // キーパー練習
    }

    public class TrainingResult
    {
        public bool done;
        public string refusal;
        public TrainingMenu menu;
        /// <summary>上がった能力（能力番号）。</summary>
        public List<int> statUps = new List<int>();
        public float fatigue;
        public int partnerId = -1;
        public string partnerLine;
        /// <summary>一緒に練習して感じた相手の実力（主人公の心の声）。数値は出さない。</summary>
        public List<string> impressions = new List<string>();
        public string Impression => impressions.Count > 0 ? string.Join("\n", impressions) : null;
    }

    /// <summary>
    /// 練習。1日3回まで。疲れると試合でガッツが減る。
    /// 仲の良い島民と一緒に練習すると伸びが良く、好感度も上がる。
    /// 一緒に練習すると相手の実力を肌で感じる（「あいつドリブルめちゃくちゃ上手いな」など。数値は出ない）。
    /// </summary>
    public class TrainingSystem
    {
        public int maxPerDay = 3;
        public float fatiguePerSession = 20f;
        public float recoveryPerDay = 30f;
        public float baseGain = 0.6f;

        readonly PlayerState _player;
        readonly NPCDatabase _db;
        readonly NPCKnowledgeBook _book;
        readonly SeededRandom _rng;

        public TrainingSystem(PlayerState player, NPCDatabase db, NPCKnowledgeBook book, SeededRandom rng)
        {
            _player = player;
            _db = db;
            _book = book;
            _rng = rng;
        }

        // shoot, pass, dribble, tech, def, phys, speed, stamina, mental, gk
        static int[] Primary(TrainingMenu m)
        {
            switch (m)
            {
                case TrainingMenu.Shooting: return new[] { 0, 3 };
                case TrainingMenu.Dribbling: return new[] { 2, 3 };
                case TrainingMenu.Passing: return new[] { 1, 3 };
                case TrainingMenu.Defense: return new[] { 4, 5 };
                case TrainingMenu.Weights: return new[] { 5 };
                case TrainingMenu.Running: return new[] { 6, 7 };
                case TrainingMenu.Tactics: return new[] { 8 };
                default: return new[] { 9 };
            }
        }

        public static string Name(TrainingMenu m)
        {
            switch (m)
            {
                case TrainingMenu.Shooting: return "シュート練習";
                case TrainingMenu.Dribbling: return "ドリブル練習";
                case TrainingMenu.Passing: return "パス練習";
                case TrainingMenu.Defense: return "守備練習";
                case TrainingMenu.Weights: return "筋トレ";
                case TrainingMenu.Running: return "走り込み";
                case TrainingMenu.Tactics: return "戦術研究";
                default: return "キーパー練習";
            }
        }

        public TrainingResult Train(TrainingMenu menu, int partnerId = -1)
        {
            int day = _db.currentDay;
            var r = new TrainingResult { menu = menu };
            if (_player.lastTrainingDay != day) { _player.lastTrainingDay = day; _player.trainingsToday = 0; }
            if (_player.trainingsToday >= maxPerDay) { r.refusal = "今日はもう練習できない"; return r; }
            if (_player.fatigue >= 90f) { r.refusal = "疲れすぎて体が動かない……"; return r; }

            while (_player.statProgress.Count < NPCAbilities.StatCount) _player.statProgress.Add(0f);
            NPCData partner = partnerId >= 0 ? _db.Get(partnerId) : null;
            var entry = partner != null ? _book.Get(partner.id) : null;
            if (partner != null && (entry == null || entry.affinity < 30)) { r.refusal = "まだ一緒に練習するほど仲良くない"; return r; }

            // 疲れているほど伸びにくい
            float tired = (1f - _player.fatigue / 150f) * (1f + InventorySystem.TrainingBonus(_player));
            foreach (int stat in Primary(menu))
            {
                int cur = _player.abilities[stat];
                float gain = baseGain * tired * Math.Max(0.15f, Math.Min(1f, 1f - (cur - 40) / 100f));
                if (partner != null && partner.abilities[stat] > cur) gain *= 1.5f; // うまい人と練習すると伸びる
                _player.statProgress[stat] += gain;
                while (_player.statProgress[stat] >= 1f)
                {
                    _player.statProgress[stat] -= 1f;
                    _player.abilities[stat] = NPCAbilities.Clamp(_player.abilities[stat] + 1);
                    r.statUps.Add(stat);
                }
            }

            _player.trainingsToday++;
            _player.fatigue = Math.Min(100f, _player.fatigue + fatiguePerSession * (menu == TrainingMenu.Running ? 1.25f : 1f));
            PlayerGrowth.AddExperience(_player, 5);
            r.fatigue = _player.fatigue;
            r.done = true;

            if (partner != null)
            {
                r.partnerId = partner.id;
                entry.affinity = Math.Min(100, entry.affinity + 3);
                _book.Reveal(partner.id, NPCInfoFlags.Effort | NPCInfoFlags.Position);
                int main = Primary(menu)[0];
                int noise = _rng.Range(-4, 5);
                int tier = TrainingComments.Tier(partner.abilities[main], _player.abilities[main], noise);
                r.impressions.Add(TrainingComments.Line(main, tier, partner.FullName, _rng));
                AddStandout(partner, main, r);
                r.partnerLine = TrainingComments.PartnerLine(partner, partner.abilities[main] > _player.abilities[main], _rng);
            }
            return r;
        }

        /// <summary>練習中に目についた、相手の一番の武器（または弱点）。</summary>
        void AddStandout(NPCData partner, int trained, TrainingResult r)
        {
            int best = -1, worst = -1;
            for (int i = 0; i < NPCAbilities.StatCount; i++)
            {
                if (i == trained) continue;
                if (i == 9 && partner.position != Position.GK) continue; // キーパー以外のキーパー能力は見えない
                if (best < 0 || partner.abilities[i] > partner.abilities[best]) best = i;
                if (worst < 0 || partner.abilities[i] < partner.abilities[worst]) worst = i;
            }
            if (best < 0) return;
            int bestTier = TrainingComments.Tier(partner.abilities[best], _player.abilities[best], _rng.Range(-4, 5));
            if (bestTier <= 2) r.impressions.Add(TrainingComments.Line(best, bestTier, partner.FullName, _rng));
            else if (worst >= 0 && _rng.Chance(0.5f))
                r.impressions.Add(TrainingComments.Line(worst, 4, partner.FullName, _rng));
        }

        public void TickDay()
        {
            _player.fatigue = Math.Max(0f, _player.fatigue - recoveryPerDay);
        }
    }
}
