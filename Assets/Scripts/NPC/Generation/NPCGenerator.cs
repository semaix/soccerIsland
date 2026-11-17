using System;
using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    /// <summary>
    /// 島民を一括生成する。同じ settings.seed からは必ず同じ島ができる。
    ///
    /// 生成手順:
    ///  1. クラブ生成（町クラブ・職場チーム・裏フットサル・刑務所・ビーチ）
    ///  2. 島民生成（街 → 年齢 → 性別 → 職業 → 性格 → 才能/努力/成長 → 隠し能力）
    ///  3. 特殊性格（ゴッド・ギフテッド・幻影）の保証
    ///  4. クラブ所属
    ///  5. 関係生成（家族・同級生・同僚・親分子分・ライバル・師弟・友人）
    ///  6. 知名度・市場価値・年俸
    /// </summary>
    public class NPCGenerator
    {
        readonly NPCGenerationSettings _s;
        readonly SeededRandom _rng;
        readonly HashSet<string> _usedPhones = new HashSet<string>();
        readonly HashSet<string> _usedNames = new HashSet<string>();

        NPCDatabase _db;
        readonly Dictionary<Town, List<SoccerClub>> _townClubs = new Dictionary<Town, List<SoccerClub>>();
        readonly Dictionary<string, SoccerClub> _workplaceClubs = new Dictionary<string, SoccerClub>();
        readonly List<SoccerClub> _undergroundClubs = new List<SoccerClub>();
        SoccerClub _prisonClub;
        SoccerClub _beachClub;

        public NPCGenerator(NPCGenerationSettings settings)
        {
            _s = settings ?? NPCGenerationSettings.CreateDefault();
            _s.FillDefaultsIfEmpty();
            _rng = new SeededRandom(_s.seed);
        }

        public static NPCDatabase Generate(NPCGenerationSettings settings) => new NPCGenerator(settings).Generate();

        public NPCDatabase Generate()
        {
            _db = new NPCDatabase { seed = _s.seed, currentYear = _s.startYear, currentDay = 0 };

            CreateClubs();

            int special = Math.Min(_s.npcCount, _s.prisonerCount + _s.castawayCount);
            int prisoners = Math.Min(_s.prisonerCount, special);
            int castaways = special - prisoners;
            int normal = _s.npcCount - special;

            for (int i = 0; i < normal; i++) _db.Add(CreateResident(SpecialKind.None));
            for (int i = 0; i < prisoners; i++) _db.Add(CreateResident(SpecialKind.Prisoner));
            for (int i = 0; i < castaways; i++) _db.Add(CreateResident(SpecialKind.Castaway));

            GuaranteeSpecialPersonalities();
            foreach (var npc in _db.npcs) FinalizeAbilities(npc);

            AssignClubs();
            _db.RebuildIndex();

            BuildRelations();
            AssignClubLeaders();

            foreach (var npc in _db.npcs)
            {
                npc.fame = RollFame(npc);
                npc.marketValue = MarketValueCalculator.Calculate(npc);
                npc.salary = MarketValueCalculator.CalculateSalary(npc);
                npc.marketValueHistory.Add(new MarketValueRecord(0, npc.marketValue));
            }

            _db.RebuildIndex();
            return _db;
        }

        enum SpecialKind { None, Prisoner, Castaway }

        // ==================================================================
        // 1. クラブ
        // ==================================================================

        void CreateClubs()
        {
            int nextId = 0;
            foreach (var town in _s.towns)
            {
                var list = new List<SoccerClub>();
                foreach (var name in town.townClubNames)
                {
                    var c = new SoccerClub { id = nextId++, name = name, town = town.town, category = ClubCategory.Town };
                    _db.AddClub(c);
                    list.Add(c);
                }
                _townClubs[town.town] = list;
            }

            foreach (var occ in _s.occupations)
            {
                if (string.IsNullOrEmpty(occ.workplaceClubName) || _workplaceClubs.ContainsKey(occ.workplaceClubName)) continue;
                var category = occ.occupation == Occupation.Lifeguard ? ClubCategory.Beach : ClubCategory.Workplace;
                var c = new SoccerClub { id = nextId++, name = occ.workplaceClubName, town = occ.workplaceClubTown, category = category };
                _db.AddClub(c);
                _workplaceClubs[c.name] = c;
                if (category == ClubCategory.Beach) _beachClub = c;
            }

            if (_beachClub == null)
            {
                _beachClub = new SoccerClub { id = nextId++, name = "煌浜ビーチボーイズ", town = Town.Kirahama, category = ClubCategory.Beach };
                _db.AddClub(_beachClub);
            }

            // 裏フットサル：協会未加盟
            string[] underNames = { "黒潮會フットサル部", "ミッドナイト・アンダーグラウンド", "廃墟ファントムズ" };
            Town[] underTowns = { Town.Furugane, Town.Kirahama, Town.Tenpou };
            for (int i = 0; i < underNames.Length; i++)
            {
                var c = new SoccerClub { id = nextId++, name = underNames[i], town = underTowns[i], category = ClubCategory.Underground, registeredWithAssociation = false };
                _db.AddClub(c);
                _undergroundClubs.Add(c);
            }

            _prisonClub = new SoccerClub { id = nextId++, name = "島刑務所 鉄格子FC", town = Town.Furugane, category = ClubCategory.Prison, registeredWithAssociation = false };
            _db.AddClub(_prisonClub);
        }

        // ==================================================================
        // 2. 島民
        // ==================================================================

        NPCData CreateResident(SpecialKind kind)
        {
            var npc = new NPCData { id = _db.Count };

            npc.hometown = PickTown();
            var townProfile = _s.GetTown(npc.hometown);

            npc.age = RollAge(kind);
            npc.birthYear = _s.startYear - npc.age;

            npc.occupation = kind == SpecialKind.Prisoner ? Occupation.Prisoner
                           : kind == SpecialKind.Castaway ? Occupation.Castaway
                           : PickOccupation(npc.age, townProfile);

            var occProfile = _s.GetOccupation(npc.occupation);
            npc.gender = RollGender(occProfile);

            AssignName(npc, kind == SpecialKind.Castaway);

            npc.personality = PickPersonality(townProfile);
            npc.soccerPersonality = PickSoccerPersonality(townProfile, npc.age);
            npc.position = PickPosition(npc.soccerPersonality);

            npc.talent = _rng.GaussianInt(_s.talentMean + (townProfile?.talentBias ?? 0f), _s.talentStdDev, 1, 100);
            // ごく稀に突出した才能
            if (_rng.Chance(0.01f)) npc.talent = _rng.Range(90, 101);
            npc.effort = _rng.GaussianInt(_s.effortMean + (townProfile?.effortBias ?? 0f), _s.effortStdDev, 1, 100);
            npc.growthType = PickGrowthType();
            if (npc.growthType == GrowthType.Awakening) npc.awakeningAge = _rng.Range(18, 34);

            npc.jobIncome = RollIncome(occProfile, npc.age);

            // 裏の人間
            float undergroundChance = occProfile?.undergroundChance ?? 0.02f;
            npc.isUnderground = _rng.Chance(undergroundChance);
            npc.registeredWithAssociation = !npc.isUnderground || _rng.Chance(0.3f);
            if (npc.isUnderground && _rng.Chance(_s.hiddenStrengthChance))
                npc.publicPerformanceRate = _rng.Range(0.55f, 0.8f);

            if (kind == SpecialKind.Prisoner)
            {
                npc.isIncarcerated = true;
                npc.registeredWithAssociation = false;
                npc.releaseYear = _s.startYear + _rng.Range(1, 8);
                // 刑務所には天才がいる
                if (_rng.Chance(0.15f)) npc.talent = Math.Max(npc.talent, _rng.Range(80, 100));
            }
            if (kind == SpecialKind.Castaway)
            {
                npc.isStranded = true;
                npc.registeredWithAssociation = false;
                npc.talent = Math.Max(npc.talent, _rng.Range(40, 95)); // 海の向こうから来た実力者
            }

            // 人生何周目か（裏設定）。ほとんどは1周目
            npc.lifeCycle = _rng.Chance(0.05f) ? (_rng.Chance(0.2f) ? 3 : 2) : 1;

            if (kind != SpecialKind.Prisoner && kind != SpecialKind.Castaway) npc.phoneNumber = NewPhoneNumber();
            else npc.phoneNumber = "";

            npc.appearance = RollAppearance(npc);
            return npc;
        }

        Town PickTown()
        {
            var w = new float[_s.towns.Count];
            for (int i = 0; i < w.Length; i++) w[i] = _s.towns[i].populationShare;
            return _s.towns[_rng.WeightedIndex(w)].town;
        }

        static readonly int[] AgeBracketMin = { 10, 15, 18, 23, 30, 40, 50, 60 };
        static readonly int[] AgeBracketMax = { 14, 17, 22, 29, 39, 49, 59, 78 };
        static readonly float[] AgeBracketWeight = { 6, 7, 12, 18, 22, 15, 10, 10 };

        int RollAge(SpecialKind kind)
        {
            if (kind == SpecialKind.Prisoner) return _rng.Range(19, 56);
            if (kind == SpecialKind.Castaway) return _rng.Range(16, 45);
            int b = _rng.WeightedIndex(AgeBracketWeight);
            int min = Math.Max(_s.minAge, AgeBracketMin[b]);
            int max = Math.Min(_s.maxAge, AgeBracketMax[b]);
            return _rng.Range(min, max + 1);
        }

        Occupation PickOccupation(int age, TownProfile town)
        {
            if (age < 18) return age >= 16 && _rng.Chance(0.15f) ? Occupation.ConvenienceClerk : Occupation.Student;
            if (age >= 63 && _rng.Chance(0.55f)) return Occupation.Retired;

            var candidates = new List<KeyValuePair<Occupation, float>>();
            foreach (var p in _s.occupations)
            {
                if (p.baseWeight <= 0f || age < p.minAge || age > p.maxAge) continue;
                float w = p.baseWeight * (town?.OccupationMultiplier(p.occupation) ?? 1f);
                // 20代前半は学生が多い
                if (p.occupation == Occupation.Student) w *= age <= 22 ? 4f : 0f;
                candidates.Add(new KeyValuePair<Occupation, float>(p.occupation, w));
            }
            return candidates.Count == 0 ? Occupation.Unemployed : _rng.WeightedPick(candidates);
        }

        Gender RollGender(OccupationProfile occ)
        {
            float f = _s.femaleRatio;
            float m = occ?.femaleMultiplier ?? 1f;
            float p = f * m / (f * m + (1f - f));
            return _rng.Chance(p) ? Gender.Female : Gender.Male;
        }

        void AssignName(NPCData npc, bool foreigner)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (foreigner)
                {
                    int i = _rng.Range(0, NPCNamePool.CastawayCount);
                    npc.familyName = npc.familyNameKana = NPCNamePool.CastawayNames[i, 0];
                    npc.givenName = npc.givenNameKana = NPCNamePool.CastawayNames[i, 1];
                }
                else
                {
                    int f = _rng.Range(0, NPCNamePool.FamilyCount);
                    npc.familyName = NPCNamePool.FamilyNames[f, 0];
                    npc.familyNameKana = NPCNamePool.FamilyNames[f, 1];
                    var pool = npc.gender == Gender.Male ? NPCNamePool.MaleGivenNames : NPCNamePool.FemaleGivenNames;
                    int g = _rng.Range(0, pool.GetLength(0));
                    npc.givenName = pool[g, 0];
                    npc.givenNameKana = pool[g, 1];
                }
                if (_usedNames.Add(npc.FullName)) return;
            }
            // 同姓同名は許容（島なので珍しくない）
            _usedNames.Add(npc.FullName);
        }

        Personality PickPersonality(TownProfile t)
        {
            var w = t == null ? new[] { 1f, 1f, 1f } : new[] { t.introvertWeight, t.extrovertWeight, t.gloomyWeight };
            return (Personality)_rng.WeightedIndex(w);
        }

        SoccerPersonality PickSoccerPersonality(TownProfile t, int age)
        {
            double r = _rng.NextDouble();
            // ゴッド・ギフテッドは現役世代のみ。幻影（謎のプレーヤー）は老人のこともある
            if (age <= 40)
            {
                if (r < _s.godChance) return SoccerPersonality.God;
                r -= _s.godChance;
                if (r < _s.giftedChance) return SoccerPersonality.Gifted;
                r -= _s.giftedChance;
            }
            if (r < _s.phantomChance) return SoccerPersonality.Phantom;

            var w = t == null
                ? new[] { 1f, 1f, 1f, 1f, 1f, 1f }
                : new[] { t.moodMakerWeight, t.egoistWeight, t.senseWeight, t.hystericWeight, t.fighterWeight, t.devotedWeight };
            return (SoccerPersonality)_rng.WeightedIndex(w); // enum の並び順 MoodMaker..Devoted と一致
        }

        Position PickPosition(SoccerPersonality p)
        {
            // GK, DF, MF, FW
            float[] w = { 10, 32, 33, 25 };
            switch (p)
            {
                case SoccerPersonality.Egoist: w[3] *= 2.2f; w[0] *= 0.5f; break;
                case SoccerPersonality.Sense: w[2] *= 1.6f; w[3] *= 1.6f; w[0] *= 0.3f; break;
                case SoccerPersonality.Fighter: w[1] *= 2.0f; w[0] *= 1.3f; break;
                case SoccerPersonality.Devoted: w[1] *= 1.5f; w[2] *= 1.5f; w[3] *= 0.5f; break;
                case SoccerPersonality.Hysteric: w[1] *= 1.3f; break;
                case SoccerPersonality.MoodMaker: w[2] *= 1.3f; break;
            }
            return (Position)_rng.WeightedIndex(w);
        }

        GrowthType PickGrowthType()
        {
            float[] w = { _s.earlyWeight, _s.normalWeight, _s.lateWeight, _s.awakeningWeight, _s.longevityWeight };
            return (GrowthType)_rng.WeightedIndex(w);
        }

        long RollIncome(OccupationProfile p, int age)
        {
            if (p == null || p.incomeMaxMan <= 0) return 0;
            // 経験年数で上がる。ピークは50代
            float exp = Math.Max(0f, Math.Min(1f, (age - p.minAge) / 30f));
            float t = Math.Max(0f, Math.Min(1f, exp * 0.7f + _rng.Value * 0.3f));
            // マフィアは格差が激しい
            if (p.occupation == Occupation.Mafia) t = (float)Math.Pow(_rng.Value, 3);
            long man = (long)Math.Round(p.incomeMinMan + (p.incomeMaxMan - p.incomeMinMan) * t);
            return man * 10000;
        }

        string NewPhoneNumber()
        {
            while (true)
            {
                string prefix = _rng.Chance(0.5f) ? "090" : (_rng.Chance(0.5f) ? "080" : "070");
                string phone = $"{prefix}-{_rng.Range(0, 10000):D4}-{_rng.Range(0, 10000):D4}";
                if (_usedPhones.Add(phone)) return phone;
            }
        }

        NPCAppearance RollAppearance(NPCData npc)
        {
            var a = new NPCAppearance
            {
                bodyType = _rng.Range(0, 4),
                hairStyle = _rng.Range(0, 24),
                hairColor = _rng.Range(0, 12),
                skinTone = _rng.Range(0, 6),
                faceType = _rng.Range(0, 16),
                outfit = npc.occupation,
                hasGlasses = _rng.Chance(npc.occupation == Occupation.Banker || npc.occupation == Occupation.Doctor ? 0.4f : 0.12f),
                hasScar = _rng.Chance(npc.occupation == Occupation.Mafia || npc.occupation == Occupation.Prisoner ? 0.4f : 0.03f),
            };
            if (npc.occupation == Occupation.Construction || npc.occupation == Occupation.Fisherman || npc.soccerPersonality == SoccerPersonality.Fighter)
                a.bodyType = 2;
            if (npc.age < 14) a.bodyType = 3;
            return a;
        }

        // ==================================================================
        // 3. 特殊性格の保証 / 隠し能力
        // ==================================================================

        void GuaranteeSpecialPersonalities()
        {
            Guarantee(SoccerPersonality.God, _s.guaranteedGods, n => n.age >= 16 && n.age <= 40);
            Guarantee(SoccerPersonality.Gifted, _s.guaranteedGifted, n => n.age >= 12 && n.age <= 30);
            Guarantee(SoccerPersonality.Phantom, _s.guaranteedPhantoms, n => n.age >= 18 && n.age <= 45);
        }

        void Guarantee(SoccerPersonality p, int count, Predicate<NPCData> eligible)
        {
            int have = 0;
            foreach (var n in _db.npcs) if (n.soccerPersonality == p) have++;
            var pool = _db.Query(n => !n.IsSpecialPersonality && n.occupation != Occupation.Castaway && eligible(n));
            _rng.Shuffle(pool);
            for (int i = 0; have < count && i < pool.Count; i++, have++)
                pool[i].soccerPersonality = p;
        }

        void FinalizeAbilities(NPCData npc)
        {
            switch (npc.soccerPersonality)
            {
                case SoccerPersonality.God:
                    npc.talent = 100;
                    npc.potential = _rng.Range(97, 100);
                    break;
                case SoccerPersonality.Gifted:
                    npc.talent = Math.Max(npc.talent, _rng.Range(92, 100));
                    npc.potential = _rng.Range(92, 99);
                    break;
                case SoccerPersonality.Phantom:
                    npc.talent = Math.Max(npc.talent, _rng.Range(85, 100));
                    npc.potential = _rng.Range(88, 97);
                    npc.registeredWithAssociation = false;
                    npc.publicPerformanceRate = 0.5f; // 誰も本気を見たことがない
                    break;
                default:
                    npc.potential = Clamp((int)Math.Round(38 + npc.talent * 0.6f + _rng.Gaussian(0f, 5f)), 20, 99);
                    // 覚醒型は「才能度」（本人・周囲の認識）と関係なく、本当の上限が高い
                    if (npc.growthType == GrowthType.Awakening)
                        npc.potential = Math.Max(npc.potential, _rng.Range(72, 97));
                    break;
            }

            float[] posBias = PositionStatBias(npc.position);
            float[] persBias = PersonalityStatBias(npc.soccerPersonality);
            for (int i = 0; i < NPCAbilities.StatCount; i++)
            {
                float v = npc.potential + posBias[i] + persBias[i] + _rng.Gaussian(0f, 6f);
                npc.statPotential[i] = Clamp((int)Math.Round(v), 5, 99);
            }
            GrowthCurve.RecalculateAbilities(npc);
        }

        // shoot, pass, dribble, tech, def, phys, speed, stamina, mental, gk
        static float[] PositionStatBias(Position p)
        {
            switch (p)
            {
                case Position.GK: return new float[] { -30, -10, -25, -15, -5, 0, -10, -10, 3, 5 };
                case Position.DF: return new float[] { -15, -3, -10, -8, 5, 3, 0, 0, 0, -45 };
                case Position.MF: return new float[] { -5, 5, 0, 3, -5, -5, -2, 3, 0, -45 };
                default:          return new float[] { 5, -5, 3, 0, -20, 0, 3, -5, 0, -45 };
            }
        }

        static float[] PersonalityStatBias(SoccerPersonality p)
        {
            switch (p)
            {
                case SoccerPersonality.Egoist:    return new float[] { 5, -5, 4, 0, -5, 0, 0, 0, 3, 0 };
                case SoccerPersonality.Sense:     return new float[] { 2, 3, 5, 8, -5, -5, 0, -3, 0, 0 };
                case SoccerPersonality.Hysteric:  return new float[] { 0, 0, 0, 0, 2, 3, 0, 0, -8, 0 };
                case SoccerPersonality.Fighter:   return new float[] { 0, -3, -3, -5, 5, 8, 0, 4, 2, 0 };
                case SoccerPersonality.Devoted:   return new float[] { -5, 4, -3, 0, 3, 0, 0, 6, 3, 0 };
                case SoccerPersonality.MoodMaker: return new float[] { 0, 2, 0, 0, 0, 0, 0, 2, 5, 0 };
                default:                          return new float[10];
            }
        }

        // ==================================================================
        // 4. クラブ所属
        // ==================================================================

        void AssignClubs()
        {
            foreach (var npc in _db.npcs)
            {
                if (npc.isIncarcerated) { npc.clubId = _prisonClub.id; continue; }
                if (npc.age >= 72 && _rng.Chance(0.5f)) { npc.clubId = -1; continue; } // 引退

                if (npc.isUnderground && _undergroundClubs.Count > 0 && (npc.occupation == Occupation.Mafia || _rng.Chance(0.5f)))
                {
                    npc.clubId = PickUndergroundClub(npc.hometown).id;
                    continue;
                }

                var occ = _s.GetOccupation(npc.occupation);
                if (occ != null && !string.IsNullOrEmpty(occ.workplaceClubName) && npc.age >= 18 && _rng.Chance(0.6f)
                    && _workplaceClubs.TryGetValue(occ.workplaceClubName, out var work))
                {
                    npc.clubId = work.id;
                    continue;
                }

                if (npc.occupation == Occupation.Castaway || (npc.hometown == Town.Kirahama && _rng.Chance(0.08f)))
                {
                    npc.clubId = _beachClub.id;
                    continue;
                }

                npc.clubId = _rng.Pick(_townClubs[npc.hometown]).id;
            }
        }

        SoccerClub PickUndergroundClub(Town town)
        {
            foreach (var c in _undergroundClubs) if (c.town == town && _rng.Chance(0.6f)) return c;
            return _rng.Pick(_undergroundClubs);
        }

        void AssignClubLeaders()
        {
            foreach (var club in _db.clubs)
            {
                NPCData best = null;
                float bestScore = float.MinValue;
                foreach (var m in _db.GetClubMembers(club.id))
                {
                    float score = m.Overall + Math.Min(m.age, 45) * 0.5f + (m.occupation == Occupation.Mafia ? 20 : 0);
                    if (score > bestScore) { bestScore = score; best = m; }
                }
                if (best == null) continue;
                club.leaderId = best.id;

                // 裏クラブはボスと子分の関係
                if (club.category == ClubCategory.Underground)
                {
                    foreach (var m in _db.GetClubMembers(club.id))
                        if (m != best) AddMutual(m, best, RelationType.Boss, RelationType.Subordinate, _rng.Range(-20, 80));
                }
            }
        }

        // ==================================================================
        // 5. 関係（相関図）
        // ==================================================================

        void BuildRelations()
        {
            BuildFamilies();
            BuildClassmates();
            BuildCoworkers();
            BuildRivalsAndMentors();
            BuildFriends();
        }

        void BuildFamilies()
        {
            foreach (var npc in _db.npcs)
            {
                if (npc.occupation == Occupation.Castaway || !_rng.Chance(_s.familyChance)) continue;
                var town = _db.GetByTown(npc.hometown);
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var other = town[_rng.Range(0, town.Count)];
                    if (other == npc || other.occupation == Occupation.Castaway || npc.HasRelation(other.id)) continue;
                    int diff = Math.Abs(other.age - npc.age);
                    bool sibling = diff <= 8;
                    bool parent = diff >= 20 && diff <= 40;
                    if (!sibling && !parent) continue;

                    // 年下側が年上側の苗字を名乗る
                    var younger = other.age < npc.age ? other : npc;
                    var elder = younger == npc ? other : npc;
                    younger.familyName = elder.familyName;
                    younger.familyNameKana = elder.familyNameKana;
                    AddMutual(npc, other, RelationType.Family, RelationType.Family, _rng.Range(10, 90));
                    break;
                }
            }
        }

        /// <summary>同じ街・同じ生まれ年は同級生。小さい頃から一緒にボールを蹴ってきた仲。</summary>
        void BuildClassmates()
        {
            var groups = new Dictionary<long, List<NPCData>>();
            foreach (var npc in _db.npcs)
            {
                if (npc.occupation == Occupation.Castaway) continue;
                long key = (long)npc.hometown * 10000 + npc.birthYear;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<NPCData>();
                list.Add(npc);
            }
            foreach (var list in groups.Values)
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                        AddMutual(list[i], list[j], RelationType.Classmate, RelationType.Classmate, _rng.GaussianInt(20, 30, -80, 100));
        }

        void BuildCoworkers()
        {
            foreach (var npc in _db.npcs)
            {
                if (npc.occupation == Occupation.Student || npc.occupation == Occupation.Unemployed ||
                    npc.occupation == Occupation.Retired || npc.occupation == Occupation.Castaway) continue;
                var same = _db.GetByOccupation(npc.occupation);
                int links = 0;
                for (int attempt = 0; attempt < 10 && links < _s.maxCoworkerLinks; attempt++)
                {
                    var other = same[_rng.Range(0, same.Count)];
                    if (other == npc || other.hometown != npc.hometown && npc.occupation != Occupation.Prisoner) continue;
                    if (npc.HasRelation(other.id)) continue;
                    AddMutual(npc, other, RelationType.Coworker, RelationType.Coworker, _rng.GaussianInt(10, 30, -80, 90));
                    links++;
                }
            }
        }

        void BuildRivalsAndMentors()
        {
            var all = _db.npcs;
            foreach (var npc in all)
            {
                if (_rng.Chance(_s.rivalChance))
                {
                    for (int attempt = 0; attempt < 15; attempt++)
                    {
                        var other = all[_rng.Range(0, all.Count)];
                        if (other == npc || npc.HasRelation(other.id)) continue;
                        if (Math.Abs(other.age - npc.age) > 4 || other.position != npc.position) continue;
                        // 別の街・別のクラブ同士だとライバル意識が強い
                        int affinity = other.hometown != npc.hometown ? _rng.Range(-90, -20) : _rng.Range(-50, 30);
                        AddMutual(npc, other, RelationType.Rival, RelationType.Rival, affinity);
                        break;
                    }
                }

                if (npc.age >= 40 && npc.talent >= 60 && _rng.Chance(_s.mentorChance))
                {
                    var members = _db.GetClubMembers(npc.clubId);
                    for (int attempt = 0; attempt < 10 && members.Count > 1; attempt++)
                    {
                        var pupil = members[_rng.Range(0, members.Count)];
                        if (pupil == npc || pupil.age > npc.age - 15 || npc.HasRelation(pupil.id)) continue;
                        // pupil から見て npc は師匠、npc から見て pupil は弟子
                        AddMutual(pupil, npc, RelationType.Mentor, RelationType.Pupil, _rng.Range(30, 100));
                        break;
                    }
                }
            }
        }

        void BuildFriends()
        {
            foreach (var npc in _db.npcs)
            {
                int target = npc.personality == Personality.Extrovert ? _s.maxFriendLinks
                           : npc.personality == Personality.Introvert ? 1 : (_rng.Chance(0.5f) ? 1 : 0);
                var pool = _db.GetClubMembers(npc.clubId);
                if (pool.Count < 2) pool = _db.GetByTown(npc.hometown);
                int made = 0;
                for (int attempt = 0; attempt < 12 && made < target; attempt++)
                {
                    var other = pool[_rng.Range(0, pool.Count)];
                    if (other == npc || npc.HasRelation(other.id) || Math.Abs(other.age - npc.age) > 15) continue;
                    AddMutual(npc, other, RelationType.Friend, RelationType.Friend, _rng.Range(30, 100));
                    made++;
                }
            }
        }

        /// <summary>a から見た b の関係が typeAtoB、b から見た a が typeBtoA。</summary>
        static void AddMutual(NPCData a, NPCData b, RelationType typeAtoB, RelationType typeBtoA, int affinity)
        {
            if (a == b || a.HasRelation(b.id)) return;
            a.relations.Add(new NPCRelation(b.id, typeAtoB, affinity));
            if (!b.HasRelation(a.id)) b.relations.Add(new NPCRelation(a.id, typeBtoA, affinity));
        }

        // ==================================================================
        // 6. 知名度
        // ==================================================================

        int RollFame(NPCData npc)
        {
            float f = (npc.PublicOverall - 30) * 0.9f + _rng.Gaussian(0f, 8f);
            if (npc.occupation == Occupation.Idol) f += 40;
            if (npc.occupation == Occupation.Doctor || npc.occupation == Occupation.Police) f += 5;
            if (npc.soccerPersonality == SoccerPersonality.MoodMaker) f += 8;
            if (npc.soccerPersonality == SoccerPersonality.Phantom) f = 5; // 噂だけの存在
            if (npc.isUnderground) f *= 0.6f;
            if (npc.age < 15) f *= 0.5f;
            return Clamp((int)Math.Round(f), 0, 100);
        }

        static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
