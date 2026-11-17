using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class MatchTests
    {
        static IslandCareer _career;
        static IslandCareer Career()
        {
            if (_career == null)
                _career = new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));
            return _career;
        }

        static IslandCareer FreshCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));

        static List<SoccerIsland.World.Match> NormalMatches(IslandCareer c) =>
            c.World.State.matches.Where(m => m.awayLineup.Count > 0 && !m.IsUnderground && m.category != MatchCategory.PrisonLeague).ToList();

        static MatchSetup Setup(int i, Position? pos = null)
        {
            var c = Career();
            if (pos.HasValue) c.Player.position = pos.Value;
            var ms = NormalMatches(c);
            var s = MatchFactory.FromWorldMatch(c, ms[i % ms.Count], Side.Home, i + 1);
            c.Player.position = Position.MF;
            return s;
        }

        static MatchPlayer P(Personality p, SoccerPersonality sp, Position pos = Position.MF, float tension = 60f)
        {
            var stats = new NPCAbilities();
            for (int i = 0; i < NPCAbilities.StatCount; i++) stats[i] = 50;
            return new MatchPlayer { name = sp + "/" + p, personality = p, soccerPersonality = sp, position = pos, stats = stats, tension = tension, maxGuts = 600, guts = 600 };
        }

        // ---------------- 性格バランス ----------------

        [Test]
        public void BalancedTeamHasBetterChemistry()
        {
            var balanced = new List<MatchPlayer>
            {
                P(Personality.Introvert, SoccerPersonality.Egoist), P(Personality.Extrovert, SoccerPersonality.Devoted),
                P(Personality.Gloomy, SoccerPersonality.Devoted), P(Personality.Extrovert, SoccerPersonality.MoodMaker),
                P(Personality.Introvert, SoccerPersonality.Sense), P(Personality.Gloomy, SoccerPersonality.Fighter),
            };
            var bad = new List<MatchPlayer>
            {
                P(Personality.Gloomy, SoccerPersonality.Egoist), P(Personality.Gloomy, SoccerPersonality.Egoist),
                P(Personality.Gloomy, SoccerPersonality.Egoist), P(Personality.Gloomy, SoccerPersonality.Hysteric),
                P(Personality.Gloomy, SoccerPersonality.Hysteric), P(Personality.Introvert, SoccerPersonality.Sense),
            };
            var a = TeamChemistry.Evaluate(balanced);
            var b = TeamChemistry.Evaluate(bad);
            Assert.Greater(a.score, 20f);
            Assert.Less(b.score, -20f);
            Assert.IsTrue(b.reasons.Any(r => r.Contains("エゴイスト同士")));
            Assert.Greater(b.cardRisk, a.cardRisk);
        }

        // ---------------- ミーティング ----------------

        [Test]
        public void MeetingReactionsDependOnPersonalityAndCanStartArguments()
        {
            var ego = P(Personality.Extrovert, SoccerPersonality.Egoist, Position.FW);
            var dev = P(Personality.Introvert, SoccerPersonality.Devoted, Position.DF);
            var team = new MatchTeam { players = { ego, dev, P(Personality.Gloomy, SoccerPersonality.Fighter) } };
            var r = Meeting.Hold(team, MeetingTopic.Defend, new SeededRandom(1));
            Assert.Less(ego.tension, 60f, "エゴイストは守備的な方針が嫌い");
            Assert.Greater(dev.tension, 60f, "献身的は守備的な方針が好き");
            Assert.AreEqual(1, r.arguments.Count, "エゴイストと献身的が口論する");
            Assert.Less(r.moodDelta, 0f);

            var sense = P(Personality.Extrovert, SoccerPersonality.Sense);
            var team2 = new MatchTeam { players = { sense } };
            Meeting.Hold(team2, MeetingTopic.Freedom, new SeededRandom(1));
            Assert.AreEqual(85f, sense.tension, 0.01f);

            var trap = new MatchTeam { players = { dev } };
            Meeting.Hold(trap, MeetingTopic.OffsideTrapPlan, new SeededRandom(1));
            Assert.IsTrue(trap.offsideTrapPlanned);
        }

        // ---------------- コーチング ----------------

        [Test]
        public void CoachingThatMeetsTheDemandWorks()
        {
            var rng = new SeededRandom(3);
            var mood = P(Personality.Extrovert, SoccerPersonality.MoodMaker, tension: 30f);
            var team = new MatchTeam { players = { mood } };
            var opp = new MatchTeam();
            bool got = false;
            for (int m = 0; m < 300 && !got; m += 8) got = Coaching.UpdateDemand(mood, team, opp, m, rng);
            Assert.IsTrue(got);
            Assert.AreEqual(CoachingCommand.Praise, mood.demand.Value);

            var r = Coaching.CoachPlayer(mood, CoachingCommand.Praise, 50f, rng);
            Assert.IsTrue(r.metDemand && r.accepted);
            Assert.AreEqual(50f, mood.tension, 0.01f);
            Assert.IsTrue(!mood.demand.HasValue);

            var hyst = P(Personality.Extrovert, SoccerPersonality.Hysteric, tension: 50f);
            Coaching.CoachPlayer(hyst, CoachingCommand.Scold, 80f, rng);
            Assert.AreEqual(90f, hyst.tension, 0.01f, "ヒステリーを叱ると爆発する");

            var shy = P(Personality.Introvert, SoccerPersonality.Devoted, tension: 50f);
            Coaching.CoachPlayer(shy, CoachingCommand.Scold, 80f, rng);
            Assert.AreEqual(30f, shy.tension, 0.01f, "内向的な人は叱られると萎縮する");
        }

        // ---------------- 試合 ----------------

        [Test]
        public void MatchesFinishWithReasonableScores()
        {
            double goals = 0;
            for (int i = 0; i < 40; i++)
            {
                var e = new MatchEngine(Setup(i));
                var r = e.RunToEnd();
                Assert.IsTrue(e.Finished);
                Assert.GreaterOrEqual(e.Minute, 90f);
                goals += r.homeScore + r.awayScore;
                Assert.IsTrue(r.log.Any(x => x.type == MatchEventType.HalfTime));
            }
            double avg = goals / 40;
            Assert.Greater(avg, 1.0);
            Assert.Less(avg, 6.0);
        }

        [Test]
        public void ProtagonistGetsCommandPrompts()
        {
            var e = new MatchEngine(Setup(1)) { Mode = ControlMode.ProtagonistOnly };
            var kinds = new HashSet<PromptKind>();
            int prompts = 0;
            while (true)
            {
                var p = e.Advance();
                if (p == null) break;
                prompts++;
                kinds.Add(p.kind);
                Assert.IsTrue(p.actor.isProtagonist);
                Assert.Throws<ArgumentException>(() => e.Choose(MatchCommand.MissOnPurpose)); // 八百長ミッションがないので選べない
                var cmd = p.kind == PromptKind.Attack ? (p.zone >= 5 ? MatchCommand.Shoot : MatchCommand.Pass)
                        : p.kind == PromptKind.Defend ? MatchCommand.Tackle : p.options[0];
                if (!p.options.Contains(cmd)) cmd = p.options[0];
                e.Choose(cmd, p.passTargets.FirstOrDefault());
            }
            Assert.Greater(prompts, 5);
            Assert.IsTrue(kinds.Contains(PromptKind.Attack) && kinds.Contains(PromptKind.Defend));
        }

        [Test]
        public void DribblingLowersTrustButCreatingChancesRaisesIt()
        {
            int lowered = 0, raised = 0;
            for (int i = 0; i < 20; i++)
            {
                var e = new MatchEngine(Setup(i)) { Controller = new AutoPilot(i) };
                e.RunToEnd();
                lowered += e.Log.Count(x => x.type == MatchEventType.TrustChanged && x.value < 0 && x.text.Contains("ドリブル"));
                raised += e.Log.Count(x => x.type == MatchEventType.TrustChanged && x.value > 5 && x.text.Contains("チャンス"));
            }
            Assert.Greater(lowered, 0);
            Assert.Greater(raised, 0);
        }

        [Test]
        public void PassivePlayTanksTrust()
        {
            float normal = 0, passive = 0;
            for (int i = 0; i < 10; i++)
            {
                normal += new MatchEngine(Setup(i)) { Controller = new AutoPilot(i) }.RunToEnd().trust;
                passive += new MatchEngine(Setup(i)) { Controller = new AutoPilot(i) { passive = true } }.RunToEnd().trust;
            }
            Assert.Less(passive / 10, 25f, "消極的だと一気に下がる");
            Assert.Greater(normal / 10, passive / 10 + 30f);
        }

        [Test]
        public void TeammatesPassToTrustedProtagonistMore()
        {
            int lowTouches = 0, highTouches = 0;
            for (int i = 0; i < 10; i++)
            {
                lowTouches += TouchesWithFixedTrust(i, 5f);
                highTouches += TouchesWithFixedTrust(i, 95f);
            }
            Assert.Greater(highTouches, lowTouches);
        }

        static int TouchesWithFixedTrust(int seed, float trust)
        {
            var e = new MatchEngine(Setup(seed));
            var ctrl = new FixedTrustController(trust);
            e.Controller = ctrl;
            e.ChangeTrust(trust - e.Trust, "test");
            e.RunToEnd();
            return ctrl.attackPrompts;
        }

        class FixedTrustController : IMatchController
        {
            readonly float _trust;
            public int attackPrompts;
            public FixedTrustController(float t) { _trust = t; }
            public MatchChoice Decide(MatchEngine e, MatchPrompt p)
            {
                e.ChangeTrust(_trust - e.Trust, "test");
                if (p.kind == PromptKind.Attack)
                {
                    if (p.actor.isProtagonist) attackPrompts++;
                    return new MatchChoice(p.options.Contains(MatchCommand.Pass) ? MatchCommand.Pass : MatchCommand.Dribble);
                }
                return new MatchChoice(p.kind == PromptKind.Defend ? MatchCommand.PassCut : p.options[0]);
            }
        }

        // ---------------- ミッション ----------------

        [Test]
        public void CrampedProtagonistCanOnlyCoach()
        {
            var s = Setup(2);
            s.missions.Add(new CrampMission(30));
            var e = new MatchEngine(s);
            bool coached = false;
            while (true)
            {
                var p = e.Advance();
                if (p == null) break;
                if (e.Minute >= 31f) Assert.IsTrue(!p.actor.isProtagonist, "足がつった後、主人公はボールに関われない（味方への指示だけ）");
                e.Choose(p.options[0], p.passTargets.FirstOrDefault());
                if (e.Protagonist.cramped && !coached && e.CanCoach) coached = !e.Coach(CoachingCommand.GoForward).notAllowed;
            }
            // 試合終了まで指示だけで進める
            Assert.IsTrue(e.Protagonist.cramped);
            var r = e.Result();
            Assert.AreEqual(1, r.missions.Count);
            Assert.AreEqual(r.ProtagonistWon ? MissionStatus.Success : MissionStatus.Failed, r.missions[0].status);
        }

        [Test]
        public void CoachingWorksWhileCramped()
        {
            var s = Setup(3);
            s.missions.Add(new CrampMission(0));
            var e = new MatchEngine(s) { Controller = new AutoPilot() };
            int accepted = 0;
            e.OnEvent += ev => { if (ev.type == MatchEventType.Demand && e.CanCoach) { var r = e.Coach(ev.actor.demand ?? CoachingCommand.Praise, ev.actor); if (r.metDemand) accepted++; } };
            e.RunToEnd();
            Assert.IsTrue(e.Protagonist.cramped);
            Assert.Greater(accepted, 0, "要求に応える指示が出せる");
        }

        [Test]
        public void OffsideTrapMissionCountsTraps()
        {
            int best = 0;
            for (int i = 0; i < 10; i++)
            {
                var s = Setup(i, Position.DF);
                s.home.offsideTrapPlanned = true;
                s.missions.Add(new OffsideTrapMission(5));
                var e = new MatchEngine(s) { Controller = new AutoPilot(i) { trapper = true } };
                var r = e.RunToEnd();
                best = Math.Max(best, e.OffsideTrapsSucceeded);
                Assert.AreEqual(e.OffsideTrapsSucceeded >= 5 ? MissionStatus.Success : MissionStatus.Failed, r.missions[0].status);
                Assert.AreEqual(e.OffsideTrapsSucceeded, r.log.Count(x => x.type == MatchEventType.OffsideTrapSuccess && x.side == Side.Home));
            }
            Assert.Greater(best, 0);
        }

        [Test]
        public void ObviousMatchFixingGetsExposed()
        {
            float obvious = 0, subtle = 0;
            int exposed = 0;
            for (int i = 0; i < 10; i++)
            {
                var s1 = Setup(i); s1.crowd = 2000; s1.missions.Add(new ThrowMatchMission());
                var r1 = new MatchEngine(s1) { Controller = new AutoPilot(i) { throwing = true, subtle = false } }.RunToEnd();
                obvious += r1.suspicion;
                if (r1.fixingExposed) { exposed++; Assert.AreEqual(MissionStatus.Failed, r1.missions[0].status); }

                var s2 = Setup(i); s2.missions.Add(new ThrowMatchMission());
                subtle += new MatchEngine(s2) { Controller = new AutoPilot(i) { throwing = true, subtle = true } }.RunToEnd().suspicion;
            }
            Assert.Greater(exposed, 0, "露骨な八百長はばれる");
            Assert.Less(subtle, obvious);
            Assert.AreEqual(0f, subtle, 0.01f, "目立たない手抜きは疑われない");
        }

        [Test]
        public void ShowboatingProducesBeautifulSkills()
        {
            int normal = 0, flair = 0;
            for (int i = 0; i < 10; i++)
            {
                normal += new MatchEngine(Setup(i)) { Controller = new AutoPilot(i) }.RunToEnd().protagonist.beautifulSkills;
                flair += new MatchEngine(Setup(i)) { Controller = new AutoPilot(i) { showboat = true } }.RunToEnd().protagonist.beautifulSkills;
            }
            Assert.Greater(flair, normal);
        }

        [Test]
        public void PublicMatchesHideUndergroundStrength()
        {
            var db = Career().Population.Database;
            var hidden = db.npcs.FirstOrDefault(n => n.publicPerformanceRate < 0.9f);
            Assume.That(hidden == null ? 0 : 1, Is.GreaterThan(0));
            var pub = MatchFactory.FromNPC(hidden, false, new SeededRandom(1));
            var real = MatchFactory.FromNPC(hidden, true, new SeededRandom(1));
            Assert.Greater(real.stats.Overall(hidden.position), pub.stats.Overall(hidden.position));
        }

        // ---------------- 島への反映 ----------------

        [Test]
        public void ResultIsAppliedToTheIsland()
        {
            var c = FreshCareer();
            var wm = c.World.TodaysMatches().FirstOrDefault(m => m.awayLineup.Count > 0 && !m.IsUnderground)
                     ?? NormalMatches(c).First();
            while (c.Population.Database.currentDay < wm.day) c.AdvanceDay();

            var setup = MatchFactory.FromWorldMatch(c, wm, Side.Home);
            var r = new MatchEngine(setup) { Controller = new AutoPilot() }.RunToEnd();
            float fameBefore = c.Player.fame;
            MatchResultApplier.Apply(c, setup, r);

            Assert.IsTrue(wm.played);
            Assert.AreEqual(r.homeScore, wm.homeScore);
            Assert.AreEqual(1, c.Player.matchesPlayed);
            Assert.AreNotEqual(fameBefore, c.Player.fame);

            var report = c.AdvanceDay();
            Assert.IsTrue(!report.world.playedMatches.Contains(wm), "主人公が戦った試合は自動計算されない");
            Assert.AreEqual(r.homeScore, wm.homeScore);
        }

        [Test]
        public void ExposedFixingHitsFameAndNews_SuccessfulFixingHalvesLoan()
        {
            var c = FreshCareer();
            c.Player.fame = 40;
            c.Player.blackMarketAccess = true;
            var loan = c.Bank.Borrow(Lender.LoanShark, 1000000, 10);
            var ms = NormalMatches(c);

            var s = MatchFactory.FromWorldMatch(c, ms[0], Side.Home, 5);
            s.missions.Add(new ThrowMatchMission(loan.id));
            var exposed = new MatchResult { homeScore = 0, awayScore = 1, protagonistSide = Side.Home, suspicion = 100, fixingExposed = true, protagonist = s.home.Protagonist };
            MatchResultApplier.Apply(c, s, exposed);
            Assert.Less(c.Player.fame, 40f);
            Assert.IsTrue(c.AdvanceDay().newspaper.articles.Any(a => a.title.Contains("八百長")));
            Assert.Greater(loan.balance, 1000000 * 0.9);

            var s2 = MatchFactory.FromWorldMatch(c, ms[1], Side.Home, 6);
            s2.missions.Add(new ThrowMatchMission(loan.id));
            double before = loan.balance;
            var quiet = new MatchResult { homeScore = 0, awayScore = 2, protagonistSide = Side.Home, suspicion = 10, protagonist = s2.home.Protagonist };
            MatchResultApplier.Apply(c, s2, quiet);
            Assert.Less(Math.Abs(loan.balance - before / 2), 1.0, "八百長成功で借金が半分");
        }

        [Test]
        public void SessionRunsMeetingMatchAndApplies()
        {
            var c = FreshCareer();
            var wm = NormalMatches(c).First();
            var setup = MatchFactory.FromWorldMatch(c, wm, Side.Home, 3);
            setup.missions.Add(new WinMission());
            var session = new MatchSession(c, setup);
            Assert.IsTrue(session.Diagnose().reasons.Count > 0);
            var meeting = session.HoldMeeting(MeetingTopic.Enjoy);
            Assert.IsTrue(meeting != null && meeting.reactions.Count >= 10);

            var prompt = session.Advance();
            Assert.IsTrue(prompt != null, "主人公の出番が来る");
            var result = session.AutoPlay();
            Assert.AreEqual(MatchSession.Phase.Finished, session.Current);
            Assert.IsTrue(wm.played);
            Assert.AreEqual(1, result.missions.Count);
            Assert.AreEqual(1, c.Player.matchesPlayed);
        }

        // ---------------- キャプテン翼式（自チーム全員を操作） ----------------

        class Recorder : IMatchController
        {
            public readonly List<MatchPrompt> prompts = new List<MatchPrompt>();
            readonly AutoPilot _ap;
            public Recorder(int seed) { _ap = new AutoPilot(seed); }
            public MatchChoice Decide(MatchEngine e, MatchPrompt p) { prompts.Add(p); return _ap.Decide(e, p); }
        }

        [Test]
        public void FullTeamModeCommandsEveryoneIncludingKeeperAndAerials()
        {
            var kinds = new HashSet<PromptKind>();
            int teammatePrompts = 0;
            for (int i = 0; i < 6; i++)
            {
                var rec = new Recorder(i);
                var e = new MatchEngine(Setup(i)) { Controller = rec };
                Assert.AreEqual(ControlMode.FullTeam, e.Mode, "既定はキャプテン翼式");
                e.RunToEnd();
                foreach (var p in rec.prompts)
                {
                    kinds.Add(p.kind);
                    if (!p.actor.isProtagonist) teammatePrompts++;
                    Assert.IsTrue(e.Home.players.Contains(p.actor), "相手チームの選手は操作しない");
                    Assert.IsTrue(p.guts >= 0 && p.maxGuts > 0);
                }
            }
            Assert.Greater(teammatePrompts, 100);
            foreach (PromptKind k in Enum.GetValues(typeof(PromptKind)))
                Assert.IsTrue(kinds.Contains(k), k + " の場面がない");
        }

        static MatchSetup PersonalityTeams(SoccerPersonality sp, int seed)
        {
            var rng = new SeededRandom(seed);
            var home = MatchFactory.Generic("ホーム", 55, 11, rng);
            foreach (var p in home.players) if (p.position != Position.GK) p.soccerPersonality = sp;
            home.players.RemoveAt(home.players.Count - 1);
            home.players.Add(MatchFactory.FromProtagonist(new PlayerState()));
            var away = MatchFactory.Generic("アウェイ", 55, 11, rng);
            return new MatchSetup { home = home, away = away, protagonistSide = Side.Home, seed = seed };
        }

        class AlwaysPass : IMatchController
        {
            public MatchChoice Decide(MatchEngine e, MatchPrompt p)
            {
                if (p.kind == PromptKind.Attack && p.options.Contains(MatchCommand.Pass)) return new MatchChoice(MatchCommand.Pass);
                return new MatchChoice(p.options[0]);
            }
        }

        [Test]
        public void EgoistsIgnorePassOrdersButDevotedAlwaysObey()
        {
            int egoDisobey = 0, devotedDisobey = 0;
            for (int i = 0; i < 5; i++)
            {
                var e1 = new MatchEngine(PersonalityTeams(SoccerPersonality.Egoist, i + 1)) { Controller = new AlwaysPass() };
                e1.RunToEnd();
                egoDisobey += e1.Log.Count(x => x.type == MatchEventType.Disobey);
                var e2 = new MatchEngine(PersonalityTeams(SoccerPersonality.Devoted, i + 1)) { Controller = new AlwaysPass() };
                e2.RunToEnd();
                devotedDisobey += e2.Log.Count(x => x.type == MatchEventType.Disobey);
            }
            Assert.Greater(egoDisobey, 5, "エゴイストはパスの指示を無視して自分で行く");
            Assert.AreEqual(0, devotedDisobey, "献身的は必ず指示を聞く");
        }

        [Test]
        public void SpecialMovesAndCutIns()
        {
            var db = Career().Population.Database;
            var striker = db.npcs.First(n => n.abilities.shooting >= 70);
            var mp = MatchFactory.FromNPC(striker, true, new SeededRandom(1));
            Assert.IsTrue(mp.specials.Any(m => m.kind == SpecialKind.Shot), "シュートが得意な選手は必殺シュートを持つ");
            Assert.AreEqual(mp.specials[0].name, MatchFactory.FromNPC(striker, true, new SeededRandom(9)).specials[0].name, "技は毎回同じ");

            Assert.AreEqual(0, MatchFactory.ProtagonistSpecials(1).Count);
            Assert.AreEqual("移住者シュート", MatchFactory.ProtagonistSpecials(3)[0].name);

            int specialShots = 0, goalCutIns = 0, goals = 0;
            for (int i = 0; i < 10; i++)
            {
                var e = new MatchEngine(Setup(i)) { Controller = new AutoPilot(i) };
                e.RunToEnd();
                specialShots += e.Log.Count(x => x.type == MatchEventType.SpecialShot && x.cutIn && !string.IsNullOrEmpty(x.moveName));
                goals += e.Log.Count(x => x.type == MatchEventType.Goal);
                goalCutIns += e.Log.Count(x => x.type == MatchEventType.Goal && x.cutIn);
                Assert.IsTrue(!e.Log.Any(x => x.type == MatchEventType.Dribble && x.actor != null && x.actor.position == Position.GK), "キーパーはドリブルしない");
            }
            Assert.Greater(specialShots, 0);
            Assert.AreEqual(goals, goalCutIns, "ゴールには必ずカットイン");
        }

        [Test]
        public void ProtagonistLevelsUpAndLearnsSpecials()
        {
            for (int l = 1; l < 20; l++) Assert.Greater(PlayerGrowth.ExpToNext(l + 1), PlayerGrowth.ExpToNext(l));

            var c = FreshCareer();
            int shootBefore = c.Player.abilities.shooting;
            var learned = new List<SpecialMove>();
            var ms = NormalMatches(c);
            for (int i = 0; i < 8; i++)
            {
                var s = MatchFactory.FromWorldMatch(c, ms[i], Side.Home, i + 1);
                var session = new MatchSession(c, s);
                var r = session.AutoPlay();
                Assert.Greater(r.protagonistExp, 0);
                learned.AddRange(r.learnedSpecials);
            }
            Assert.GreaterOrEqual(c.Player.level, 3);
            Assert.Greater(c.Player.abilities.shooting, shootBefore);
            Assert.IsTrue(learned.Any(m => m.name == "移住者シュート"), "レベル3で必殺シュートを覚える");

            var pro = MatchFactory.FromProtagonist(c.Player);
            Assert.IsTrue(pro.specials.Any(m => m.kind == SpecialKind.Shot));
        }

        // ---------------- フォーメーション・作戦 ----------------

        [Test]
        public void FormationAssignsSlotsAndFlagsOutOfPosition()
        {
            var t = MatchFactory.Generic("テスト", 55, 11, new SeededRandom(2));
            foreach (var p in t.players) p.RememberNaturalPosition();
            FormationSystem.Apply(t, Formation.F541);
            Assert.AreEqual(5, t.players.Count(p => p.position == Position.DF));
            Assert.AreEqual(4, t.players.Count(p => p.position == Position.MF));
            Assert.AreEqual(1, t.players.Count(p => p.position == Position.FW));
            Assert.AreEqual(1, t.players.Count(p => p.position == Position.GK));
            Assert.IsTrue(t.players.Any(p => p.OutOfPosition), "FWが余るので誰かが本来と違う位置に入る");

            FormationSystem.Apply(t, Formation.F442);
            Assert.AreEqual(2, t.players.Count(p => p.position == Position.FW));
            Assert.IsTrue(!t.players.Any(p => p.OutOfPosition), "4-4-2 なら全員本来の位置");
        }

        static int CountEvents(TeamTactic tactic, MatchEventType type, out int goalsFor, out int goalsAgainst)
        {
            int n = 0; goalsFor = 0; goalsAgainst = 0;
            for (int i = 0; i < 12; i++)
            {
                var s = PersonalityTeams(SoccerPersonality.MoodMaker, i + 1);
                FormationSystem.Apply(s.home, Formation.F442);
                s.home.tactic = tactic;
                s.autoSelectCpuTactics = false;
                var e = new MatchEngine(s) { Controller = new TacticFollower() };
                var r = e.RunToEnd();
                n += r.log.Count(x => x.type == type && x.side == Side.Home);
                goalsFor += r.homeScore;
                goalsAgainst += r.awayScore;
            }
            return n;
        }

        /// <summary>作戦に沿ったコマンドを選ぶ（CPUと同じ傾向）。</summary>
        class TacticFollower : IMatchController
        {
            readonly AutoPilot _ap = new AutoPilot(5);
            public MatchChoice Decide(MatchEngine e, MatchPrompt p)
            {
                var team = e.ProtagonistTeam;
                if (p.kind == PromptKind.Attack && team.tactic == TeamTactic.WingAttack && p.options.Contains(MatchCommand.Centering)) return new MatchChoice(MatchCommand.Centering);
                if (p.kind == PromptKind.Defend && team.tactic == TeamTactic.ParkTheBus && p.options.Contains(MatchCommand.Block)) return new MatchChoice(MatchCommand.Block);
                return _ap.Decide(e, p);
            }
        }

        [Test]
        public void TacticsChangeHowTheTeamPlays()
        {
            int wingCrosses = CountEvents(TeamTactic.WingAttack, MatchEventType.Centering, out _, out _);
            int balancedCrosses = CountEvents(TeamTactic.Balanced, MatchEventType.Centering, out _, out _);
            Assert.Greater(wingCrosses, balancedCrosses, "サイド攻撃はセンタリングが増える");

            CountEvents(TeamTactic.ParkTheBus, MatchEventType.Goal, out _, out int conceded);
            CountEvents(TeamTactic.Balanced, MatchEventType.Goal, out _, out int concededBalanced);
            Assert.Less(conceded, concededBalanced, "ゴール前を固めると失点が減る");
        }

        [Test]
        public void TacticsHaveFansAndHaters()
        {
            var ego = P(Personality.Extrovert, SoccerPersonality.Egoist);
            var fighter = P(Personality.Extrovert, SoccerPersonality.Fighter);
            var t = new MatchTeam { players = { ego, fighter } };
            var r = FormationSystem.SetTactic(t, TeamTactic.ParkTheBus);
            Assert.Less(ego.tension, 60f, "エゴイストは守備的な作戦が嫌い");
            FormationSystem.SetTactic(t, TeamTactic.HighPress);
            Assert.Greater(fighter.tension, 60f, "ファイターはプレスが好き");
            Assert.AreEqual(TeamTactic.HighPress, t.tactic);
        }
    }
}
