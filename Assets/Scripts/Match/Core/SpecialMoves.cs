using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Match
{
    public enum SpecialKind
    {
        Shot,    // 必殺シュート
        Dribble, // 必殺ドリブル
        Tackle,  // 必殺タックル
        Block,   // 必殺ブロック
        Save,    // 必殺セーブ（キーパー）
    }

    /// <summary>選手ごとの必殺技。使うとガッツを大きく消費し、カットインが入る。</summary>
    [Serializable]
    public class SpecialMove
    {
        public string name;
        public SpecialKind kind;
        /// <summary>通常技への上乗せ。</summary>
        public int power;
        public int gutsCost;
        /// <summary>技を出すときの叫び。</summary>
        public string shout;

        public SpecialMove() { }
        public SpecialMove(string name, SpecialKind kind, int power, int gutsCost, string shout)
        {
            this.name = name; this.kind = kind; this.power = power; this.gutsCost = gutsCost; this.shout = shout;
        }
    }

    /// <summary>
    /// 島民の必殺技を能力・性格から決める（同じ島民はいつも同じ技）。技名はすべて島にちなんだオリジナル。
    /// </summary>
    public static class SpecialMoveLibrary
    {
        static readonly string[] ShotNames = { "潮騒シュート", "灯台キャノン", "珊瑚礁ループ", "台風ボレー", "夕凪ショット", "黒潮ドライブ", "大漁一本釣りシュート", "渦潮スピン" };
        static readonly string[] DribbleNames = { "波乗りドリブル", "蜃気楼ステップ", "引き潮フェイント", "カモメ舞い" };
        static readonly string[] TackleNames = { "荒波スライディング", "高潮タックル", "岩礁クラッシュ" };
        static readonly string[] BlockNames = { "防波堤ブロック", "鉄壁の岬", "人間テトラポッド" };
        static readonly string[] SaveNames = { "大漁ネットセーブ", "灯台守キャッチ", "港の守り神" };

        public static List<SpecialMove> ForNPC(NPCData n, NPCAbilities stats)
        {
            var list = new List<SpecialMove>();
            int seed = n.id * 31 + 7;

            bool genius = n.soccerPersonality == SoccerPersonality.God || n.soccerPersonality == SoccerPersonality.Gifted;
            if (stats.shooting >= 68 || genius || (n.soccerPersonality == SoccerPersonality.Sense && stats.shooting >= 58))
                list.Add(new SpecialMove(ShotNames[Pick(ref seed, ShotNames.Length)], SpecialKind.Shot, 14 + (stats.shooting - 60) / 3, 160, "くらえ！"));
            if (stats.dribbling >= 70 || (n.soccerPersonality == SoccerPersonality.Sense && stats.dribbling >= 60))
                list.Add(new SpecialMove(DribbleNames[Pick(ref seed, DribbleNames.Length)], SpecialKind.Dribble, 12, 120, "抜かせてもらう！"));
            if (stats.defense >= 70 || (n.soccerPersonality == SoccerPersonality.Fighter && stats.defense >= 60))
                list.Add(new SpecialMove(TackleNames[Pick(ref seed, TackleNames.Length)], SpecialKind.Tackle, 12, 110, "止める！"));
            if (stats.defense >= 66 && stats.physical >= 62)
                list.Add(new SpecialMove(BlockNames[Pick(ref seed, BlockNames.Length)], SpecialKind.Block, 12, 110, "ここは通さない！"));
            if (n.position == Position.GK && stats.goalkeeping >= 66)
                list.Add(new SpecialMove(SaveNames[Pick(ref seed, SaveNames.Length)], SpecialKind.Save, 14, 140, "取った！"));
            if (n.soccerPersonality == SoccerPersonality.God)
                list.Add(new SpecialMove("天啓", SpecialKind.Shot, 30, 220, "……見えた。"));
            return list;
        }

        static int Pick(ref int seed, int len) => Math.Abs(seed++) % len;

        /// <summary>主人公がレベルで覚える必殺技。</summary>
        public static SpecialMove ForProtagonistLevel(int level)
        {
            switch (level)
            {
                case 3: return new SpecialMove("移住者シュート", SpecialKind.Shot, 14, 160, "島に来た意味を見せてやる！");
                case 5: return new SpecialMove("潮風ドリブル", SpecialKind.Dribble, 12, 120, "風に乗る！");
                case 8: return new SpecialMove("防波堤スライディング", SpecialKind.Tackle, 12, 110, "行かせるか！");
                case 12: return new SpecialMove("黒潮ボレー", SpecialKind.Shot, 22, 190, "これが島の流れだ！");
                case 16: return new SpecialMove("人生二周目のシュート", SpecialKind.Shot, 30, 220, "……なぜだろう、初めてじゃない気がする。");
                default: return null;
            }
        }

        public static SpecialMove Find(List<SpecialMove> moves, SpecialKind kind)
        {
            SpecialMove best = null;
            if (moves == null) return null;
            foreach (var m in moves) if (m.kind == kind && (best == null || m.power > best.power)) best = m;
            return best;
        }
    }
}
