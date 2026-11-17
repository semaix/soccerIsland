using System.Collections.Generic;
using SoccerIsland.Career;
using SoccerIsland.NPC;

namespace SoccerIsland.Match
{
    /// <summary>
    /// 1試合の流れ（ミーティング → 試合 → 結果反映）をまとめる。UIはこれだけ触ればよい（Unity非依存）。
    /// </summary>
    public class MatchSession
    {
        public enum Phase { Meeting, Playing, Finished }

        public IslandCareer Career { get; }
        public MatchSetup Setup { get; }
        public MatchEngine Engine { get; private set; }
        public Phase Current { get; private set; } = Phase.Meeting;
        public MeetingResult MeetingResult { get; private set; }
        public MatchResult Result { get; private set; }

        public MatchSession(IslandCareer career, MatchSetup setup)
        {
            Career = career;
            Setup = setup;
        }

        /// <summary>主人公のチームの今日の試合から作る（なければ null）。</summary>
        public static MatchSession ForTodaysTeamMatch(IslandCareer career, params IMatchMission[] missions)
        {
            var m = MatchFactory.TodaysTeamMatch(career);
            if (m == null) return null;
            var setup = MatchFactory.FromWorldMatch(career, m);
            setup.missions.AddRange(missions);
            return new MatchSession(career, setup);
        }

        public MatchTeam MyTeam => Setup.protagonistSide.HasValue
            ? (Setup.protagonistSide == Side.Home ? Setup.home : Setup.away) : null;

        /// <summary>チームの性格バランス（ミーティング前に見せる）。</summary>
        public ChemistryReport Diagnose() => MyTeam == null ? new ChemistryReport() : TeamChemistry.Evaluate(MyTeam.players);

        public MeetingResult HoldMeeting(MeetingTopic topic, MatchPlayer ace = null, long bonus = 0)
        {
            if (Current != Phase.Meeting || MyTeam == null) return null;
            if (topic == MeetingTopic.BonusPromise && !Career.Player.TrySpend(bonus, "勝利ボーナスの約束", Career.Population.Database.currentDay))
                return null;
            MeetingResult = Meeting.Hold(MyTeam, topic, new SeededRandom(Setup.seed ^ 77), ace);
            return MeetingResult;
        }

        /// <summary>フォーメーションを決める（ミーティング中）。</summary>
        public void SetFormation(Formation f)
        {
            if (Current == Phase.Meeting && MyTeam != null) FormationSystem.Apply(MyTeam, f);
        }

        /// <summary>作戦を決める（ミーティング中）。性格による反応が返る。</summary>
        public List<MeetingReaction> SetTactic(TeamTactic t)
        {
            if (Current != Phase.Meeting || MyTeam == null) return new List<MeetingReaction>();
            return FormationSystem.SetTactic(MyTeam, t);
        }

        public MatchEngine Kickoff()
        {
            if (Engine != null) return Engine;
            Engine = new MatchEngine(Setup);
            Current = Phase.Playing;
            return Engine;
        }

        /// <summary>主人公の判断が必要になるまで進める。null なら試合終了。</summary>
        public MatchPrompt Advance()
        {
            if (Engine == null) Kickoff();
            var p = Engine.Advance();
            if (p == null && Engine.Finished && Current != Phase.Finished) Finish();
            return p;
        }

        public void Choose(MatchCommand c, MatchPlayer target = null) => Engine.Choose(c, target);

        public CoachingResult Coach(CoachingCommand c, MatchPlayer target = null) => Engine.Coach(c, target);

        /// <summary>残りをおまかせで進める。</summary>
        public MatchResult AutoPlay(IMatchController controller = null)
        {
            if (Engine == null) Kickoff();
            Engine.Controller = controller ?? new AutoPilot(Setup.seed);
            Engine.Advance();
            if (Current != Phase.Finished) Finish();
            return Result;
        }

        void Finish()
        {
            Result = Engine.Result();
            MatchResultApplier.Apply(Career, Setup, Result);
            Current = Phase.Finished;
        }

        public IReadOnlyList<MatchEvent> Log => Engine != null ? (IReadOnlyList<MatchEvent>)Engine.Log : new List<MatchEvent>();
    }
}
