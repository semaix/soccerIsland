using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Field;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class FieldSessionTests
    {
        static FieldSession NewSession()
        {
            var c = new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));
            c.Player.cash = 100000000;
            return new FieldSession(c);
        }

        static List<string> Labels(MenuPage p) => p.items().Select(i => i.label).ToList();
        static MenuItem Item(MenuPage p, string startsWith) => p.items().First(i => i.label.StartsWith(startsWith));

        [Test]
        public void MainMenuHasEverything()
        {
            var s = NewSession();
            var labels = Labels(s.MainMenu());
            foreach (var want in new[] { "つよさ", "もちもの", "しりあい", "でんわ", "しんぶん", "そうかんず", "おかね", "チーム", "サイン・パフォーマンス", "やすむ", "セーブ" })
                Assert.IsTrue(labels.Any(l => l.StartsWith(want)), want);
        }

        [Test]
        public void EachPlaceOffersItsOwnThings()
        {
            var s = NewSession();
            var c = s.Career;
            Assert.IsTrue(Labels(s.PlaceMenu()).Any(l => l.StartsWith("寝る")), "自宅では寝られる");
            c.MoveTo(c.Here.town, IslandPlace.Workplace);
            var down = Labels(s.PlaceMenu());
            Assert.IsTrue(down.Contains("お店") && down.Contains("銀行") && down.Any(l => l.StartsWith("サッカー協会")));
            c.MoveTo(c.Here.town, IslandPlace.ClubGround);
            Assert.IsTrue(Labels(s.PlaceMenu()).Contains("練習する"));
            c.MoveTo(Town.Yushin, IslandPlace.Hospital);
            Assert.IsTrue(Labels(s.PlaceMenu()).Any(l => l.StartsWith("けが")));
        }

        [Test]
        public void ShoppingFromTheMenu()
        {
            var s = NewSession();
            var c = s.Career;
            c.MoveTo(Town.Kirahama, IslandPlace.Workplace);
            var shop = s.Shop();
            Assert.IsTrue(shop.items().All(i => i.label.Contains("「")));
            Assert.IsTrue(c.World.State.items.Where(i => i.town == Town.Kirahama && !i.isUnderground).All(i => shop.items().Any(m => m.label.Contains(i.name))), "その街のお店の品だけ並ぶ");
            var bento = shop.items().First(i => i.label.Contains("スタミナ弁当") && i.enabled);
            var r = bento.select();
            Assert.IsTrue(r.messages[0].Contains("買った"));
            Assert.AreEqual(1, c.Inventory.Count("スタミナ弁当"));
            var use = Item(s.Inventory(), "スタミナ弁当").select();
            Assert.IsTrue(use.messages.Count > 0);
        }

        [Test]
        public void TalkingAndScoutingFromTheTalkMenu()
        {
            var s = NewSession();
            var c = s.Career;
            c.MoveTo(Town.Yushin, IslandPlace.Workplace);
            var doc = c.PeopleHere().First(n => n.occupation == Occupation.Doctor || n.age >= 20);
            var menu = s.TalkMenu(doc.id);
            Assert.IsTrue(Labels(menu).Contains("話す") && Labels(menu).Contains("スカウトする"));
            var talk = Item(menu, "話す").select();
            Assert.IsTrue(talk.messages.Count > 0 && talk.close);
            var scout = Item(menu, "スカウトする").select();
            Assert.IsTrue(scout.messages[0].Length > 0, "断られても理由が出る");
            Assert.IsTrue(!Labels(menu).Contains("デートに誘う"), "1周目は恋愛なし");
        }

        [Test]
        public void SleepingStartsTheNextMorningWithTheNews()
        {
            var s = NewSession();
            var c = s.Career;
            int day = c.Population.Database.currentDay;
            var r = Item(s.PlaceMenu(), "寝る").select();
            Assert.AreEqual(day + 1, c.Population.Database.currentDay);
            Assert.AreEqual(TimeOfDay.Morning, c.Now);
            Assert.IsTrue(r.timeChanged && r.close);
            Assert.IsTrue(r.messages.Any(m => m.StartsWith("【朝刊】")));
        }

        [Test]
        public void TournamentFromTheAssociationToTheGround()
        {
            var s = NewSession();
            var c = s.Career;
            c.MoveTo(c.Here.town, IslandPlace.Workplace);
            var enter = Item(s.Association(), "地域対抗カップ").select();
            Assert.IsTrue(s.Tournament != null && enter.close);
            c.MoveTo(c.Here.town, IslandPlace.ClubGround);
            var play = s.MatchOptions().First(i => i.label.StartsWith("地域対抗カップ"));
            var r = play.select();
            Assert.IsTrue(r.match != null, "試合が始まる");
            r.match.AutoPlay();
            var lines = s.FinishMatch(r.match);
            Assert.IsTrue(lines[0].StartsWith("試合終了"));
            Assert.Greater(s.Tournament.State.history.Count, 0, "大会が進む");
        }

        [Test]
        public void MatchInvitesFromCallsShowUpAtTheGround()
        {
            var s = NewSession();
            var c = s.Career;
            SoccerIsland.World.Match m = null;
            for (int d = 0; d < 14 && m == null; d++)
            {
                m = c.World.TodaysMatches().FirstOrDefault(x => !x.played && x.awayLineup.Count > 0 && !x.IsUnderground && x.category != MatchCategory.PrisonLeague);
                if (m == null) c.AdvanceDay();
            }
            Assert.IsTrue(m != null);
            s.Invites.Add(new IncomingCall { callerId = m.homeLineup[0], kind = IncomingCallKind.MatchInvite, matchId = m.id, joinHome = true });
            c.MoveTo(c.Here.town, IslandPlace.ClubGround);
            var item = s.MatchOptions().First(i => i.label.StartsWith("助っ人"));
            Assert.IsTrue(item.select().match != null);
        }

        [Test]
        public void EveryMenuOpensWithoutErrors()
        {
            var s = NewSession();
            var c = s.Career;
            // 何人かと知り合っておく
            c.requirePresence = false;
            foreach (var n in c.Population.Database.npcs.Take(30)) { c.Population.Knowledge.Reveal(n.id, NPCInfoFlags.All); c.Population.Knowledge.GetOrCreate(n.id).affinity = 80; }
            c.requirePresence = true;
            int opened = 0;
            foreach (var place in new[] { IslandPlace.Home, IslandPlace.Workplace, IslandPlace.ClubGround })
            {
                c.MoveTo(c.Player.homeTown, place);
                foreach (var root in new[] { s.MainMenu(), s.PlaceMenu() })
                    opened += Walk(root, 0);
            }
            Assert.Greater(opened, 30);
        }

        /// <summary>メニューを開ける画面だけたどる（試合・寝る・セーブ・引退などは押さない）。</summary>
        static int Walk(MenuPage page, int depth)
        {
            if (page.items == null) { Assert.IsTrue(page.text != null); return 1; }
            int n = 1;
            if (depth >= 2) return n;
            foreach (var item in page.items())
            {
                if (!item.enabled || Skip(item.label)) continue;
                var r = item.select();
                if (r != null && r.open != null) n += Walk(r.open, depth + 1);
            }
            return n;
        }

        static bool Skip(string label) =>
            new[] { "寝る", "やすむ", "セーブ", "引退", "試合", "大会", "決戦", "カップ", "トーナメント", "助っ人", "サイン", "放出", "借りる", "預ける", "宝くじ", "株", "toto", "引き出す", "出る" }.Any(label.Contains);
    }
}
