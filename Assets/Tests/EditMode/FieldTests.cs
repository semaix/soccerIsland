using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Field;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class FieldTests
    {
        static readonly Town[] Towns = { Town.Yushin, Town.Furugane, Town.Kirahama, Town.Tenpou };

        [Test]
        public void TileOrderMatchesTheArtSheet()
        {
            // Tools/PixelArt/generate_art.py の TILE_NAMES と同じ数・順番
            var path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets", "Resources", "Art", "tiles.txt");
            Assume.That(System.IO.File.Exists(path) ? 1 : 0, Is.GreaterThan(0));
            var names = System.IO.File.ReadAllLines(path).Where(l => l.Length > 0).ToList();
            var tiles = System.Enum.GetValues(typeof(FieldTile)).Cast<FieldTile>().Where(t => t != FieldTile.None).ToList();
            Assert.AreEqual(names.Count, tiles.Count);
            for (int i = 0; i < tiles.Count; i++)
                Assert.AreEqual(names[i].Replace("_", ""), tiles[i].ToString().ToLowerInvariant(), $"{i}番目");
        }

        [Test]
        public void EveryTownHasItsPlaces()
        {
            foreach (var t in Towns)
            {
                var m = TownMapGenerator.Generate(t, Town.Furugane, 1);
                Assert.AreEqual(48, m.width);
                Assert.IsTrue(m.Zone(IslandPlace.Workplace) != null && m.Zone(IslandPlace.ClubGround) != null && m.Zone(IslandPlace.Beach) != null, t.ToString());
                Assert.AreEqual(t == Town.Furugane, m.Zone(IslandPlace.Home) != null, "自宅は住んでいる街だけ");
                Assert.AreEqual(t == Town.Kirahama, m.Zone(IslandPlace.Bar) != null, "飲み屋街は煌浜");
                Assert.AreEqual(t == Town.Yushin, m.Zone(IslandPlace.Hospital) != null, "病院は勇心町");
                Assert.AreEqual(t == Town.Furugane, m.Zone(IslandPlace.Prison) != null, "刑務所は古鐘町");
                // マップ上の場所は IslandMap の定義と一致する
                foreach (var z in m.zones) Assert.IsTrue(IslandMap.Exists(t, z.place, Town.Furugane), $"{t} {z.place}");
            }
        }

        [Test]
        public void EntrancesAndExitsAreWalkableAndConnected()
        {
            foreach (var t in Towns)
            {
                var m = TownMapGenerator.Generate(t, Town.Furugane, 3);
                var reach = Reachable(m, m.exits[0].rect.x, m.exits[0].rect.y);
                foreach (var e in m.exits) Assert.IsTrue(reach.Contains(e.rect.y * m.width + e.rect.x), $"{t}：出口同士がつながっている");
                foreach (var z in m.zones)
                {
                    Assert.IsTrue(!m.IsSolid(z.entryX, z.entryY), $"{t} {z.label} の入口に立てる");
                    Assert.IsTrue(reach.Contains(z.entryY * m.width + z.entryX), $"{t} {z.label} に歩いて行ける");
                    Assert.Greater(m.WalkableCells(z).Count, 10, $"{t} {z.label} に島民が立てる");
                }
                Assert.AreEqual(TownMapGenerator.WestNeighbor(t), m.exits.First(e => e.westEdge).to);
                Assert.AreEqual(TownMapGenerator.EastNeighbor(t), m.exits.First(e => !e.westEdge).to);
                Assert.AreEqual(1, IslandMap.Distance(t, TownMapGenerator.EastNeighbor(t)), "出口の先は隣の街");
            }
        }

        static HashSet<int> Reachable(TownMap m, int sx, int sy)
        {
            var seen = new HashSet<int> { sy * m.width + sx };
            var q = new Queue<int>(seen);
            while (q.Count > 0)
            {
                int c = q.Dequeue(), x = c % m.width, y = c / m.width;
                foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + d.Item1, ny = y + d.Item2;
                    if (m.IsSolid(nx, ny)) continue;
                    if (seen.Add(ny * m.width + nx)) q.Enqueue(ny * m.width + nx);
                }
            }
            return seen;
        }

        [Test]
        public void MapsAreTheSameEveryTime()
        {
            var a = TownMapGenerator.Generate(Town.Kirahama, Town.Furugane, 9);
            var b = TownMapGenerator.Generate(Town.Kirahama, Town.Furugane, 9);
            Assert.IsTrue(a.ground.SequenceEqual(b.ground) && a.deco.SequenceEqual(b.deco));
        }

        [Test]
        public void ThePitchHasGoalsAndWaterIsNotWalkable()
        {
            var m = TownMapGenerator.Generate(Town.Yushin, Town.Furugane, 2);
            var pitch = m.Zone(IslandPlace.ClubGround).rect;
            int goals = 0;
            for (int y = pitch.y; y < pitch.y + pitch.h; y++)
                for (int x = pitch.x; x < pitch.x + pitch.w; x++)
                    if (m.Deco(x, y) == FieldTile.Goal) goals++;
            Assert.AreEqual(4, goals, "左右にゴール");
            Assert.IsTrue(m.IsSolid(10, m.height - 1), "海には入れない");
            Assert.IsTrue(m.IsSolid(-1, 5) && m.IsSolid(5, -1), "マップの外には出られない");
        }

        [Test]
        public void IslandersLookLikeTheirJobs()
        {
            var db = new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault()))).Population.Database;
            var doctor = CharacterLooks.For(db.npcs.First(n => n.occupation == Occupation.Doctor));
            var police = CharacterLooks.For(db.npcs.First(n => n.occupation == Occupation.Police));
            var builder = CharacterLooks.For(db.npcs.First(n => n.occupation == Occupation.Construction));
            var schoolgirl = CharacterLooks.For(db.npcs.First(n => n.occupation == Occupation.Student && n.gender == Gender.Female));
            Assert.IsTrue(doctor.layers.Contains("char_coat"), "医者は白衣");
            Assert.IsTrue(police.layers.Contains("char_hat_cap"), "警官は帽子");
            Assert.IsTrue(builder.layers.Contains("char_hat_helmet"), "土木はヘルメット");
            Assert.IsTrue(schoolgirl.layers.Contains("char_skirt"), "女子学生はスカート");
            Assert.AreEqual("char_body", doctor.layers[0], "体が一番下");

            var looks = db.npcs.Select(n => CharacterLooks.For(n).Key).Distinct().Count();
            Assert.Greater(looks, 500, "1000人の見た目がかなりばらける");

            Assert.IsTrue(doctor.TryMap(CharacterLook.KeyOutfit, out var coat) && coat.r > 230, "キー色は服の色に塗り替わる");
            Assert.IsTrue(!doctor.TryMap(new Rgb(24, 20, 32), out _), "輪郭は塗り替えない");
        }
    }
}
