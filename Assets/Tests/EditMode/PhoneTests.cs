using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class PhoneTests
    {
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));

        static void KnowNumber(IslandCareer c, NPCData n, int affinity)
        {
            c.Population.Knowledge.Reveal(n.id, NPCInfoFlags.Basic | NPCInfoFlags.PhoneNumber);
            c.Population.Knowledge.GetOrCreate(n.id).affinity = affinity;
        }

        [Test]
        public void YouNeedTheNumberAndOneCallPerDay()
        {
            var c = NewCareer();
            var n = c.Population.Database.npcs.First(x => x.personality == Personality.Extrovert && x.phoneNumber != "");
            Assert.IsTrue(!c.Phone.CanCall(n.id), "番号を知らない");
            KnowNumber(c, n, 80);
            Assert.IsTrue(c.Phone.CanCall(n.id));
            c.Phone.Call(n.id, CallPurpose.Chat);
            Assert.IsTrue(!c.Phone.CanCall(n.id), "同じ人には1日1回");
            c.AdvanceDay();
            Assert.IsTrue(c.Phone.CanCall(n.id));
            Assert.IsTrue(c.Phone.PhoneBook().Contains(n.id));
        }

        [Test]
        public void ExtrovertsPickUpMoreThanGloomyPeople()
        {
            var c = NewCareer();
            int ext = 0, gloom = 0;
            foreach (var n in c.Population.Database.npcs.Where(x => x.phoneNumber != "" && !x.isUnderground).Take(400))
            {
                KnowNumber(c, n, 0);
                var r = c.Phone.Call(n.id, CallPurpose.InviteTraining);
                if (!r.answered) continue;
                if (n.personality == Personality.Extrovert) ext++;
                if (n.personality == Personality.Gloomy) gloom++;
            }
            Assert.Greater(ext, gloom);
        }

        [Test]
        public void ChatCallWorksLikeTalking()
        {
            var c = NewCareer();
            var n = c.Population.Database.npcs.First(x => x.personality == Personality.Extrovert && x.phoneNumber != "" && !x.isUnderground);
            KnowNumber(c, n, 90);
            CallResult r = null;
            for (int i = 0; i < 10 && (r == null || !r.answered); i++) { r = c.Phone.Call(n.id, CallPurpose.Chat); c.AdvanceDay(); }
            Assert.IsTrue(r.answered && r.conversation != null && r.conversation.lines.Count > 0);
        }

        [Test]
        public void AskingAboutSomeoneAddsToTheRelationMap()
        {
            var c = NewCareer();
            var n = c.Population.Database.npcs.First(x => x.personality == Personality.Extrovert && x.phoneNumber != "" && !x.isUnderground && x.relations.Count > 0);
            var target = n.relations[0].targetId;
            KnowNumber(c, n, 90);
            CallResult r = null;
            for (int i = 0; i < 10 && (r == null || !r.answered); i++) { r = c.Phone.Call(n.id, CallPurpose.AskAbout, target); c.AdvanceDay(); }
            Assert.IsTrue(r.answered);
            Assert.IsTrue(c.Population.Knowledge.KnowsRelation(n.id, target));
            Assert.IsTrue(c.Population.Knowledge.Knows(target, NPCInfoFlags.Name));
        }

        [Test]
        public void FriendsCallYouAndInviteYouToMatches()
        {
            var c = NewCareer();
            // 試合に出る予定のある島民と仲良くしておく
            var players = new HashSet<int>(c.World.State.matches.Where(x => !x.IsUnderground && x.category != MatchCategory.PrisonLeague)
                                                                 .SelectMany(x => x.homeLineup.Concat(x.awayLineup)));
            foreach (var n in c.Population.Database.npcs.Where(x => x.phoneNumber != "" && players.Contains(x.id)).Take(80))
                KnowNumber(c, n, 90);
            var calls = new List<IncomingCall>();
            for (int d = 0; d < 14; d++) calls.AddRange(c.AdvanceDay().incomingCalls);
            Assert.Greater(calls.Count, 3);

            var invite = calls.LastOrDefault(x => x.kind == IncomingCallKind.MatchInvite);
            Assert.IsTrue(invite != null, "助っ人の誘いが来る");
            var m = c.World.State.GetMatch(invite.matchId);
            Assert.IsTrue(m == null || m.Involves(invite.callerId), "誘ってきた人はその試合に出る");
        }

        [Test]
        public void AcceptingAnInvitePutsYouOnTheCallersTeam()
        {
            var c = NewCareer();
            var m = c.World.State.matches.First(x => !x.played && x.awayLineup.Count > 0 && !x.IsUnderground && x.category != MatchCategory.PrisonLeague);
            var caller = m.awayLineup[0];
            var call = new IncomingCall { callerId = caller, kind = IncomingCallKind.MatchInvite, matchId = m.id, joinHome = false };
            var setup = MatchFactory.FromInvite(c, call);
            Assert.AreEqual(Side.Away, setup.protagonistSide);
            Assert.IsTrue(setup.away.Protagonist != null);
            Assert.IsTrue(setup.away.players.Any(p => p.npcId == caller) || setup.away.players.Count == 11);
        }

        [Test]
        public void LoanSharkCalls()
        {
            var c = NewCareer();
            c.Player.blackMarketAccess = true;
            c.Bank.Borrow(Lender.LoanShark, 1000000, 10);
            c.Player.cash = 0;
            var calls = new List<IncomingCall>();
            for (int d = 0; d < 16; d++) calls.AddRange(c.AdvanceDay().incomingCalls);
            Assert.IsTrue(calls.Any(x => x.kind == IncomingCallKind.LoanShark));
        }
    }
}
