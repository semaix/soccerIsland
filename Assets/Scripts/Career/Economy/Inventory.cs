using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    [Serializable]
    public class InventoryItem
    {
        public string name;
        public int count;
    }

    /// <summary>次の試合で効く効果。</summary>
    public enum MatchBuff
    {
        Hype,       // テンションが上がる（サッカーハイボール）
        Calm,       // チームのヒステリーが落ち着く（ノイローゼ注射）
        Warded,     // 威圧・審判買収が効かない（魔除けタバコ）
        Intimidate, // 相手を威圧する（脅迫タピオカ）
        Headband,   // 気合が入る（大漁ハチマキ）
    }

    public enum ItemUse
    {
        Consumable, // 使うとなくなる
        Equipment,  // 身につける（スパイクなど。能力が上がる）
        Fashion,    // 身につける（ファンに声をかけられやすくなる）
        Display,    // 飾る（一度だけ知名度が上がる）
    }

    public class ItemEffect
    {
        public ItemUse use;
        public MatchBuff? buff;
        public float fatigue;     // 疲労の増減（マイナスで回復）
        public int rejuvenate;    // 若返る年数
        public int statIndex = -1;// 装備で上がる能力
        public int statBonus;
        public float fame;        // 飾ったときの知名度
        public float fanBonus;    // ファッションのファン増加
        public float trainingBonus;
        public string description;
    }

    /// <summary>お店の商品の効果。商品名で引く。</summary>
    public static class ItemEffects
    {
        static readonly Dictionary<string, ItemEffect> _table = new Dictionary<string, ItemEffect>
        {
            { "サッカーハイボール", new ItemEffect { use = ItemUse.Consumable, buff = MatchBuff.Hype, fatigue = 5f, description = "次の試合でテンションが上がる。ちょっと疲れる。" } },
            { "魔除けタバコ", new ItemEffect { use = ItemUse.Consumable, buff = MatchBuff.Warded, description = "次の試合、相手の威圧と審判買収が効かない。" } },
            { "ノイローゼ注射", new ItemEffect { use = ItemUse.Consumable, buff = MatchBuff.Calm, description = "次の試合、チームのヒステリーが落ち着く。" } },
            { "若返りフレーク", new ItemEffect { use = ItemUse.Consumable, rejuvenate = 1, fatigue = -100f, description = "1歳若返る（1年に1回まで）。疲れも取れる。" } },
            { "若返りフレーク（原液）", new ItemEffect { use = ItemUse.Consumable, rejuvenate = 2, fatigue = -100f, description = "2歳若返る（1年に1回まで）。" } },
            { "脅迫タピオカ（裏ルート）", new ItemEffect { use = ItemUse.Consumable, buff = MatchBuff.Intimidate, description = "次の試合、相手を威圧する。" } },
            { "おにぎり", new ItemEffect { use = ItemUse.Consumable, fatigue = -10f, description = "疲れが少し取れる。" } },
            { "スポーツドリンク", new ItemEffect { use = ItemUse.Consumable, fatigue = -15f, description = "疲れが取れる。" } },
            { "大漁ハチマキ", new ItemEffect { use = ItemUse.Consumable, buff = MatchBuff.Headband, description = "次の試合、気合が入る。" } },
            { "限定スパイク「流星」", new ItemEffect { use = ItemUse.Equipment, statIndex = 2, statBonus = 4, description = "ドリブル+4（装備）。" } },
            { "闇スパイク", new ItemEffect { use = ItemUse.Equipment, statIndex = 0, statBonus = 5, description = "シュート+5（装備）。" } },
            { "手作りすね当て", new ItemEffect { use = ItemUse.Equipment, statIndex = 5, statBonus = 2, description = "フィジカル+2（装備）。" } },
            { "最新トレーニングシューズ", new ItemEffect { use = ItemUse.Equipment, trainingBonus = 0.2f, description = "練習の伸びが2割増し（装備）。" } },
            { "島ブランドのサングラス", new ItemEffect { use = ItemUse.Fashion, fanBonus = 0.25f, description = "垢抜けて見える。煌浜で流行中。" } },
            { "ヤンキー刺繍の特攻ジャージ", new ItemEffect { use = ItemUse.Fashion, fanBonus = 0.1f, description = "古鐘町の子供たちに大人気。" } },
            { "スタミナ弁当", new ItemEffect { use = ItemUse.Consumable, fatigue = -25f, description = "疲れがかなり取れる。" } },
            { "エナジーゼリー", new ItemEffect { use = ItemUse.Consumable, fatigue = -12f, description = "疲れが取れる。" } },
            { "ブランドジャージ", new ItemEffect { use = ItemUse.Fashion, fanBonus = 0.2f, description = "ファンに声をかけられやすくなる。" } },
            { "使用済みポテチ（伝説の選手が食べた）", new ItemEffect { use = ItemUse.Display, fame = 2f, description = "飾ると運気が上がる気がする。" } },
            { "伝説のユニフォーム（サイン入り）", new ItemEffect { use = ItemUse.Display, fame = 4f, description = "飾ると島中の話題に。" } },
            { "裏toto 必勝メモ", new ItemEffect { use = ItemUse.Display, fame = 0f, description = "……書いてあることは、だいたい外れる。" } },
        };

        public static ItemEffect Of(string name) => name != null && _table.TryGetValue(name, out var e) ? e : null;

        public static string BuffName(MatchBuff b)
        {
            switch (b)
            {
                case MatchBuff.Hype: return "ハイボールで上機嫌";
                case MatchBuff.Calm: return "チームが落ち着いている";
                case MatchBuff.Warded: return "魔除け";
                case MatchBuff.Intimidate: return "威圧（脅迫タピオカ）";
                default: return "大漁ハチマキ";
            }
        }
    }

    public class UseResult
    {
        public bool ok;
        public string message;
    }

    /// <summary>主人公の買い物と持ち物。</summary>
    public class InventorySystem
    {
        public const int MaxEquipped = 3;

        readonly PlayerState _player;
        readonly WorldState _world;
        readonly FameSystem _fame;
        readonly Func<int> _today;

        public InventorySystem(PlayerState player, WorldState world, FameSystem fame, Func<int> today)
        {
            _player = player;
            _world = world;
            _fame = fame;
            _today = today;
        }

        public int Count(string name)
        {
            foreach (var i in _player.inventory) if (i.name == name) return i.count;
            return 0;
        }

        public void Add(string name, int n = 1)
        {
            foreach (var i in _player.inventory) if (i.name == name) { i.count += n; return; }
            _player.inventory.Add(new InventoryItem { name = name, count = n });
        }

        bool Remove(string name)
        {
            for (int k = 0; k < _player.inventory.Count; k++)
            {
                var i = _player.inventory[k];
                if (i.name != name) continue;
                i.count--;
                if (i.count <= 0) _player.inventory.RemoveAt(k);
                return true;
            }
            return false;
        }

        /// <summary>表のお店で買う（裏市場の品は BlackMarket.Buy）。最後の1個なら完売になる。</summary>
        public bool Buy(int itemId)
        {
            var item = _world.GetItem(itemId);
            if (item == null || item.isUnderground || item.SurfaceStatus != ItemStatus.OnSale || item.stock <= 0) return false;
            if (!_player.TrySpend(item.price, "買い物: " + item.name, _today())) return false;
            item.stock--;
            if (item.stock <= 0) { item.status = ItemStatus.SoldOut; item.buyerId = -1; }
            Add(item.name);
            return true;
        }

        public UseResult Use(string name)
        {
            var e = ItemEffects.Of(name);
            if (e == null || Count(name) <= 0) return new UseResult { message = "使えない" };
            switch (e.use)
            {
                case ItemUse.Equipment:
                case ItemUse.Fashion:
                    if (_player.equipped.Contains(name)) { _player.equipped.Remove(name); return new UseResult { ok = true, message = name + "を外した" }; }
                    if (_player.equipped.Count >= MaxEquipped) return new UseResult { message = "これ以上身につけられない" };
                    _player.equipped.Add(name);
                    return new UseResult { ok = true, message = name + "を身につけた" };

                case ItemUse.Display:
                    if (_player.displayed.Contains(name)) return new UseResult { message = "もう飾ってある" };
                    _player.displayed.Add(name);
                    if (e.fame > 0f) _fame.AddFame(e.fame);
                    return new UseResult { ok = true, message = name + "を部屋に飾った" };

                default:
                    if (e.rejuvenate > 0)
                    {
                        if (_today() - _player.lastRejuvenateDay < 365) return new UseResult { message = "今年はもう若返った" };
                        _player.age = Math.Max(16, _player.age - e.rejuvenate);
                        _player.lastRejuvenateDay = _today();
                    }
                    if (e.fatigue != 0f) _player.fatigue = Math.Max(0f, Math.Min(100f, _player.fatigue + e.fatigue));
                    if (e.buff.HasValue && !_player.matchBuffs.Contains(e.buff.Value)) _player.matchBuffs.Add(e.buff.Value);
                    Remove(name);
                    return new UseResult { ok = true, message = e.description };
            }
        }

        /// <summary>装備品で上がる能力。</summary>
        public static int EquipmentBonus(PlayerState p, int statIndex)
        {
            int b = 0;
            foreach (var name in p.equipped)
            {
                var e = ItemEffects.Of(name);
                if (e != null && e.use == ItemUse.Equipment && e.statIndex == statIndex) b += e.statBonus;
            }
            return b;
        }

        public static float TrainingBonus(PlayerState p)
        {
            float b = 0f;
            foreach (var name in p.equipped) { var e = ItemEffects.Of(name); if (e != null) b += e.trainingBonus; }
            return b;
        }

        public static float FanBonus(PlayerState p)
        {
            float b = 0f;
            foreach (var name in p.equipped) { var e = ItemEffects.Of(name); if (e != null && e.use == ItemUse.Fashion) b += e.fanBonus; }
            return b;
        }
    }
}
