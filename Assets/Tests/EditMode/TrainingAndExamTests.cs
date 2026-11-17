using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class TrainingAndExamTests
    {
        // このファイルは各システムの動きを見るので、場所の制限（同じ場所にいる人としか話せない等）は外す
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault()))) { requirePresence = false };

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
        public void TrainingWithAPartnerGivesImpressions()
        {
            var c = NewCareer();
            var partner = c.Population.Database.npcs.First(n => n.abilities.dribbling >= 75);
            Assert.IsTrue(!c.Training.Train(TrainingMenu.Dribbling, partner.id).done, "仲良くないと一緒に練習できない");
            c.Population.Knowledge.GetOrCreate(partner.id).affinity = 50;
            var r = c.Training.Train(TrainingMenu.Dribbling, partner.id);
            Assert.IsTrue(r.done);
            Assert.IsTrue(r.impressions.Count >= 1 && r.impressions.All(x => x.Contains(partner.FullName)));
            Assert.IsTrue(!string.IsNullOrEmpty(r.partnerLine));
            Assert.IsTrue(!r.Impression.Any(char.IsDigit), "数値は出さない");
            Assert.AreEqual(53, c.Population.Knowledge.Get(partner.id).affinity);
            Assert.IsTrue(c.Population.Knowledge.Knows(partner.id, NPCInfoFlags.Effort));
        }

        [Test]
        public void ImpressionsMatchTheAbility()
        {
            var rng = new SeededRandom(1);
            Assert.AreEqual(0, TrainingComments.Tier(95, 60, 0));
            Assert.AreEqual(1, TrainingComments.Tier(75, 60, 0));
            Assert.AreEqual(4, TrainingComments.Tier(25, 60, 0));

            // 足が速い相手と走り込むと、速さの感想が出る
            var c = NewCareer();
            var fast = c.Population.Database.npcs.Where(n => !n.isIncarcerated).OrderByDescending(n => n.abilities.speed).First();
            c.Population.Knowledge.Reveal(fast.id, NPCInfoFlags.Basic);
            c.Population.Knowledge.GetOrCreate(fast.id).affinity = 80;
            var r = c.Training.Train(TrainingMenu.Running, fast.id);
            Assert.IsTrue(r.impressions[0].Contains("速") || r.impressions[0].Contains("消えた") || r.impressions[0].Contains("追いつけない") || r.impressions[0].Contains("ぶっちぎ"), r.impressions[0]);

            // 全部の能力・段階にセリフがある
            for (int stat = 0; stat < NPCAbilities.StatCount; stat++)
                for (int tier = 0; tier < 5; tier++)
                    Assert.IsTrue(TrainingComments.Line(stat, tier, "X", rng).Contains("X"));
        }
    }
}
