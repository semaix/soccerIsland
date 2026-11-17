using System;
using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    [Serializable]
    public class WeightedOccupation
    {
        public Occupation occupation;
        public float weight = 1f;

        public WeightedOccupation() { }
        public WeightedOccupation(Occupation o, float w) { occupation = o; weight = w; }
    }

    /// <summary>街ごとの雰囲気・人口・性格傾向。</summary>
    [Serializable]
    public class TownProfile
    {
        public Town town;
        public string description;
        /// <summary>人口比率（相対値）。</summary>
        public float populationShare = 1f;

        /// <summary>この街で特に多い職業（職業のベース重みに掛ける倍率）。</summary>
        public List<WeightedOccupation> occupationMultipliers = new List<WeightedOccupation>();

        // 性格の出やすさ（内向的・外交的・根暗）
        public float introvertWeight = 1f;
        public float extrovertWeight = 1f;
        public float gloomyWeight = 1f;

        // サッカー性格の出やすさ（通常6種）
        public float moodMakerWeight = 1f;
        public float egoistWeight = 1f;
        public float senseWeight = 1f;
        public float hystericWeight = 1f;
        public float fighterWeight = 1f;
        public float devotedWeight = 1f;

        /// <summary>才能度・努力度の平均に足す値。</summary>
        public float talentBias;
        public float effortBias;

        /// <summary>この街の町クラブ名。</summary>
        public List<string> townClubNames = new List<string>();

        public float OccupationMultiplier(Occupation o)
        {
            foreach (var w in occupationMultipliers)
                if (w.occupation == o) return w.weight;
            return 1f;
        }
    }

    /// <summary>職業ごとの年収・年齢制限・裏社会率など。</summary>
    [Serializable]
    public class OccupationProfile
    {
        public Occupation occupation;
        /// <summary>島全体での出現しやすさ。0なら通常抽選に出ない（受刑者・漂流者は別枠）。</summary>
        public float baseWeight = 1f;
        public int minAge = 18;
        public int maxAge = 70;
        /// <summary>年収レンジ（万円）。</summary>
        public int incomeMinMan = 200;
        public int incomeMaxMan = 600;
        /// <summary>女性の出やすさの倍率（1=男女同じ）。</summary>
        public float femaleMultiplier = 1f;
        /// <summary>裏の人間である確率。</summary>
        public float undergroundChance = 0.02f;
        /// <summary>職場チームの名前（空ならなし）。</summary>
        public string workplaceClubName = "";
        public Town workplaceClubTown;

        public OccupationProfile() { }

        public OccupationProfile(Occupation o, float weight, int minAge, int maxAge, int incomeMin, int incomeMax,
            float female = 1f, float underground = 0.02f)
        {
            occupation = o;
            baseWeight = weight;
            this.minAge = minAge;
            this.maxAge = maxAge;
            incomeMinMan = incomeMin;
            incomeMaxMan = incomeMax;
            femaleMultiplier = female;
            undergroundChance = underground;
        }

        public OccupationProfile WithClub(string name, Town town)
        {
            workplaceClubName = name;
            workplaceClubTown = town;
            return this;
        }
    }

    /// <summary>NPC生成の設定一式。Unity側では NPCGenerationConfig(ScriptableObject) がこれを持つ。</summary>
    [Serializable]
    public class NPCGenerationSettings
    {
        public int seed = 20261117;
        public int npcCount = 1000;
        public int startYear = 2026;

        public float femaleRatio = 0.42f;
        public int minAge = 10;
        public int maxAge = 78;

        /// <summary>刑務所にいるNPCの数（刑務所リーグ用）。</summary>
        public int prisonerCount = 24;
        /// <summary>島に漂流して出られないNPCの数。</summary>
        public int castawayCount = 6;

        // ---- 特殊性格 ----
        public float godChance = 0.001f;
        public float giftedChance = 0.004f;
        public float phantomChance = 0.002f;
        public int guaranteedGods = 1;
        public int guaranteedGifted = 3;
        public int guaranteedPhantoms = 1;

        // ---- 成長タイプの出やすさ ----
        public float earlyWeight = 20f;
        public float normalWeight = 40f;
        public float lateWeight = 18f;
        public float awakeningWeight = 8f;
        public float longevityWeight = 14f;

        // ---- 才能・努力 ----
        public float talentMean = 45f;
        public float talentStdDev = 17f;
        public float effortMean = 50f;
        public float effortStdDev = 20f;

        // ---- 関係生成 ----
        public float familyChance = 0.25f;
        public float rivalChance = 0.18f;
        public float mentorChance = 0.12f;
        public int maxCoworkerLinks = 2;
        public int maxFriendLinks = 3;

        /// <summary>裏の人間のうち「表では下手に見える」割合。</summary>
        public float hiddenStrengthChance = 0.3f;

        public List<TownProfile> towns = new List<TownProfile>();
        public List<OccupationProfile> occupations = new List<OccupationProfile>();

        public TownProfile GetTown(Town t)
        {
            foreach (var p in towns) if (p.town == t) return p;
            return null;
        }

        public OccupationProfile GetOccupation(Occupation o)
        {
            foreach (var p in occupations) if (p.occupation == o) return p;
            return null;
        }

        /// <summary>ゲームの基本設定。towns/occupations が空のときもこれで埋める。</summary>
        public static NPCGenerationSettings CreateDefault()
        {
            var s = new NPCGenerationSettings();
            s.FillDefaultsIfEmpty();
            return s;
        }

        public void FillDefaultsIfEmpty()
        {
            if (towns == null || towns.Count == 0) towns = DefaultTowns();
            if (occupations == null || occupations.Count == 0) occupations = DefaultOccupations();
        }

        public static List<TownProfile> DefaultTowns()
        {
            return new List<TownProfile>
            {
                new TownProfile
                {
                    town = Town.Yushin,
                    description = "人を救うのが好きな、気の強い街。病院と交番が多い。",
                    populationShare = 1.0f,
                    occupationMultipliers =
                    {
                        new WeightedOccupation(Occupation.Doctor, 3f),
                        new WeightedOccupation(Occupation.Nurse, 3f),
                        new WeightedOccupation(Occupation.Police, 3f),
                        new WeightedOccupation(Occupation.Firefighter, 3f),
                        new WeightedOccupation(Occupation.Lifeguard, 2f),
                        new WeightedOccupation(Occupation.Mafia, 0.3f),
                    },
                    introvertWeight = 0.8f, extrovertWeight = 1.3f, gloomyWeight = 0.6f,
                    devotedWeight = 2.0f, fighterWeight = 1.6f, hystericWeight = 1.2f, egoistWeight = 0.5f,
                    effortBias = 5f,
                    townClubNames = { "勇心レスキューズ", "勇心ハートブレイカーズ", "勇心ジャスティスFC" },
                },
                new TownProfile
                {
                    town = Town.Furugane,
                    description = "人情的な旧街。田舎でヤンキーが多い。",
                    populationShare = 1.0f,
                    occupationMultipliers =
                    {
                        new WeightedOccupation(Occupation.Construction, 3f),
                        new WeightedOccupation(Occupation.Fisherman, 3f),
                        new WeightedOccupation(Occupation.Farmer, 3f),
                        new WeightedOccupation(Occupation.Retired, 1.5f),
                        new WeightedOccupation(Occupation.Unemployed, 1.8f),
                        new WeightedOccupation(Occupation.Mafia, 1.5f),
                        new WeightedOccupation(Occupation.Salaryman, 0.5f),
                        new WeightedOccupation(Occupation.Banker, 0.3f),
                    },
                    introvertWeight = 1.0f, extrovertWeight = 1.0f, gloomyWeight = 0.8f,
                    fighterWeight = 2.2f, hystericWeight = 1.6f, moodMakerWeight = 1.4f, senseWeight = 0.7f,
                    talentBias = 2f,
                    townClubNames = { "古鐘ヤンキース", "古鐘ばってん組", "古鐘港シーガルズ" },
                },
                new TownProfile
                {
                    town = Town.Kirahama,
                    description = "お店や飲み屋がいっぱいある垢抜けた街。チャラい。",
                    populationShare = 1.1f,
                    occupationMultipliers =
                    {
                        new WeightedOccupation(Occupation.ShopClerk, 3f),
                        new WeightedOccupation(Occupation.ConvenienceClerk, 2f),
                        new WeightedOccupation(Occupation.Bartender, 3f),
                        new WeightedOccupation(Occupation.Chef, 2f),
                        new WeightedOccupation(Occupation.Hairdresser, 3f),
                        new WeightedOccupation(Occupation.Idol, 4f),
                        new WeightedOccupation(Occupation.Mafia, 1.2f),
                        new WeightedOccupation(Occupation.Farmer, 0.2f),
                    },
                    introvertWeight = 0.6f, extrovertWeight = 2.0f, gloomyWeight = 0.5f,
                    senseWeight = 2.0f, egoistWeight = 1.5f, moodMakerWeight = 1.5f, devotedWeight = 0.6f,
                    effortBias = -8f, talentBias = 3f,
                    townClubNames = { "煌浜ナイトスターズ", "煌浜パーリーピーポーFC", "煌浜サンセットSC" },
                },
                new TownProfile
                {
                    town = Town.Tenpou,
                    description = "意識が高い、島1番というプライドがある街。",
                    populationShare = 0.9f,
                    occupationMultipliers =
                    {
                        new WeightedOccupation(Occupation.Salaryman, 2.5f),
                        new WeightedOccupation(Occupation.Banker, 3f),
                        new WeightedOccupation(Occupation.Doctor, 1.5f),
                        new WeightedOccupation(Occupation.Teacher, 2f),
                        new WeightedOccupation(Occupation.Reporter, 2f),
                        new WeightedOccupation(Occupation.Construction, 0.4f),
                        new WeightedOccupation(Occupation.Unemployed, 0.3f),
                        new WeightedOccupation(Occupation.Mafia, 0.5f),
                    },
                    introvertWeight = 1.3f, extrovertWeight = 1.0f, gloomyWeight = 0.9f,
                    egoistWeight = 2.2f, senseWeight = 1.2f, hystericWeight = 1.3f, fighterWeight = 0.6f,
                    effortBias = 10f, talentBias = 1f,
                    townClubNames = { "天峰ロイヤルズ", "天峰台エリートアカデミー", "天峰プライドFC" },
                },
            };
        }

        public static List<OccupationProfile> DefaultOccupations()
        {
            return new List<OccupationProfile>
            {
                // 学生は年齢で自動決定するので baseWeight は大学生向け
                new OccupationProfile(Occupation.Student,         2.0f, 10, 22,   0,   80),
                new OccupationProfile(Occupation.Salaryman,       6.0f, 22, 65, 300,  900).WithClub("天峰サラリーマンズ", Town.Tenpou),
                new OccupationProfile(Occupation.Construction,    4.0f, 18, 68, 300,  700, 0.2f, 0.04f).WithClub("古鐘土建ユナイテッド", Town.Furugane),
                new OccupationProfile(Occupation.Doctor,          1.2f, 26, 75, 900, 2500).WithClub("勇心病院FC", Town.Yushin),
                new OccupationProfile(Occupation.Nurse,           2.0f, 21, 65, 350,  600, 3.0f).WithClub("勇心病院FC", Town.Yushin),
                new OccupationProfile(Occupation.ShopClerk,       3.5f, 16, 65, 200,  450, 1.6f),
                new OccupationProfile(Occupation.ConvenienceClerk,2.0f, 16, 70, 150,  300, 1.0f, 0.03f),
                new OccupationProfile(Occupation.Mafia,           1.2f, 18, 75, 300, 5000, 0.3f, 1.0f),
                new OccupationProfile(Occupation.Police,          2.0f, 20, 62, 400,  800, 0.5f, 0.01f).WithClub("勇心警察SC", Town.Yushin),
                new OccupationProfile(Occupation.Firefighter,     1.2f, 20, 60, 400,  750, 0.3f).WithClub("勇心消防ファイヤーズ", Town.Yushin),
                new OccupationProfile(Occupation.Fisherman,       2.5f, 16, 78, 250,  900, 0.3f, 0.05f).WithClub("古鐘漁協FC", Town.Furugane),
                new OccupationProfile(Occupation.Farmer,          2.0f, 16, 78, 200,  500, 0.8f),
                new OccupationProfile(Occupation.Teacher,         1.5f, 23, 65, 400,  700, 1.2f),
                new OccupationProfile(Occupation.Bartender,       1.5f, 20, 70, 250,  600, 0.8f, 0.2f),
                new OccupationProfile(Occupation.Chef,            1.5f, 18, 72, 250,  800, 0.7f),
                new OccupationProfile(Occupation.Hairdresser,     1.2f, 19, 65, 250,  600, 1.8f),
                new OccupationProfile(Occupation.Banker,          1.2f, 22, 65, 500, 1500, 0.9f, 0.03f),
                new OccupationProfile(Occupation.Reporter,        0.8f, 22, 70, 350,  800),
                new OccupationProfile(Occupation.Lifeguard,       0.8f, 18, 50, 250,  500).WithClub("煌浜ビーチガーディアンズ", Town.Kirahama),
                new OccupationProfile(Occupation.Idol,            0.5f, 15, 35, 100, 3000, 3.0f),
                new OccupationProfile(Occupation.Unemployed,      1.5f, 18, 78,   0,  100, 0.8f, 0.15f),
                new OccupationProfile(Occupation.Retired,         0.0f, 63, 78, 150,  400),
                new OccupationProfile(Occupation.Prisoner,        0.0f, 18, 70,   0,    0, 0.25f, 0.6f),
                new OccupationProfile(Occupation.Castaway,        0.0f, 16, 60,   0,   50, 0.6f),
            };
        }
    }
}
