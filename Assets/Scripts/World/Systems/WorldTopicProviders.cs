using SoccerIsland.NPC;

namespace SoccerIsland.World
{
    /// <summary>試合の話。話したNPCは必ずその試合の出場メンバー。</summary>
    public class MatchTopicProvider : IConversationTopicProvider
    {
        public int lookAheadDays = 7;
        /// <summary>裏フットサルの話をしてくれる好感度。</summary>
        public int undergroundAffinity = 60;

        readonly WorldState _state;

        public MatchTopicProvider(WorldState state) { _state = state; }

        public bool TryGetTopic(NPCData npc, TopicContext ctx, out TopicMention mention)
        {
            mention = default;
            var m = _state.NextMatchOf(npc.id, ctx.day, lookAheadDays);
            if (m == null) return false;
            if (m.IsUnderground && ctx.affinity < undergroundAffinity) return false;

            mention = new TopicMention { kind = TopicKind.Match, refId = m.id, line = Line(npc, m, m.day - ctx.day) };
            return true;
        }

        static string When(int daysLeft) => daysLeft == 0 ? "今日" : daysLeft == 1 ? "明日" : $"{daysLeft}日後";

        static string Line(NPCData npc, Match m, int daysLeft)
        {
            string when = When(daysLeft);
            string opp = m.OpponentOf(npc.id);
            switch (m.category)
            {
                case MatchCategory.UndergroundFutsal:
                    return $"……{when}の深夜、{m.venue}で{opp}とやる。誰にも言うなよ。";
                case MatchCategory.PrisonLeague:
                    return $"{when}、運動場で{opp}との棟対抗戦がある。ここじゃ数少ない楽しみさ。";
                case MatchCategory.RegionalDerby:
                    return $"{when}は{opp}との地域対抗戦！ 絶対に負けられない。";
                case MatchCategory.IslandSelect:
                    return $"{when}、島代表として{opp}と戦うんだ。見に来てくれよ。";
                case MatchCategory.Beach:
                    return $"{when}、{m.venue}でビーチサッカー！ 砂の上は別競技だよ。";
                default:
                    return $"{when}、{m.venue}で{opp}と試合なんだ。よかったら見に来て！";
            }
        }
    }

    /// <summary>買い物の話。話した商品は必ず完売または商談中になっている。</summary>
    public class PurchaseTopicProvider : IConversationTopicProvider
    {
        public int recentDays = 10;
        public int undergroundAffinity = 60;

        readonly WorldState _state;

        public PurchaseTopicProvider(WorldState state) { _state = state; }

        public bool TryGetTopic(NPCData npc, TopicContext ctx, out TopicMention mention)
        {
            mention = default;
            for (int i = _state.purchases.Count - 1; i >= 0; i--)
            {
                var p = _state.purchases[i];
                if (p.npcId != npc.id || p.mentioned || p.day < ctx.day - recentDays) continue;
                var item = _state.GetItem(p.itemId);
                if (item == null) continue;
                if (item.isUnderground && ctx.affinity < undergroundAffinity) continue;

                string line;
                if (!p.completed && item.status == ItemStatus.Negotiating && item.buyerId == npc.id)
                    line = item.isUnderground
                        ? $"……裏市場の{item.name}、今こっそり商談中なんだ。"
                        : $"{item.shopName}の{item.name}、今商談中なんだよね。決まったら自慢させて！";
                else if (p.completed && item.status == ItemStatus.SoldOut && item.buyerId == npc.id)
                    line = item.isUnderground
                        ? $"……{item.name}、裏市場で手に入れた。最後のひとつだったぜ。"
                        : $"{item.shopName}で{item.name}買ったんだ！ 最後の1個だったよ。";
                else if (p.completed && item.status == ItemStatus.SoldOut)
                    line = item.isUnderground
                        ? $"……{item.name}、裏市場で買えた。もう残ってないらしい。"
                        : $"{item.shopName}で{item.name}買ったんだ！ もう完売したらしいよ。";
                else
                    continue; // 再入荷などで「完売・商談中」でなくなった話はしない

                p.mentioned = true;
                mention = new TopicMention { kind = TopicKind.Purchase, refId = item.id, line = line };
                return true;
            }
            return false;
        }
    }
}
