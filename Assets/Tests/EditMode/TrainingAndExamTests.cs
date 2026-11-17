using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class TrainingAndExamTests
    {
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));

        [Test]
        public void GradeThresholds()
        {
            Assert.AreEqual(ExamGrades.Dan, ExamGrades.FromOverall(95));
            Assert.AreEqual(1, ExamGrades.FromOverall(85));
            Assert.AreEqual(5, ExamGrades.FromOverall(60));
            Assert.AreEqual(10, ExamGrades.FromOverall(5));
            Assert.AreEqual("初段", ExamGrades.Label(0));
            Assert.AreEqual("3級", ExamGrades.Label(3));
            Assert.IsTrue(ExamGrades.IsBetter(2, 5) && !ExamGrades.IsBetter(5, 2) && ExamGrades.IsBetter(9, -1));
        }

        [Test]
        public void IslandersHavePastGradesThatCanBeOutdated()
        {
            var db = NewCareer().Population.Database;
            var graded = db.npcs.Where(n => n.examGrade >= 0).ToList();
            Assert.Greater(graded.Count, 200);
            Assert.IsTrue(graded.All(n => n.registeredWithAssociation && !n.isUnderground), "裏の人間は検定を受けない");
            // 覚醒した人は、覚醒前に受けた級のままだと今の実力より低い
            int outdated = graded.Count(n => n.examGrade > ExamGrades.FromOverall(n.PublicOverall));
            Assert.Greater(outdated, 0, "級が今の実力より低い掘り出し物がいる");
        }

        [Test]
        public void TalkingRevealsExamGrade()
        {
            var c = NewCareer();
            var npc = c.Population.Database.npcs.First(n => n.examGrade >= 0 && n.personality == Personality.Extrovert);
            for (int d = 0; d < 6; d++) { c.Population.Talk(npc.id); c.AdvanceDay(); }
            var p = c.Population.GetProfile(npc.id);
            Assert.IsTrue(p.examGrade.StartsWith(ExamGrades.Label(npc.examGrade)), p.examGrade);
        }

        [Test]
        public void MonthlyExamUpdatesGradesAndMakesNews()
        {
            var c = NewCareer();
            var db = c.Population.Database;
            var bloomer = db.npcs.Where(n => n.AppearsInOfficialRanking && n.age >= 15 && n.age <= 40).OrderByDescending(n => n.PublicOverall).First();
            bloomer.examGrade = 10;
            var results = c.Exam.RunMonthlyExam(1f);
            var r = results.First(x => x.npcId == bloomer.id);
            Assert.IsTrue(r.BigJump);
            Assert.AreEqual(db.currentYear, bloomer.examYear);

            // 検定日には新聞に載る
            while (!AbilityExam.IsExamDay(db.currentDay)) c.AdvanceDay();
            var report = c.AdvanceDay();
            Assert.IsTrue(report.exams != null && report.exams.Count > 0);
        }

        [Test]
        public void ProtagonistExam()
        {
            var c = NewCareer();
            long cash = c.Player.cash;
            var r = c.Exam.TakeExam();
            Assert.IsTrue(r != null);
            Assert.AreEqual(cash - c.Exam.fee, c.Player.cash);
            Assert.AreEqual(r.after, c.Player.examGrade);
            Assert.IsTrue(c.Exam.TakeExam() == null, "30日に1回");
        }

        [Test]
        public void TrainingRaisesStatsAndTiresYouOut()
        {
            var c = NewCareer();
            int before = c.Player.abilities.shooting;
            int ups = 0;
            for (int d = 0; d < 10; d++)
            {
                for (int k = 0; k < 3; k++) ups += c.Training.Train(TrainingMenu.Shooting).statUps.Count(s => s == 0);
                Assert.IsTrue(!c.Training.Train(TrainingMenu.Shooting).done, "1日3回まで");
                c.AdvanceDay();
            }
            Assert.Greater(c.Player.abilities.shooting, before);
            Assert.GreaterOrEqual(c.Player.abilities.shooting - before, ups); // 練習の経験値でレベルアップした分も上がる

            c.Player.fatigue = 95f;
            Assert.IsTrue(!c.Training.Train(TrainingMenu.Running).done, "疲れすぎ");
            int tiredGuts = MatchFactory.FromProtagonist(c.Player).maxGuts;
            c.Player.fatigue = 0f;
            Assert.Greater(MatchFactory.FromProtagonist(c.Player).maxGuts, tiredGuts, "疲れているとガッツが減る");
        }

        [Test]
        public void TrainingWithAPartnerGivesAnImpression()
        {
            var c = NewCareer();
            var partner = c.Population.Database.npcs.First(n => n.abilities.dribbling >= 75);
            Assert.IsTrue(!c.Training.Train(TrainingMenu.Dribbling, partner.id).done, "仲良くないと一緒に練習できない");
            c.Population.Knowledge.GetOrCreate(partner.id).affinity = 50;
            var r = c.Training.Train(TrainingMenu.Dribbling, partner.id);
            Assert.IsTrue(r.done);
            Assert.IsTrue(r.impression.Contains(partner.FullName));
            Assert.IsTrue(r.impression.Contains("上手") || r.impression.Contains("次元"), r.impression);
            Assert.AreEqual(53, c.Population.Knowledge.Get(partner.id).affinity);
            Assert.IsTrue(c.Population.Knowledge.Knows(partner.id, NPCInfoFlags.Effort));
        }
    }
}
