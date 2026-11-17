using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    /// <summary>
    /// 月に1回の人気投票。島民はみんな芸能人のように扱われ、知名度・話題性・最近の勝利で票が集まる。
    /// 裏の人間と服役中の人は対象外。主人公も参加する。
    /// </summary>
    public class PopularityVote
    {
        public int listSize = 10;

        readonly NPCDatabase _db;
        readonly WorldState _world;
        readonly PlayerState _player;
        readonly SeededRandom _rng;

        public PopularityVote(NPCDatabase db, WorldState world, PlayerState player, SeededRandom rng)
        {
            _db = db;
            _world = world;
            _player = player;
            _rng = rng;
        }

        public List<VoteEntry> Run(MediaState media)
        {
            var wins = RecentWins(30);
            var all = new List<VoteEntry>();
            foreach (var n in _db.npcs)
            {
                if (n.isIncarcerated || n.isUnderground || n.fame < 10) continue;
                wins.TryGetValue(n.id, out int w);
                float score = n.fame + n.hype * 30f + w * 2f + _rng.Gaussian(0f, 6f);
                all.Add(new VoteEntry { npcId = n.id, name = n.FullName, votes = Votes(score) });
            }
            float ps = _player.fame + _player.form * 10f + _rng.Gaussian(0f, 6f);
            all.Add(new VoteEntry { npcId = -1, name = _player.name, votes = Votes(ps) });

            all.Sort((a, b) => b.votes != a.votes ? b.votes.CompareTo(a.votes) : a.npcId.CompareTo(b.npcId));
            media.playerVoteRank = all.FindIndex(e => e.npcId == -1) + 1;
            if (media.playerVoteRank > listSize) media.playerVoteRank = 0;
            if (all.Count > listSize) all.RemoveRange(listSize, all.Count - listSize);
            media.lastVote = all;
            media.lastVoteDay = _db.currentDay;
            return all;
        }

        static int Votes(float score) => score <= 0 ? 0 : (int)(score * score * 3f);

        Dictionary<int, int> RecentWins(int days)
        {
            var d = new Dictionary<int, int>();
            foreach (var m in _world.matches)
            {
                if (!m.played || m.day < _db.currentDay - days || m.homeScore == m.awayScore || m.IsUnderground) continue;
                foreach (var id in m.homeScore > m.awayScore ? m.homeLineup : m.awayLineup)
                    d[id] = (d.TryGetValue(id, out var c) ? c : 0) + 1;
            }
            return d;
        }
    }
}
