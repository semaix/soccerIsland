using System.Collections.Generic;
using UnityEngine;

namespace SoccerIsland.Field.UnityIntegration
{
    /// <summary>
    /// ドット絵素材（Resources/Art）を読み込んで、マップの1枚絵とキャラのスプライトを作る。
    /// 1マス＝16px＝1ユニット。キャラは島民ごとにキー色を塗り替えて作り、同じ見た目は使い回す。
    /// </summary>
    public class FieldArt
    {
        public const int PixelsPerUnit = 16;
        public const int CharW = 16, CharH = 24, CharFrames = 12;

        readonly Texture2D _tiles;
        readonly Dictionary<string, Texture2D> _parts = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, Sprite[]> _characters = new Dictionary<string, Sprite[]>();

        public FieldArt()
        {
            _tiles = Resources.Load<Texture2D>("Art/tiles");
        }

        Texture2D Part(string name)
        {
            if (!_parts.TryGetValue(name, out var t))
            {
                t = Resources.Load<Texture2D>("Art/" + name);
                _parts[name] = t;
            }
            return t;
        }

        /// <summary>マップ全体を1枚の絵にする（地面の上に飾りを重ねる）。</summary>
        public Sprite BakeMap(TownMap m)
        {
            int T = FieldTiles.Size;
            var tex = new Texture2D(m.width * T, m.height * T, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[m.width * T * m.height * T];
            var src = _tiles.GetPixels32();
            int sheetW = _tiles.width, sheetH = _tiles.height;
            for (int y = 0; y < m.height; y++)
                for (int x = 0; x < m.width; x++)
                {
                    Blit(src, sheetW, sheetH, (int)m.Ground(x, y), pixels, m.width * T, x, m.height - 1 - y, false);
                    var d = m.Deco(x, y);
                    if (d != FieldTile.None) Blit(src, sheetW, sheetH, (int)d, pixels, m.width * T, x, m.height - 1 - y, true);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0, 0), PixelsPerUnit);
        }

        /// <summary>タイルを1枚コピーする（Unity のテクスチャは左下が原点）。</summary>
        static void Blit(Color32[] src, int sheetW, int sheetH, int tile, Color32[] dst, int dstW, int tx, int ty, bool alpha)
        {
            int T = FieldTiles.Size;
            int sx = (tile % FieldTiles.SheetColumns) * T;
            int sy = sheetH - (tile / FieldTiles.SheetColumns + 1) * T;
            for (int y = 0; y < T; y++)
                for (int x = 0; x < T; x++)
                {
                    var c = src[(sy + y) * sheetW + sx + x];
                    if (alpha && c.a == 0) continue;
                    dst[(ty * T + y) * dstW + tx * T + x] = c;
                }
        }

        /// <summary>島民のスプライト12コマ（下・左・右・上 × 3コマ）。</summary>
        public Sprite[] Character(CharacterLook look)
        {
            if (_characters.TryGetValue(look.Key, out var cached)) return cached;
            int w = CharW * CharFrames;
            var pixels = new Color32[w * CharH];
            foreach (var name in look.layers)
            {
                var part = Part(name);
                if (part == null) continue;
                var src = part.GetPixels32();
                for (int i = 0; i < pixels.Length && i < src.Length; i++)
                {
                    var c = src[i];
                    if (c.a == 0) continue;
                    if (look.TryMap(new Rgb(c.r, c.g, c.b), out var mapped)) c = new Color32(mapped.r, mapped.g, mapped.b, 255);
                    pixels[i] = c;
                }
            }
            var tex = new Texture2D(w, CharH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);
            tex.Apply();
            var frames = new Sprite[CharFrames];
            for (int i = 0; i < CharFrames; i++)
                frames[i] = Sprite.Create(tex, new Rect(i * CharW, 0, CharW, CharH), new Vector2(0.5f, 0f), PixelsPerUnit);
            _characters[look.Key] = frames;
            return frames;
        }

        /// <summary>向き（0下 1左 2右 3上）とコマ（0立ち 1,2歩き）から絵の番号。</summary>
        public static int FrameIndex(int dir, int step) => dir * 3 + step;
    }
}
