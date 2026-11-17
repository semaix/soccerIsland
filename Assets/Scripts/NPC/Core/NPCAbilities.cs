using System;

namespace SoccerIsland.NPC
{
    /// <summary>
    /// 隠し能力値（1-99）。プレイヤーには表示しない。
    /// スカウトは会話・試合観戦・能力検定などの間接情報から推測する。
    /// </summary>
    [Serializable]
    public class NPCAbilities
    {
        public int shooting;    // シュート
        public int passing;     // パス
        public int dribbling;   // ドリブル
        public int technique;   // テクニック（足技の綺麗さ）
        public int defense;     // ディフェンス
        public int physical;    // フィジカル
        public int speed;       // スピード
        public int stamina;     // スタミナ
        public int mental;      // メンタル
        public int goalkeeping; // キーパー

        public const int StatCount = 10;
        public const int Min = 1;
        public const int Max = 99;

        public int this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return shooting;
                    case 1: return passing;
                    case 2: return dribbling;
                    case 3: return technique;
                    case 4: return defense;
                    case 5: return physical;
                    case 6: return speed;
                    case 7: return stamina;
                    case 8: return mental;
                    case 9: return goalkeeping;
                    default: throw new IndexOutOfRangeException();
                }
            }
            set
            {
                value = Clamp(value);
                switch (index)
                {
                    case 0: shooting = value; break;
                    case 1: passing = value; break;
                    case 2: dribbling = value; break;
                    case 3: technique = value; break;
                    case 4: defense = value; break;
                    case 5: physical = value; break;
                    case 6: speed = value; break;
                    case 7: stamina = value; break;
                    case 8: mental = value; break;
                    case 9: goalkeeping = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }

        public static int Clamp(int v) => v < Min ? Min : (v > Max ? Max : v);

        /// <summary>ポジションごとの重み付き総合力。</summary>
        public int Overall(Position position)
        {
            float[] w = Weights(position);
            float sum = 0f, wsum = 0f;
            for (int i = 0; i < StatCount; i++)
            {
                sum += this[i] * w[i];
                wsum += w[i];
            }
            return (int)Math.Round(sum / wsum);
        }

        // shoot, pass, dribble, tech, def, phys, speed, stamina, mental, gk
        static readonly float[] GKWeights = { 0.0f, 0.5f, 0.0f, 0.3f, 0.5f, 0.6f, 0.3f, 0.3f, 1.0f, 4.0f };
        static readonly float[] DFWeights = { 0.2f, 0.7f, 0.3f, 0.4f, 3.0f, 1.6f, 1.0f, 1.0f, 1.0f, 0.0f };
        static readonly float[] MFWeights = { 0.8f, 2.4f, 1.3f, 1.6f, 0.9f, 0.7f, 0.8f, 1.4f, 1.0f, 0.0f };
        static readonly float[] FWWeights = { 3.0f, 0.8f, 1.6f, 1.2f, 0.2f, 1.0f, 1.6f, 0.6f, 1.0f, 0.0f };

        public static float[] Weights(Position p)
        {
            switch (p)
            {
                case Position.GK: return GKWeights;
                case Position.DF: return DFWeights;
                case Position.MF: return MFWeights;
                default: return FWWeights;
            }
        }

        public NPCAbilities Clone() => (NPCAbilities)MemberwiseClone();
    }
}
