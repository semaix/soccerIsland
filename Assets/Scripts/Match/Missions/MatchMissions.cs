namespace SoccerIsland.Match
{
    /// <summary>試合中のイベント（ミッション）。</summary>
    public interface IMatchMission
    {
        string Title { get; }
        /// <summary>八百長用のコマンド（わざと外す等）を出すか。</summary>
        bool AllowsFixingCommands { get; }
        void OnStart(MatchEngine engine);
        void OnMinute(MatchEngine engine, int minute);
        void OnEvent(MatchEngine engine, MatchEvent e);
        MissionResult Evaluate(MatchEngine engine, MatchResult result);
    }

    public abstract class MatchMission : IMatchMission
    {
        public abstract string Title { get; }
        public virtual bool AllowsFixingCommands => false;
        public virtual void OnStart(MatchEngine engine) { }
        public virtual void OnMinute(MatchEngine engine, int minute) { }
        public virtual void OnEvent(MatchEngine engine, MatchEvent e) { }
        public abstract MissionResult Evaluate(MatchEngine engine, MatchResult result);

        protected MissionResult Done(bool ok, string detail) =>
            new MissionResult { title = Title, status = ok ? MissionStatus.Success : MissionStatus.Failed, detail = detail };
    }

    /// <summary>勝て。</summary>
    public class WinMission : MatchMission
    {
        public override string Title => "勝利せよ";
        public override MissionResult Evaluate(MatchEngine e, MatchResult r) =>
            Done(r.ProtagonistWon, $"{r.homeScore}-{r.awayScore}");
    }

    /// <summary>足がつった状態で、指示だけでプレイして勝利しろ。</summary>
    public class CrampMission : MatchMission
    {
        public int crampMinute;
        public CrampMission(int crampMinute = 20) { this.crampMinute = crampMinute; }
        public override string Title => "足がつった……指示だけで勝利しろ";
        public override void OnStart(MatchEngine e) { if (crampMinute <= 0) e.CrampProtagonist(); }
        public override void OnMinute(MatchEngine e, int minute) { if (minute >= crampMinute) e.CrampProtagonist(); }
        public override MissionResult Evaluate(MatchEngine e, MatchResult r) =>
            Done(r.ProtagonistWon && r.protagonist != null && r.protagonist.cramped, r.ProtagonistWon ? "指示だけで勝ち切った！" : "勝てなかった");
    }

    /// <summary>ファンが見てる。綺麗な足技で活躍しろ。</summary>
    public class FanShowcaseMission : MatchMission
    {
        public int requiredSkills;
        public float requiredRating;
        public FanShowcaseMission(int skills = 3, float rating = 7f) { requiredSkills = skills; requiredRating = rating; }
        public override string Title => $"ファンが見てる。綺麗な足技で活躍しろ（{requiredSkills}回）";
        public override MissionResult Evaluate(MatchEngine e, MatchResult r)
        {
            var p = r.protagonist;
            if (p == null) return Done(false, "出場していない");
            return Done(p.beautifulSkills >= requiredSkills && p.rating >= requiredRating,
                        $"足技 {p.beautifulSkills}回 / 評価 {p.rating:0.0}");
        }
    }

    /// <summary>ベテランとしてオフサイドトラップを N 回かけろ。</summary>
    public class OffsideTrapMission : MatchMission
    {
        public int required;
        public OffsideTrapMission(int required = 5) { this.required = required; }
        public override string Title => $"ベテランとしてオフサイドトラップを{required}回かけろ";
        public override MissionResult Evaluate(MatchEngine e, MatchResult r) =>
            Done(e.OffsideTrapsSucceeded >= required, $"{e.OffsideTrapsSucceeded}回成功");
    }

    /// <summary>
    /// 八百長をしろ。わざと負けろ。
    /// わざと外す・パスミスは疑いが溜まり、100でばれる。目立たないように手を抜くのがコツ。
    /// </summary>
    public class ThrowMatchMission : MatchMission
    {
        /// <summary>闇金の借金と連動させるときのローンID（-1 なら無関係）。</summary>
        public int loanId;
        public ThrowMatchMission(int loanId = -1) { this.loanId = loanId; }
        public override string Title => "八百長をしろ。わざと負けろ";
        public override bool AllowsFixingCommands => true;
        public override MissionResult Evaluate(MatchEngine e, MatchResult r)
        {
            if (r.fixingExposed) return Done(false, "八百長がばれた！");
            return Done(r.ProtagonistLost, r.ProtagonistLost ? $"負けた（疑い {r.suspicion:0}）" : "勝ってしまった／引き分け");
        }
    }
}
