using System;
using System.Collections.Generic;
using SoccerIsland.Career;
using SoccerIsland.NPC;

namespace SoccerIsland.Field
{
    /// <summary>
    /// 街のマップを作る（同じ街・同じシードなら毎回同じ）。48×36 マス。
    ///
    ///   北：森と岩場            西端・東端：隣の街への出口（大通りの両端）
    ///   中央北：街なか（建物と広場）   中央東：クラブのグラウンド（サッカーコート）
    ///   西：自宅（住んでいる街だけ）   南：浜辺と海
    ///   街ごとの名所：勇心町＝病院、古鐘町＝刑務所と畑、煌浜＝飲み屋街と屋台、天峰台＝オフィス街
    /// </summary>
    public static class TownMapGenerator
    {
        public const int Width = 48;
        public const int Height = 36;

        const int RoadY = 18;          // 大通り（2マス幅）
        const int BeachY = 28;         // ここから下が浜辺
        const int SeaY = 31;           // ここから下が海

        static readonly Town[] Ring = { Town.Yushin, Town.Furugane, Town.Kirahama, Town.Tenpou };

        public static Town WestNeighbor(Town t) => Ring[(Array.IndexOf(Ring, t) + Ring.Length - 1) % Ring.Length];
        public static Town EastNeighbor(Town t) => Ring[(Array.IndexOf(Ring, t) + 1) % Ring.Length];

        public static TownMap Generate(Town town, Town playerHome, int seed)
        {
            var rng = new SeededRandom(seed * 31 + (int)town * 7919 + 17);
            var m = new TownMap(town, Width, Height);

            // ---- 地面：草原 ----
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    float r = rng.Value;
                    m.SetGround(x, y, r < 0.08f ? FieldTile.Grass2 : r < 0.11f ? FieldTile.Flowers : FieldTile.Grass);
                }

            // ---- 北の森と岩場 ----
            for (int x = 0; x < Width; x++)
            {
                int depth = 2 + (int)(rng.Value * 2);
                for (int y = 0; y < depth; y++) m.SetGround(x, y, y == 0 || rng.Chance(0.3f) ? FieldTile.Rock : FieldTile.Tree);
            }

            // ---- 南の浜辺と海 ----
            for (int x = 0; x < Width; x++)
            {
                int wave = (int)Math.Round(Math.Sin(x * 0.35 + seed) * 0.8);
                for (int y = BeachY + wave; y < Height; y++)
                {
                    int sea = SeaY + wave;
                    m.SetGround(x, y, y < sea ? FieldTile.Sand : y == sea ? FieldTile.Shore : ((x + y) % 2 == 0 ? FieldTile.Water : FieldTile.Water2));
                }
            }

            // ---- 大通り（西端〜東端）と出口 ----
            var roadTile = town == Town.Tenpou || town == Town.Kirahama ? FieldTile.Asphalt : FieldTile.Path;
            for (int x = 0; x < Width; x++)
            {
                m.SetGround(x, RoadY, roadTile);
                m.SetGround(x, RoadY + 1, roadTile == FieldTile.Asphalt && x % 4 < 2 ? FieldTile.RoadLine : roadTile);
            }
            m.exits.Add(new MapExit { to = WestNeighbor(town), rect = new TileRect(0, RoadY, 1, 2), westEdge = true });
            m.exits.Add(new MapExit { to = EastNeighbor(town), rect = new TileRect(Width - 1, RoadY, 1, 2), westEdge = false });
            // 浜辺へ下りる道
            for (int y = RoadY + 2; y < BeachY; y++) m.SetGround(24, y, FieldTile.Path);

            // ---- 街なか ----
            var down = new TileRect(12, 4, 20, 13);
            BuildDowntown(m, town, down, rng);
            AddZone(m, IslandPlace.Workplace, "街なか", down, down.CenterX, RoadY - 1);

            // ---- グラウンド ----
            var ground = new TileRect(33, 5, 14, 12);
            BuildPitch(m, ground);
            AddZone(m, IslandPlace.ClubGround, "グラウンド", ground, ground.CenterX, ground.y + ground.h);

            // ---- 浜辺 ----
            var beach = new TileRect(0, BeachY - 1, Width, SeaY - BeachY + 2);
            AddZone(m, IslandPlace.Beach, "浜辺", beach, 24, BeachY);
            // 桟橋
            for (int y = SeaY - 1; y < Height; y++) m.SetGround(30, y, FieldTile.Pier);

            // ---- 自宅（住んでいる街だけ） ----
            if (town == playerHome)
            {
                var home = new TileRect(2, 9, 8, 8);
                BuildHouse(m, new TileRect(3, 10, 6, 5), FieldTile.RoofBrown, rng, doorX: 5);
                for (int y = 15; y < RoadY; y++) m.SetGround(5, y, FieldTile.Path);
                m.SetDeco(8, 15, FieldTile.Flowers);
                AddZone(m, IslandPlace.Home, "自宅", home, 5, 16);
            }

            // ---- 街の名所 ----
            switch (town)
            {
                case Town.Yushin:
                    var hosp = new TileRect(2, 21, 10, 6);
                    BuildHouse(m, new TileRect(3, 21, 8, 4), FieldTile.RoofWhite, rng, doorX: 7, sign: FieldTile.CrossSign);
                    AddZone(m, IslandPlace.Hospital, "勇心病院", hosp, 7, 26);
                    break;
                case Town.Furugane:
                    var prison = new TileRect(36, 20, 11, 8);
                    BuildPrison(m, prison);
                    AddZone(m, IslandPlace.Prison, "島刑務所", prison, 41, 27);
                    for (int y = 21; y < 26; y++) for (int x = 2; x < 11; x++) m.SetGround(x, y, FieldTile.Crop); // 畑
                    break;
                case Town.Kirahama:
                    var bars = new TileRect(2, 20, 20, 8);
                    BuildBarStreet(m, bars, rng);
                    AddZone(m, IslandPlace.Bar, "飲み屋街", bars, 12, 26); // 提灯（24段目）の1段下
                    break;
                case Town.Tenpou:
                    // オフィス街（街なかの建物を高く・灰色に）
                    for (int x = 2; x < 11; x += 4) BuildHouse(m, new TileRect(x, 21, 3, 4), FieldTile.RoofGray, rng, doorX: x + 1);
                    break;
            }

            // ---- 余白に木と茂み ----
            for (int i = 0; i < 40; i++)
            {
                int x = rng.Range(1, Width - 1), y = rng.Range(4, BeachY - 1);
                if (m.ZoneAt(x, y) != null || y == RoadY || y == RoadY + 1 || x == 24 || m.Ground(x, y) != FieldTile.Grass) continue;
                m.SetDeco(x, y, rng.Chance(0.5f) ? FieldTile.Tree : FieldTile.Bush);
            }
            return m;
        }

        /// <summary>試合画面用のコート（攻撃方向に7ゾーン×3列が見渡せる大きさ）。</summary>
        public static TownMap PitchMap()
        {
            var m = new TownMap(Town.Furugane, 25, 13);
            for (int y = 0; y < m.height; y++) for (int x = 0; x < m.width; x++) m.SetGround(x, y, FieldTile.DarkGrass);
            BuildPitch(m, new TileRect(1, 1, 23, 11));
            return m;
        }

        static void AddZone(TownMap m, IslandPlace place, string label, TileRect r, int ex, int ey)
        {
            m.zones.Add(new MapZone { place = place, label = label, rect = r, entryX = ex, entryY = ey });
        }

        static FieldTile RoofFor(Town t, SeededRandom rng)
        {
            switch (t)
            {
                case Town.Yushin: return rng.Chance(0.5f) ? FieldTile.RoofRed : FieldTile.RoofWhite;
                case Town.Furugane: return rng.Chance(0.6f) ? FieldTile.RoofBrown : FieldTile.RoofGreen;
                case Town.Kirahama: { var r = rng.Range(0, 3); return r == 0 ? FieldTile.RoofRed : r == 1 ? FieldTile.RoofBlue : FieldTile.RoofGreen; }
                default: return rng.Chance(0.7f) ? FieldTile.RoofGray : FieldTile.RoofBlue;
            }
        }

        /// <summary>見下ろし型の建物：屋根（上）＋壁（下1段、窓と扉）。</summary>
        static void BuildHouse(TownMap m, TileRect r, FieldTile roof, SeededRandom rng, int doorX, FieldTile sign = FieldTile.None)
        {
            for (int y = r.y; y < r.y + r.h - 1; y++)
                for (int x = r.x; x < r.x + r.w; x++) m.SetGround(x, y, roof);
            int wy = r.y + r.h - 1;
            for (int x = r.x; x < r.x + r.w; x++) m.SetGround(x, wy, x == doorX ? FieldTile.Door : (x - r.x) % 2 == 1 ? FieldTile.WallWindow : FieldTile.Wall);
            if (sign != FieldTile.None) m.SetGround(r.x + r.w / 2, r.y, sign);
            // 扉は通れる（入れないが、前に立てる）
            m.solid[wy * m.width + doorX] = true;
        }

        static void BuildDowntown(TownMap m, Town town, TileRect r, SeededRandom rng)
        {
            var paving = town == Town.Tenpou || town == Town.Kirahama ? FieldTile.Asphalt : FieldTile.Path;
            // 広場
            for (int y = r.y + 6; y < r.y + r.h; y++)
                for (int x = r.x; x < r.x + r.w; x++) m.SetGround(x, y, paving);
            m.SetDeco(r.CenterX, r.y + 9, town == Town.Furugane ? FieldTile.Well : FieldTile.Bench);
            m.SetDeco(r.x + 1, r.y + 7, paving == FieldTile.Asphalt ? FieldTile.Lamp : FieldTile.Sign);
            // 建物（上段に4軒）
            int[] xs = { r.x, r.x + 5, r.x + 10, r.x + 15 };
            foreach (var x in xs)
            {
                int h = town == Town.Tenpou ? 6 : 5;
                BuildHouse(m, new TileRect(x, r.y + 6 - h, 4, h), RoofFor(town, rng), rng, doorX: x + 1);
            }
            // 屋台（煌浜）・掲示板
            if (town == Town.Kirahama) { m.SetDeco(r.x + 4, r.y + 10, FieldTile.Stall); m.SetDeco(r.x + 15, r.y + 10, FieldTile.Stall); }
            m.SetDeco(r.x + r.w - 2, r.y + 7, FieldTile.Sign);
        }

        static void BuildPitch(TownMap m, TileRect r)
        {
            for (int y = r.y; y < r.y + r.h; y++)
                for (int x = r.x; x < r.x + r.w; x++)
                {
                    bool edge = y == r.y || y == r.y + r.h - 1;
                    bool side = x == r.x || x == r.x + r.w - 1;
                    bool half = x == r.CenterX;
                    FieldTile t = (x / 2) % 2 == 0 ? FieldTile.Pitch : FieldTile.PitchDark;
                    if (edge) t = FieldTile.LineH;
                    else if (side || half) t = FieldTile.LineV;
                    m.SetGround(x, y, t);
                }
            // ゴール（左右の中央）
            for (int dy = -1; dy <= 0; dy++)
            {
                m.SetDeco(r.x, r.CenterY + dy, FieldTile.Goal);
                m.SetDeco(r.x + r.w - 1, r.CenterY + dy, FieldTile.Goal);
            }
            // 周りのフェンス（入口は下の中央）
            for (int x = r.x - 1; x <= r.x + r.w; x++)
            {
                if (x != r.CenterX && x != r.CenterX - 1) m.SetDeco(x, r.y + r.h, FieldTile.Fence);
                m.SetDeco(x, r.y - 1, FieldTile.Fence);
            }
            for (int y = r.y + r.h + 1; y < RoadY && y < m.height; y++) m.SetGround(r.CenterX, y, FieldTile.Path);
        }

        static void BuildPrison(TownMap m, TileRect r)
        {
            for (int y = r.y; y < r.y + r.h; y++)
                for (int x = r.x; x < r.x + r.w; x++)
                {
                    bool wall = y == r.y || x == r.x || x == r.x + r.w - 1;
                    m.SetGround(x, y, wall ? FieldTile.StoneWall : FieldTile.Path);
                }
            // 面会の受付（下側が開いている）
            m.SetDeco(r.CenterX, r.y + 2, FieldTile.Sign);
        }

        static void BuildBarStreet(TownMap m, TileRect r, SeededRandom rng)
        {
            for (int y = r.y + 3; y < r.y + r.h; y++)
                for (int x = r.x; x < r.x + r.w; x++) m.SetGround(x, y, FieldTile.Asphalt);
            for (int x = r.x; x + 3 <= r.x + r.w; x += 5)
                BuildHouse(m, new TileRect(x, r.y, 4, 3), rng.Chance(0.5f) ? FieldTile.RoofRed : FieldTile.RoofBlue, rng, doorX: x + 1);
            for (int x = r.x + 1; x < r.x + r.w; x += 3) m.SetDeco(x, r.y + 4, FieldTile.Lantern);
        }
    }
}
