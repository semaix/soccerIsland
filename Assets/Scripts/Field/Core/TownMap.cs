using System;
using System.Collections.Generic;
using SoccerIsland.Career;
using SoccerIsland.NPC;

namespace SoccerIsland.Field
{
    [Serializable]
    public struct TileRect
    {
        public int x, y, w, h;
        public TileRect(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public bool Contains(int px, int py) => px >= x && py >= y && px < x + w && py < y + h;
        public int CenterX => x + w / 2;
        public int CenterY => y + h / 2;
    }

    /// <summary>マップの中の区域。中に入ると主人公の「今いる場所」がその場所になる。</summary>
    public class MapZone
    {
        public IslandPlace place;
        public string label;
        public TileRect rect;
        /// <summary>入ったときに立つ場所（ほかの街から来たとき・時間が変わったとき）。</summary>
        public int entryX, entryY;
    }

    /// <summary>マップの端の出口。踏むと隣の街へ移動する（時間が進む）。</summary>
    public class MapExit
    {
        public Town to;
        public TileRect rect;
        /// <summary>向こうの街に着いたときに立つ場所（向こうのマップの反対側の出口のそば）。</summary>
        public bool westEdge;
    }

    /// <summary>
    /// 1つの街のマップ。地面（ground）と上に置く物（deco）の2層。y=0 が画面の上。
    /// </summary>
    public class TownMap
    {
        public Town town;
        public int width, height;
        public FieldTile[] ground;
        public FieldTile[] deco;
        public bool[] solid;
        public List<MapZone> zones = new List<MapZone>();
        public List<MapExit> exits = new List<MapExit>();

        public TownMap(Town town, int w, int h)
        {
            this.town = town;
            width = w;
            height = h;
            ground = new FieldTile[w * h];
            deco = new FieldTile[w * h];
            solid = new bool[w * h];
            for (int i = 0; i < deco.Length; i++) deco[i] = FieldTile.None;
        }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
        public FieldTile Ground(int x, int y) => ground[y * width + x];
        public FieldTile Deco(int x, int y) => deco[y * width + x];
        public bool IsSolid(int x, int y) => !In(x, y) || solid[y * width + x];

        public void SetGround(int x, int y, FieldTile t)
        {
            if (!In(x, y)) return;
            ground[y * width + x] = t;
            solid[y * width + x] = FieldTiles.IsSolid(t) || (deco[y * width + x] != FieldTile.None && FieldTiles.IsSolid(deco[y * width + x]));
        }

        public void SetDeco(int x, int y, FieldTile t)
        {
            if (!In(x, y)) return;
            deco[y * width + x] = t;
            solid[y * width + x] = FieldTiles.IsSolid(ground[y * width + x]) || (t != FieldTile.None && FieldTiles.IsSolid(t));
        }

        public void Fill(TileRect r, FieldTile t)
        {
            for (int y = r.y; y < r.y + r.h; y++)
                for (int x = r.x; x < r.x + r.w; x++) SetGround(x, y, t);
        }

        /// <summary>その場所の区域（区域の外なら null）。</summary>
        public MapZone ZoneAt(int x, int y)
        {
            foreach (var z in zones) if (z.rect.Contains(x, y)) return z;
            return null;
        }

        public MapZone Zone(IslandPlace place)
        {
            foreach (var z in zones) if (z.place == place) return z;
            return null;
        }

        public MapExit ExitAt(int x, int y)
        {
            foreach (var e in exits) if (e.rect.Contains(x, y)) return e;
            return null;
        }

        /// <summary>区域の中の通れるマス（島民を立たせる場所）。</summary>
        public List<int> WalkableCells(MapZone z)
        {
            var list = new List<int>();
            for (int y = z.rect.y; y < z.rect.y + z.rect.h; y++)
                for (int x = z.rect.x; x < z.rect.x + z.rect.w; x++)
                    if (!IsSolid(x, y)) list.Add(y * width + x);
            return list;
        }
    }
}
