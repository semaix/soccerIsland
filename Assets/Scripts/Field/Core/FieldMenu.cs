using System;
using System.Collections.Generic;
using SoccerIsland.Match;

namespace SoccerIsland.Field
{
    /// <summary>メニューの1行。</summary>
    public class MenuItem
    {
        public string label;
        /// <summary>右側や下に出す補足（値段など）。</summary>
        public string detail;
        public bool enabled = true;
        public Func<MenuResult> select;

        public MenuItem(string label, Func<MenuResult> select, string detail = null, bool enabled = true)
        {
            this.label = label; this.select = select; this.detail = detail; this.enabled = enabled;
        }
    }

    /// <summary>メニューの1画面。items があれば選択肢、なければ text を読むだけの画面。</summary>
    public class MenuPage
    {
        public string title;
        public Func<List<MenuItem>> items;
        public List<string> text = new List<string>();

        public static MenuPage List(string title, Func<List<MenuItem>> items) => new MenuPage { title = title, items = items };
        public static MenuPage Text(string title, List<string> text) => new MenuPage { title = title, text = text };
    }

    /// <summary>選んだ結果。メッセージを出す・次の画面を開く・閉じる・試合を始める。</summary>
    public class MenuResult
    {
        public List<string> messages = new List<string>();
        public MenuPage open;
        public bool close;
        /// <summary>今の画面を1つ戻る。</summary>
        public bool back;
        /// <summary>試合を始める（MatchScreen を開く）。</summary>
        public MatchSession match;
        /// <summary>サイン・ゴールパフォーマンスの作成画面を開く。</summary>
        public bool openCreator;
        /// <summary>日付や時間が変わった（マップの島民を並べ直す）。</summary>
        public bool timeChanged;

        public static MenuResult Say(params string[] lines) { var r = new MenuResult(); r.messages.AddRange(lines); return r; }
        public static MenuResult Open(MenuPage p) => new MenuResult { open = p };
        public static MenuResult Close(params string[] lines) { var r = Say(lines); r.close = true; return r; }
    }
}
