using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class InventoryTests
    {
        static IslandCareer NewCareer()
        {
            // 各アイテムの効果を見るので、場所の制限は外す
            var c = new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault()))) { requirePresence = false };
            c.Player.cash = 100000000;
            return c;
        }

        static ShopItem Item(IslandCareer c, string name)
        {
            var i = c.World.State.items.First(x => x.name == name);
            i.status = ItemStatus.OnSale;
            i.stock = System.Math.Max(1, i.stock);
            return i;
        }

        [Test]
        public void EveryCatalogItemHasAnEffect()
        {
            foreach (var i in ShopCatalog.CreateDefault())
                if (i.name != "脅迫タピオカ") // 表では常に完売
                    Assert.IsTrue(ItemEffects.Of(i.name) != null, i.name + " に効果がない");
        }

        [Test]
        public void BuyingTheLastOneSellsItOut()
        {
            var c = NewCareer();
            var i = Item(c, "ブランドジャージ");
            i.stock = 1;
            Assert.IsTrue(c.Inventory.Buy(i.id));
            Assert.AreEqual(ItemStatus.SoldOut, i.status);
            Assert.AreEqual(1, c.Inventory.Count("ブランドジャージ"));
            Assert.IsTrue(!c.Inventory.Buy(i.id));
            var always = c.World.State.items.First(x => x.alwaysSoldOutOnSurface);
            Assert.IsTrue(!c.Inventory.Buy(always.id), "表で常に完売の品は買えない");
        }

        [Test]
        public void FoodRecoversFatigue()
        {
            var c = NewCareer();
            c.Player.fatigue = 60f;
            Assert.IsTrue(c.Inventory.Buy(Item(c, "スタミナ弁当").id));
            Assert.IsTrue(c.Inventory.Use("スタミナ弁当").ok);
            Assert.AreEqual(35f, c.Player.fatigue, 0.01);
            Assert.AreEqual(0, c.Inventory.Count("スタミナ弁当"));
        }

        [Test]
        public void YouthFlakesReallyMakeYouYounger()
        {
            var c = NewCareer();
            c.Player.age = 35;
            c.Inventory.Buy(Item(c, "若返りフレーク").id);
            c.Inventory.Buy(Item(c, "若返りフレーク").id);
            Assert.IsTrue(c.Inventory.Use("若返りフレーク").ok);
            Assert.AreEqual(34, c.Player.age);
            Assert.IsTrue(!c.Inventory.Use("若返りフレーク").ok, "1年に1回まで");
        }

        [Test]
        public void SpikesRaiseStatsInMatches()
        {
            var c = NewCareer();
            int before = MatchFactory.FromProtagonist(c.Player).stats.dribbling;
            c.Inventory.Buy(Item(c, "限定スパイク「流星」").id);
            Assert.IsTrue(c.Inventory.Use("限定スパイク「流星」").ok);
            Assert.AreEqual(before + 4, MatchFactory.FromProtagonist(c.Player).stats.dribbling);
            Assert.IsTrue(c.Inventory.Use("限定スパイク「流星」").ok, "もう一度使うと外す");
            Assert.AreEqual(before, MatchFactory.FromProtagonist(c.Player).stats.dribbling);
        }

        [Test]
        public void WardingCancelsMafiaTricksUntilTheMatchEnds()
        {
            var c = NewCareer();
            c.Inventory.Buy(Item(c, "魔除けタバコ").id);
            c.Inventory.Use("魔除けタバコ");
            Assert.IsTrue(c.Player.matchBuffs.Contains(MatchBuff.Warded));

            var mine = MatchFactory.Generic("主人公FC", 60, 10, new SeededRandom(1));
            MatchFactory.InsertProtagonist(mine, c.Player, 11);
            Assert.IsTrue(mine.traits.Contains(BossTrait.Warded));
            float before = mine.players.Average(p => p.tension);
            var mafia = BossTeams.Build(BossKind.Mafia, c, new SeededRandom(2));
            var setup = new MatchSetup { home = mine, away = mafia, protagonistSide = Side.Home, seed = 3 };
            var e = new MatchEngine(setup);
            Assert.AreEqual(before, mine.players.Average(p => p.tension), 0.01, "威圧が効かない");

            var r = e.RunToEnd();
            MatchResultApplier.Apply(c, setup, r);
            Assert.AreEqual(0, c.Player.matchBuffs.Count, "試合が終わると効果は切れる");
        }

        [Test]
        public void IntimidationTapiocaAndHighballBuffs()
        {
            var c = NewCareer();
            c.Player.blackMarketAccess = true;
            c.BlackMarket.LearnLocation();
            var tapioca = Item(c, "脅迫タピオカ（裏ルート）");
            Assert.IsTrue(c.BlackMarket.Buy(tapioca.id));
            Assert.IsTrue(c.Inventory.Use("脅迫タピオカ（裏ルート）").ok);
            c.Inventory.Buy(Item(c, "サッカーハイボール").id);
            float plain = MatchFactory.FromProtagonist(c.Player).tension;
            c.Inventory.Use("サッカーハイボール");
            Assert.Greater(MatchFactory.FromProtagonist(c.Player).tension, plain);
            var mine = MatchFactory.Generic("主人公FC", 60, 10, new SeededRandom(1));
            MatchFactory.InsertProtagonist(mine, c.Player, 11);
            Assert.IsTrue(mine.traits.Contains(BossTrait.Intimidation));
        }

        [Test]
        public void DisplaysAndFashion()
        {
            var c = NewCareer();
            c.Inventory.Buy(Item(c, "伝説のユニフォーム（サイン入り）").id);
            float fame = c.Player.fame;
            Assert.IsTrue(c.Inventory.Use("伝説のユニフォーム（サイン入り）").ok);
            Assert.Greater(c.Player.fame, fame);
            Assert.IsTrue(!c.Inventory.Use("伝説のユニフォーム（サイン入り）").ok, "飾るのは一度だけ");

            c.Player.fame = 50;
            var fan = c.Population.Database.npcs.First(n => n.personality == Personality.Introvert && n.fame < 20 && !n.isIncarcerated);
            float plain = c.Fans.RequestChance(fan);
            c.Inventory.Buy(Item(c, "島ブランドのサングラス").id);
            c.Inventory.Use("島ブランドのサングラス");
            Assert.Greater(c.Fans.RequestChance(fan), plain, "おしゃれだと声をかけられる");
        }
    }
}
