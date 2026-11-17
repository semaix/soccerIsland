using System;
using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    /// <summary>NPC同士の関係。相関図の1本の線。</summary>
    [Serializable]
    public class NPCRelation
    {
        public int targetId;
        public RelationType type;
        /// <summary>-100（犬猿の仲）〜 100（親友）。</summary>
        public int affinity;

        public NPCRelation() { }

        public NPCRelation(int targetId, RelationType type, int affinity)
        {
            this.targetId = targetId;
            this.type = type;
            this.affinity = affinity;
        }
    }

    /// <summary>2Dアニメ調スプライトを組み立てるための見た目パラメータ。</summary>
    [Serializable]
    public class NPCAppearance
    {
        public int bodyType;   // 0:細身 1:普通 2:がっしり 3:小柄
        public int hairStyle;
        public int hairColor;
        public int skinTone;
        public int faceType;
        /// <summary>服装は職業で決まる（スーツ・作業着・白衣・制服・ヤクザスーツ…）。</summary>
        public Occupation outfit;
        public bool hasGlasses;
        public bool hasScar;
    }

    /// <summary>市場価値の履歴1件。</summary>
    [Serializable]
    public struct MarketValueRecord
    {
        public int day;
        public long value;

        public MarketValueRecord(int day, long value)
        {
            this.day = day;
            this.value = value;
        }
    }

    /// <summary>
    /// 島民1人分のデータ。
    /// JsonUtility でそのままセーブできるよう public フィールド + List で構成する（Dictionary 不使用）。
    /// </summary>
    [Serializable]
    public class NPCData
    {
        // ---------------- 基本情報 ----------------
        public int id;
        public string familyName;
        public string givenName;
        public string familyNameKana;
        public string givenNameKana;
        public Gender gender;
        public int birthYear;
        public int age;
        public Town hometown;
        public Occupation occupation;
        public string phoneNumber;
        public NPCAppearance appearance = new NPCAppearance();

        // ---------------- 性格 ----------------
        public Personality personality;
        public SoccerPersonality soccerPersonality;

        // ---------------- サッカー ----------------
        public Position position;
        /// <summary>所属クラブID。-1 は無所属。</summary>
        public int clubId = -1;
        /// <summary>サッカー協会に登録しているか。裏の人間は未登録で、公式ランキングに載らない。</summary>
        public bool registeredWithAssociation = true;
        /// <summary>裏社会側の人間か（裏市場・裏フットサル・裏トーナメントに出入りする）。</summary>
        public bool isUnderground;

        /// <summary>才能度 0-100（会話で判明。ランク表示）。</summary>
        public int talent;
        /// <summary>努力度 0-100（会話で判明。ランク表示）。</summary>
        public int effort;
        public GrowthType growthType;
        /// <summary>能力検定の級（ExamGrades）。-1 は未受験。</summary>
        public int examGrade = -1;
        /// <summary>検定を受けた年。</summary>
        public int examYear;

        // ---------------- お金 ----------------
        /// <summary>本業の年収（円）。</summary>
        public long jobIncome;
        /// <summary>サッカーの年俸（円）。</summary>
        public long salary;
        /// <summary>現在の市場価値（円）。スカウトにはこの額を払う必要がある。</summary>
        public long marketValue;
        public List<MarketValueRecord> marketValueHistory = new List<MarketValueRecord>();

        // ---------------- 人気・評判 ----------------
        /// <summary>知名度 0-100。高いとサイン・写真・ゴシップの対象になる。</summary>
        public int fame;
        /// <summary>話題性 -0.5〜+1.0。会話・噂・試合で上下し、市場価値に効く。時間で0へ戻る。</summary>
        public float hype;

        // ---------------- 相関 ----------------
        public List<NPCRelation> relations = new List<NPCRelation>();

        // ================ 以下 隠しパラメータ（プレイヤー非表示） ================
        public NPCAbilities abilities = new NPCAbilities();
        /// <summary>才能から決まる能力の上限（総合値の目安）。</summary>
        public int potential;
        /// <summary>各能力の到達上限。現在値 = 成長曲線(年齢) × 努力 × この値。</summary>
        public NPCAbilities statPotential = new NPCAbilities();
        /// <summary>覚醒型の覚醒年齢。それ以外は0。</summary>
        public int awakeningAge;
        /// <summary>覚醒済みか（覚醒型のみ意味を持つ）。</summary>
        public bool hasAwakened;
        /// <summary>人生何周目か（転生の裏設定）。本人も気づいていない。1 = 1周目。</summary>
        public int lifeCycle = 1;
        /// <summary>表の試合では手を抜いている／裏でだけ本気を出す。表で見える能力倍率。</summary>
        public float publicPerformanceRate = 1f;
        /// <summary>服役中か。</summary>
        public bool isIncarcerated;
        /// <summary>出所予定の年（服役中のみ）。</summary>
        public int releaseYear;
        /// <summary>漂流者で島から出られないか。</summary>
        public bool isStranded;

        // ---------------- 便利プロパティ（シリアライズ対象外） ----------------
        public string FullName => familyName + " " + givenName;
        public string FullNameKana => familyNameKana + " " + givenNameKana;
        public int Overall => abilities.Overall(position);
        /// <summary>表の試合で見せる総合力（裏で本気を出すタイプは低く見える）。</summary>
        public int PublicOverall => (int)Math.Round(Overall * publicPerformanceRate);
        public bool IsSpecialPersonality => soccerPersonality >= SoccerPersonality.God;
        public bool AppearsInOfficialRanking => registeredWithAssociation && !isUnderground && !isIncarcerated;

        public NPCRelation FindRelation(int targetId)
        {
            for (int i = 0; i < relations.Count; i++)
                if (relations[i].targetId == targetId) return relations[i];
            return null;
        }

        public bool HasRelation(int targetId) => FindRelation(targetId) != null;

        public override string ToString() =>
            $"#{id} {FullName}({age}) {NPCLabels.Of(occupation)} / {NPCLabels.Of(hometown)} / {NPCLabels.Of(soccerPersonality)} / {NPCLabels.Yen(marketValue)}";
    }
}
