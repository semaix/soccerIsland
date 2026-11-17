using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerIsland.Field.UnityIntegration
{
    /// <summary>レトロなウィンドウの描画と、キー入力のまとめ。</summary>
    public static class RetroUI
    {
        static Texture2D _white;

        public static int Scale => Mathf.Max(1, Screen.height / 360);

        public static bool Ok => Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
        public static bool Cancel => Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape);
        public static bool Up => Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        public static bool Down => Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);

        static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _white.SetPixels32(new[] { new Color32(255, 255, 255, 255) });
                    _white.Apply();
                }
                return _white;
            }
        }

        /// <summary>濃い紺の地に白い縁取りのウィンドウ。</summary>
        public static void Window(Rect r)
        {
            int s = Scale;
            var old = GUI.color;
            GUI.color = new Color(0.92f, 0.92f, 0.95f);
            GUI.DrawTexture(r, White);
            GUI.color = new Color(0.08f, 0.1f, 0.25f, 0.96f);
            GUI.DrawTexture(new Rect(r.x + 2 * s, r.y + 2 * s, r.width - 4 * s, r.height - 4 * s), White);
            GUI.color = old;
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        public static GUIStyle Text(int size = 11, bool bold = false)
        {
            var st = new GUIStyle(GUI.skin.label) { fontSize = size * Scale, wordWrap = true, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
            st.normal.textColor = Color.white;
            return st;
        }

        public static GUIStyle Dim(int size = 9)
        {
            var st = Text(size);
            st.normal.textColor = new Color(0.7f, 0.75f, 0.9f);
            return st;
        }
    }

    /// <summary>
    /// 何段でも重ねられるメニュー（FieldSession の MenuPage を描く）。
    /// 上下で選んで Z で決定、X で1つ戻る。行はクリックでも選べる。
    /// </summary>
    public class RetroMenuView
    {
        class Frame
        {
            public MenuPage page;
            public int cursor;
            public int scroll;
        }

        readonly List<Frame> _stack = new List<Frame>();
        public int visibleRows = 10;

        public bool IsOpen => _stack.Count > 0;
        public void Push(MenuPage page) { if (page != null) _stack.Add(new Frame { page = page }); }
        public void Pop() { if (_stack.Count > 0) _stack.RemoveAt(_stack.Count - 1); }
        public void Clear() => _stack.Clear();

        Frame Top => _stack[_stack.Count - 1];

        /// <summary>入力を処理する。項目が選ばれたらその結果を返す。</summary>
        public MenuResult HandleInput()
        {
            if (!IsOpen) return null;
            var f = Top;
            if (RetroUI.Cancel) { Pop(); return null; }
            if (f.page.items == null)
            {
                if (RetroUI.Up) f.scroll = Math.Max(0, f.scroll - 1);
                if (RetroUI.Down) f.scroll = Math.Min(Math.Max(0, f.page.text.Count - visibleRows), f.scroll + 1);
                if (RetroUI.Ok) Pop();
                return null;
            }
            var items = f.page.items();
            if (items.Count == 0) return null;
            if (RetroUI.Up) f.cursor = (f.cursor + items.Count - 1) % items.Count;
            if (RetroUI.Down) f.cursor = (f.cursor + 1) % items.Count;
            f.cursor = Math.Min(f.cursor, items.Count - 1);
            if (f.cursor < f.scroll) f.scroll = f.cursor;
            if (f.cursor >= f.scroll + visibleRows) f.scroll = f.cursor - visibleRows + 1;
            if (RetroUI.Ok) return Select(items[f.cursor]);
            return _clicked;
        }

        MenuResult _clicked;

        static MenuResult Select(MenuItem item) => item.enabled && item.select != null ? item.select() : null;

        public void Draw()
        {
            _clicked = null;
            if (!IsOpen) return;
            int s = RetroUI.Scale;
            var f = Top;
            float w = Math.Min(Screen.width - 24 * s, 300 * s);
            float rowH = 14 * s;
            int rows = f.page.items == null ? Math.Min(visibleRows, f.page.text.Count) : Math.Min(visibleRows, f.page.items().Count);
            float h = (rows + 2) * rowH + 16 * s;
            var r = new Rect(Screen.width - w - 12 * s, 24 * s, w, h);
            RetroUI.Window(r);
            GUI.Label(new Rect(r.x + 10 * s, r.y + 6 * s, w, rowH), f.page.title, RetroUI.Text(11, true));

            float y = r.y + 6 * s + rowH + 4 * s;
            if (f.page.items == null)
            {
                for (int i = f.scroll; i < f.page.text.Count && i < f.scroll + visibleRows; i++, y += rowH)
                    GUI.Label(new Rect(r.x + 10 * s, y, w - 20 * s, rowH), f.page.text[i], RetroUI.Text(10));
                if (f.page.text.Count > visibleRows) GUI.Label(new Rect(r.x + w - 30 * s, r.y + h - 16 * s, 30 * s, rowH), "▲▼", RetroUI.Dim());
                return;
            }

            var items = f.page.items();
            string detail = null;
            for (int i = f.scroll; i < items.Count && i < f.scroll + visibleRows; i++, y += rowH)
            {
                var it = items[i];
                var st = it.enabled ? RetroUI.Text(10) : RetroUI.Dim(10);
                var row = new Rect(r.x + 10 * s, y, w - 20 * s, rowH);
                GUI.Label(row, (i == f.cursor ? "▶ " : "　") + it.label, st);
                if (GUI.Button(row, "", GUIStyle.none)) { f.cursor = i; _clicked = Select(it); }
                if (i == f.cursor) detail = it.detail;
            }
            if (items.Count > visibleRows) GUI.Label(new Rect(r.x + w - 30 * s, r.y + h - 16 * s, 30 * s, rowH), "▲▼", RetroUI.Dim());
            if (!string.IsNullOrEmpty(detail))
            {
                var dr = new Rect(r.x, r.y + h + 4 * s, w, 34 * s);
                RetroUI.Window(dr);
                GUI.Label(new Rect(dr.x + 10 * s, dr.y + 6 * s, w - 20 * s, 26 * s), detail, RetroUI.Text(9));
            }
        }
    }
}
