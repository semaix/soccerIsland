using System;
using SoccerIsland.Career;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Match
{
    /// <summary>ミッションの成否によるご褒美・罰。</summary>
    public class MissionReward
    {
        public float fame;
        public long money;
        public int exp;
        public string text;
    }

    /// <summary>
    /// 試合イベントの演出係。主人公の試合の前に、状況に応じてミッションを1つ仕込む。
    /// ・闇金の八百長を引き受けている → 「八百長をしろ。わざと負けろ」
    /// ・大観衆の前で、そこそこ有名 → 「ファンが見てる。綺麗な足技で活躍しろ」
    /// ・33歳以上のDF → 「ベテランとしてオフサイドトラップを5回かけろ」
    /// ・疲れがたまっている → 後半に足がつる「指示だけで勝利しろ」
    /// ・大一番（地域対抗・島代表・大会） → 「勝利せよ」
    /// </summary>
    public static class EventDirector
    {
        public static IMatchMission Prepare(IslandCareer career, MatchSetup setup, SeededRandom rng)
        {
            if (setup.missions.Count > 0 || !setup.protagonistSide.HasValue) return null;
            var p = career.Player;
            IMatchMission m = null;

            if (p.acceptedFixLoanId >= 0)
            {
                m = new ThrowMatchMission(p.acceptedFixLoanId);
                p.acceptedFixLoanId = -1; // 1試合だけ
            }
            else if (p.fatigue >= 60f && rng.Chance(0.4f))
                m = new CrampMission(rng.Range(55, 75));
            else if (setup.crowd >= 1000 && p.fame >= 30f && !setup.underground && rng.Chance(0.35f))
                m = new FanShowcaseMission(3, 7f);
            else if (p.age >= 33 && p.position == Position.DF && rng.Chance(0.35f))
                m = new OffsideTrapMission(5);
            else if (setup.category == MatchCategory.RegionalDerby || setup.category == MatchCategory.IslandSelect || setup.crowd >= 3000)
                m = new WinMission();

            if (m != null) setup.missions.Add(m);
            return m;
        }

        /// <summary>ミッションの結果に応じたご褒美（MatchResultApplier から呼ぶ）。</summary>
        public static MissionReward Reward(MissionResult r, IMatchMission mission)
        {
            bool ok = r.status == MissionStatus.Success;
            if (mission is ThrowMatchMission)
                return ok ? new MissionReward { text = "……借金が半分になった。でも、胸のどこかが痛い。" } : new MissionReward { text = r.detail };
            if (mission is CrampMission)
                return ok ? new MissionReward { fame = 4f, exp = 40, text = "足がつっても、指示だけで勝ち切った！ 名将の誕生だ。" } : new MissionReward { exp = 10, text = "足がつって、勝たせられなかった……。" };
            if (mission is FanShowcaseMission)
                return ok ? new MissionReward { fame = 5f, exp = 30, text = "華麗な足技に、スタンドは総立ちだった！" } : new MissionReward { fame = -1f, text = "見せ場を作れず、ファンはがっかりして帰っていった。" };
            if (mission is OffsideTrapMission)
                return ok ? new MissionReward { fame = 3f, exp = 40, text = "老獪なライン統率。ベテランの真骨頂を見せた。" } : new MissionReward { exp = 10, text = "ラインがそろわなかった。まだまだ衰えるわけにはいかない。" };
            return ok ? new MissionReward { fame = 2f, money = 100000, exp = 20, text = "大一番を制した！ 勝利給が出た。" } : new MissionReward { text = "大一番を落とした……。" };
        }
    }
}
