using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    /// <summary>
    /// 街ごとの主人公の評判（0〜100）と、街のライバル意識。
    /// ・住んでいる街のライバルの街の人は、最初は打ち解けにくい
    /// ・天峰台の人は、有名にならないと相手にしてくれない（島1番のプライド）
    /// ・地域対抗戦で勝つと自分の街の評判が上がり、負かした街の評判は下がる
    /// ・評判が高い街ほどファンに声をかけられる
    /// </summary>
    public class TownReputation
    {
        readonly PlayerState _player;
        readonly IslandEventsState _state;

        public TownReputation(PlayerState player, IslandEventsState state)
        {
            _player = player;
            _state = state;
            int towns = Enum.GetValues(typeof(Town)).Length;
            while (_state.townReputation.Count < towns) _state.townReputation.Add(50);
        }

        public int Of(Town t) => _state.townReputation[(int)t];

        public void Change(Town t, int delta)
        {
            _state.townReputation[(int)t] = Math.Max(0, Math.Min(100, Of(t) + delta));
        }

        /// <summary>会話の好感度の上がり方を、街の関係と評判で補正する。</summary>
        public int ModifyAffinityGain(NPCData npc, int gain)
        {
            float g = gain;
            int rel = TownRivalry.Between(_player.homeTown, npc.hometown);
            if (rel <= -40) g *= 0.6f;          // ライバルの街の人
            else if (rel >= 30) g *= 1.2f;      // 仲の良い街の人
            if (npc.hometown == Town.Tenpou && _player.homeTown != Town.Tenpou && _player.fame < 40f) g *= 0.5f; // 有名でないと相手にしない
            g *= 0.6f + Of(npc.hometown) / 125f; // 評判 0 → 0.6倍、100 → 1.4倍
            int result = (int)Math.Round(g);
            return gain > 0 ? Math.Max(1, result) : result;
        }

        /// <summary>ファンに声をかけられやすさの倍率。</summary>
        public float FanMultiplier(Town t) => 0.5f + Of(t) / 100f;

        /// <summary>地域対抗戦の結果を反映する。</summary>
        public void RecordDerby(Town mine, Town theirs, bool won, bool lost)
        {
            if (won) { Change(mine, +6); Change(theirs, -4); }
            else if (lost) { Change(mine, -3); Change(theirs, +2); }
        }
    }
}
