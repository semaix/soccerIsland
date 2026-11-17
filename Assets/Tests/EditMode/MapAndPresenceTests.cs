using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class MapAndPresenceTests
    {
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));

        [Test]
        public void TheIslandIsARingOfFourTowns()
        {
            Assert.AreEqual(0, IslandMap.Distance(Town.Kirahama, Town.Kirahama));
            Assert.AreEqual(1, IslandMap.Distance(Town.Yushin, Town.Furugane));
            Assert.AreEqual(1, IslandMap.Distance(Town.Tenpou, Town.Yushin), "島をぐるりと回れる");
            Assert.AreEqual(2, IslandMap.Distance(Town.Yushin, Town.Kirahama), "反対側");
            Assert.IsTrue(IslandMap.PlacesIn(Town.Kirahama, Town.Furugane).Contains(IslandPlace.Bar));
            Assert.IsTrue(!IslandMap.PlacesIn(Town.Tenpou, Town.Furugane).Contains(IslandPlace.Bar), "飲み屋街は煌浜だけ");
            Assert.IsTrue(IslandMap.PlacesIn(Town.Yushin, Town.Furugane).Contains(IslandPlace.Hospital));
            Assert.IsTrue(!IslandMap.PlacesIn(Town.Kirahama, Town.Furugane).Contains(IslandPlace.Home), "自宅は住んでいる街だけ");
        }

        [Test]
        public void YouStartAtHomeInTheMorning()
        {
            var c = NewCareer();
            Assert.IsTrue(c.requirePresence, "既定で場所の制限あり");
            Assert.AreEqual(c.Player.homeTown, c.Here.town);
            Assert.AreEqual(IslandPlace.Home, c.Here.place);
            Assert.AreEqual(TimeOfDay.Morning, c.Now);
        }

        [Test]
        public void YouCanOnlyTalkToPeopleWhoAreThere()
        {
            var c = NewCareer();
            var db = c.Population.Database;
            int day = db.currentDay;
            // 朝の勇心病院で働いている医者
            var doc = db.npcs.First(n => n.occupation == Occupation.Doctor && c.Locate(n.id).Same(new IslandLocation(Town.Yushin, IslandPlace.Workplace)));
            Assert.IsTrue(c.Interact(doc.id).notHere, "家にいたら話せない");
            c.MoveTo(Town.Yushin, IslandPlace.Workplace);
            Assert.IsTrue(c.PeopleHere().Contains(doc));
            var r = c.Interact(doc.id);
            Assert.IsTrue(!r.notHere && r.conversation != null);
            Assert.IsTrue(c.PeopleHere().All(n => c.IsHere(n.id)));
        }

        [Test]
        public void MovingBetweenTownsTakesTime()
        {
            var c = NewCareer(); // 古鐘町に住んでいる
            Assert.AreEqual(Town.Furugane, c.Here.town);
            c.MoveTo(Town.Furugane, IslandPlace.ClubGround);
            Assert.AreEqual(TimeOfDay.Morning, c.Now, "街の中の移動は時間がかからない");
            c.MoveTo(Town.Kirahama, IslandPlace.Workplace);
            Assert.AreEqual(TimeOfDay.Afternoon, c.Now, "隣の街は1時間帯");
            c.MoveTo(Town.Yushin, IslandPlace.Hospital);
            Assert.AreEqual(TimeOfDay.Night, c.Now, "反対側は2時間帯");
            Assert.IsTrue(c.MoveTo(Town.Tenpou, IslandPlace.Bar) == null, "天峰台に飲み屋街はない");

            int day = c.Population.Database.currentDay;
            var reports = c.MoveTo(Town.Kirahama, IslandPlace.Bar);
            Assert.AreEqual(1, reports.Count, "深夜を越えると日付が変わる");
            Assert.AreEqual(day + 1, c.Population.Database.currentDay);
        }

        [Test]
        public void ShopsTrainingHospitalAndBlackMarketNeedYouThere()
        {
            var c = NewCareer();
            c.Player.cash = 10000000;
            var item = c.World.State.items.First(i => i.name == "スタミナ弁当"); // 煌浜のキラマート
            item.status = ItemStatus.OnSale; item.stock = 5;
            Assert.IsTrue(!c.Inventory.Buy(item.id), "お店の街まで行かないと買えない");
            c.MoveTo(Town.Kirahama, IslandPlace.Workplace);
            Assert.IsTrue(c.Inventory.Buy(item.id));

            Assert.IsTrue(!c.Training.Train(TrainingMenu.Shooting).done, "練習はグラウンドで");
            c.MoveTo(Town.Kirahama, IslandPlace.ClubGround);
            Assert.IsTrue(c.Training.Train(TrainingMenu.Shooting).done);

            c.Player.injuredUntilDay = c.Population.Database.currentDay + 6;
            Assert.AreEqual("病院（勇心町）まで行かないと", c.Hospital.TreatProtagonist());

            c.Player.blackMarketAccess = true;
            c.BlackMarket.LearnLocation();
            Assert.IsTrue(!c.BlackMarket.CanEnter, "開催場所まで行かないと入れない");
            Assert.IsTrue(c.MoveTo(c.BlackMarket.CurrentTown, IslandPlace.BlackMarket) != null);
            Assert.IsTrue(c.BlackMarket.CanEnter);
        }

        [Test]
        public void TrainingPartnersMustBeAtTheGround()
        {
            var c = NewCareer();
            var db = c.Population.Database;
            var partner = db.npcs.First(n => c.Locate(n.id).place != IslandPlace.ClubGround && !n.isIncarcerated);
            c.Population.Knowledge.Reveal(partner.id, NPCInfoFlags.Basic);
            c.Population.Knowledge.GetOrCreate(partner.id).affinity = 80;
            c.MoveTo(c.Here.town, IslandPlace.ClubGround);
            var r = c.Training.Train(TrainingMenu.Dribbling, partner.id);
            Assert.IsTrue(!r.done);
            Assert.AreEqual("その人は今グラウンドにいない", r.refusal);
        }

        [Test]
        public void ThePhantomCanBeFoundAtTheNightGround()
        {
            var c = NewCareer();
            var phantom = c.Population.Database.npcs.First(n => n.soccerPersonality == SoccerPersonality.Phantom);
            bool met = false;
            for (int d = 0; d < 10 && !met; d++)
            {
                c.Player.timeOfDay = (int)TimeOfDay.Night;
                var loc = c.Locate(phantom.id);
                if (loc.place == IslandPlace.ClubGround)
                {
                    c.MoveTo(loc.town, IslandPlace.ClubGround);
                    if (c.Now == TimeOfDay.Night && c.IsHere(phantom.id)) met = !c.Interact(phantom.id).notHere;
                }
                c.AdvanceDay();
            }
            Assert.IsTrue(met, "夜のグラウンドに行けば謎のプレーヤーに会える");
        }

        [Test]
        public void PhoneWorksFromAnywhere()
        {
            var c = NewCareer();
            var n = c.Population.Database.npcs.First(x => x.personality == Personality.Extrovert && x.phoneNumber != "" && !c.IsHere(x.id));
            c.Population.Knowledge.Reveal(n.id, NPCInfoFlags.Basic | NPCInfoFlags.PhoneNumber);
            c.Population.Knowledge.GetOrCreate(n.id).affinity = 90;
            Assert.IsTrue(c.Phone.CanCall(n.id), "電話は離れていてもかけられる");
        }
    }
}
