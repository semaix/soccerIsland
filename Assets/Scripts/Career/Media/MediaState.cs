using System;
using System.Collections.Generic;

namespace SoccerIsland.Career
{
    public enum ArticleCategory
    {
        Match,    // 試合結果
        Transfer, // 移籍
        Market,   // 市場価値
        Ranking,  // 協会ランキング
        Gossip,   // ゴシップ
        Vote,     // 人気投票
        Lottery,  // 宝くじ
        Stock,    // 株
        Shop,     // お店
        Player,   // 主人公の話題
        Island,   // 島のニュース（覚醒・出所など）
    }

    [Serializable]
    public class Article
    {
        public ArticleCategory category;
        public string title;
        public string body;
        /// <summary>大きいほど一面に近い。</summary>
        public int priority;
        public List<int> npcIds = new List<int>();
        /// <summary>主人公についての記事か。</summary>
        public bool aboutPlayer;
    }

    /// <summary>サッカー新聞（朝刊）。前日の出来事を載せる。</summary>
    [Serializable]
    public class NewspaperIssue
    {
        public int day;
        public int year;
        public List<Article> articles = new List<Article>();

        public Article Headline => articles.Count > 0 ? articles[0] : null;
    }

    [Serializable]
    public class VoteEntry
    {
        /// <summary>-1 は主人公。</summary>
        public int npcId;
        public string name;
        public int votes;
    }

    [Serializable]
    public class MediaState
    {
        public List<NewspaperIssue> issues = new List<NewspaperIssue>();
        public List<VoteEntry> lastVote = new List<VoteEntry>();
        public int lastVoteDay = -1;
        /// <summary>主人公の最新の人気投票順位（圏外は0）。</summary>
        public int playerVoteRank;

        public const int MaxIssues = 60;

        public NewspaperIssue Latest => issues.Count > 0 ? issues[issues.Count - 1] : null;
    }
}
