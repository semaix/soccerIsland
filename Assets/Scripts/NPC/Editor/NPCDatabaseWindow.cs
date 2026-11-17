using System.Collections.Generic;
using System.Linq;
using SoccerIsland.NPC.UnityIntegration;
using UnityEditor;
using UnityEngine;

namespace SoccerIsland.NPC.EditorTools
{
    /// <summary>
    /// 開発用：島民データベースの閲覧ウィンドウ。隠しパラメータ（能力値・潜在・覚醒年齢・人生周回）も見える。
    /// メニュー SoccerIsland > NPC Database Viewer
    /// </summary>
    public class NPCDatabaseWindow : EditorWindow
    {
        enum SortKey { Id, MarketValue, Overall, Potential, Age, Talent, Effort, Fame }

        NPCGenerationConfig _config;
        int _seed = 20261117;
        IslandPopulation _population;
        string _search = "";
        Town? _townFilter;
        bool _undergroundOnly, _specialOnly, _awakeningOnly;
        SortKey _sort = SortKey.MarketValue;
        Vector2 _listScroll, _detailScroll;
        int _selectedId = -1;
        List<NPCData> _view = new List<NPCData>();

        [MenuItem("SoccerIsland/NPC Database Viewer")]
        static void Open() => GetWindow<NPCDatabaseWindow>("NPC Database");

        void OnGUI()
        {
            DrawToolbar();
            var pop = CurrentPopulation();
            if (pop == null)
            {
                EditorGUILayout.HelpBox("「Generate」で島民を生成するか、Play中に NPCManager のデータを表示します。", MessageType.Info);
                return;
            }

            DrawFilters();
            RefreshView(pop.Database);

            EditorGUILayout.BeginHorizontal();
            DrawList(pop.Database);
            DrawDetail(pop);
            EditorGUILayout.EndHorizontal();
        }

        IslandPopulation CurrentPopulation()
        {
            if (Application.isPlaying && NPCManager.Instance != null) return NPCManager.Instance.Population;
            return _population;
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _config = (NPCGenerationConfig)EditorGUILayout.ObjectField(_config, typeof(NPCGenerationConfig), false, GUILayout.Width(220));
            _seed = EditorGUILayout.IntField("Seed", _seed, GUILayout.Width(200));
            if (GUILayout.Button("Generate", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                var settings = _config != null ? _config.settings : NPCGenerationSettings.CreateDefault();
                settings.seed = _seed;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _population = IslandPopulation.CreateNew(settings);
                Debug.Log($"[NPC Viewer] {settings.npcCount}人生成 ({sw.ElapsedMilliseconds}ms)");
                _selectedId = -1;
            }
            if (_population != null && GUILayout.Button("+30日", EditorStyles.toolbarButton, GUILayout.Width(50)))
                for (int i = 0; i < 30; i++) _population.AdvanceDay();
            if (_population != null && GUILayout.Button("+1年", EditorStyles.toolbarButton, GUILayout.Width(50)))
                for (int i = 0; i < 365; i++) _population.AdvanceDay();
            GUILayout.FlexibleSpace();
            var db = CurrentPopulation()?.Database;
            if (db != null) GUILayout.Label($"{db.currentYear}年 / {db.currentDay}日目 / {db.Count}人", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField("検索", _search);
            var townNames = new[] { "全街" }.Concat(System.Enum.GetValues(typeof(Town)).Cast<Town>().Select(t => NPCLabels.Of(t))).ToArray();
            int townIdx = _townFilter.HasValue ? (int)_townFilter.Value + 1 : 0;
            townIdx = EditorGUILayout.Popup(townIdx, townNames, GUILayout.Width(90));
            _townFilter = townIdx == 0 ? (Town?)null : (Town)(townIdx - 1);
            _sort = (SortKey)EditorGUILayout.EnumPopup(_sort, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _undergroundOnly = GUILayout.Toggle(_undergroundOnly, "裏の人間", GUILayout.Width(80));
            _specialOnly = GUILayout.Toggle(_specialOnly, "特殊性格", GUILayout.Width(80));
            _awakeningOnly = GUILayout.Toggle(_awakeningOnly, "覚醒型", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        void RefreshView(NPCDatabase db)
        {
            IEnumerable<NPCData> q = db.npcs;
            if (_townFilter.HasValue) q = q.Where(n => n.hometown == _townFilter.Value);
            if (_undergroundOnly) q = q.Where(n => n.isUnderground || !n.registeredWithAssociation);
            if (_specialOnly) q = q.Where(n => n.IsSpecialPersonality);
            if (_awakeningOnly) q = q.Where(n => n.growthType == GrowthType.Awakening);
            if (!string.IsNullOrEmpty(_search))
                q = q.Where(n => n.FullName.Contains(_search) || n.FullNameKana.Contains(_search) ||
                                 NPCLabels.Of(n.occupation).Contains(_search) || NPCLabels.Of(n.soccerPersonality).Contains(_search));
            switch (_sort)
            {
                case SortKey.MarketValue: q = q.OrderByDescending(n => n.marketValue); break;
                case SortKey.Overall: q = q.OrderByDescending(n => n.Overall); break;
                case SortKey.Potential: q = q.OrderByDescending(n => n.potential); break;
                case SortKey.Age: q = q.OrderBy(n => n.age); break;
                case SortKey.Talent: q = q.OrderByDescending(n => n.talent); break;
                case SortKey.Effort: q = q.OrderByDescending(n => n.effort); break;
                case SortKey.Fame: q = q.OrderByDescending(n => n.fame); break;
            }
            _view = q.ToList();
        }

        void DrawList(NPCDatabase db)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.55f));
            GUILayout.Label($"{_view.Count}人", EditorStyles.miniBoldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (var n in _view)
            {
                var style = n.id == _selectedId ? EditorStyles.helpBox : EditorStyles.label;
                string label = $"#{n.id,-4} {n.FullName}({n.age}) {NPCLabels.Of(n.occupation)} | {NPCLabels.Of(n.soccerPersonality)} | {NPCLabels.Yen(n.marketValue)} | OVR {n.Overall}/{n.potential}";
                if (GUILayout.Button(label, style)) _selectedId = n.id;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawDetail(IslandPopulation pop)
        {
            var db = pop.Database;
            EditorGUILayout.BeginVertical();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            var n = db.Get(_selectedId);
            if (n == null)
            {
                GUILayout.Label("NPCを選択してください");
                GUILayout.Space(10);
                GUILayout.Label("協会 市場価値ランキング TOP10", EditorStyles.boldLabel);
                int rank = 1;
                foreach (var r in db.GetOfficialMarketValueRanking(10))
                    GUILayout.Label($"{rank++}. {r.FullName} ({NPCLabels.Of(r.hometown)}) {NPCLabels.Yen(r.marketValue)}");
                GUILayout.Label("裏ランキング TOP5", EditorStyles.boldLabel);
                rank = 1;
                foreach (var r in db.GetUndergroundMarketValueRanking(5))
                    GUILayout.Label($"{rank++}. {r.FullName} ({NPCLabels.Of(r.occupation)}) {NPCLabels.Yen(r.marketValue)}");
            }
            else
            {
                GUILayout.Label($"{n.FullName}（{n.FullNameKana}）", EditorStyles.boldLabel);
                Row("性別 / 年齢", $"{NPCLabels.Of(n.gender)} / {n.age}歳 ({n.birthYear}年生)");
                Row("街 / 職業", $"{NPCLabels.Of(n.hometown)} / {NPCLabels.Of(n.occupation)}");
                Row("クラブ", db.GetClub(n.clubId)?.name ?? "無所属");
                Row("電話", string.IsNullOrEmpty(n.phoneNumber) ? "なし" : n.phoneNumber);
                Row("性格", $"{NPCLabels.Of(n.personality)} / {NPCLabels.Of(n.soccerPersonality)}");
                Row("ポジション", n.position.ToString());
                Row("才能度 / 努力度", $"{n.talent} ({NPCLabels.Rank(n.talent)}) / {n.effort} ({NPCLabels.Rank(n.effort)})");
                Row("成長タイプ", NPCLabels.Of(n.growthType) + (n.growthType == GrowthType.Awakening ? $"（覚醒 {n.awakeningAge}歳 {(n.hasAwakened ? "済" : "前")}）" : ""));
                Row("年俸 / 本業年収", $"{NPCLabels.Yen(n.salary)} / {NPCLabels.Yen(n.jobIncome)}");
                Row("市場価値", $"{NPCLabels.Yen(n.marketValue)}  (hype {n.hype:+0.000;-0.000})");
                Row("知名度", n.fame.ToString());
                Row("協会登録 / 裏", $"{(n.registeredWithAssociation ? "登録" : "未登録")} / {(n.isUnderground ? "裏の人間" : "-")}");
                if (n.isIncarcerated) Row("服役", $"{n.releaseYear}年出所");
                if (n.isStranded) Row("漂流者", "島から出られない");

                GUILayout.Space(6);
                GUILayout.Label("隠しパラメータ", EditorStyles.boldLabel);
                Row("総合 / 表の総合 / 潜在", $"{n.Overall} / {n.PublicOverall} / {n.potential}");
                Row("人生", $"{n.lifeCycle}周目");
                string[] names = { "シュート", "パス", "ドリブル", "テクニック", "守備", "フィジカル", "スピード", "スタミナ", "メンタル", "キーパー" };
                for (int i = 0; i < NPCAbilities.StatCount; i++)
                {
                    var rect = EditorGUILayout.GetControlRect();
                    EditorGUI.ProgressBar(rect, n.abilities[i] / 99f, $"{names[i]} {n.abilities[i]} / {n.statPotential[i]}");
                }

                GUILayout.Space(6);
                GUILayout.Label($"関係 ({n.relations.Count})", EditorStyles.boldLabel);
                foreach (var r in n.relations)
                {
                    var t = db.Get(r.targetId);
                    if (t == null) continue;
                    if (GUILayout.Button($"{NPCLabels.Of(r.type)}: {t.FullName}({t.age}) 好感{r.affinity}", EditorStyles.miniButton))
                        _selectedId = t.id;
                }

                GUILayout.Space(6);
                var profile = pop.GetProfile(n.id);
                GUILayout.Label($"プレイヤーの知識（好感度 {profile.affinity}）", EditorStyles.boldLabel);
                Row("見えている情報", $"{profile.name} / {profile.age} / {profile.occupation} / {profile.soccerPersonality} / 価値 {profile.marketValue} / 才能 {profile.talentRank}");
                if (GUILayout.Button("話しかける（テスト）"))
                {
                    var res = Application.isPlaying && NPCManager.Instance != null ? NPCManager.Instance.Talk(n.id) : pop.Talk(n.id);
                    foreach (var line in res.lines) Debug.Log($"{n.FullName}「{line}」");
                    foreach (var c in res.marketChanges)
                        if (c.Delta != 0) Debug.Log($"  市場価値 #{c.npcId}: {NPCLabels.Yen(c.before)} → {NPCLabels.Yen(c.after)} ({c.reason})");
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        static void Row(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140));
            GUILayout.Label(value);
            EditorGUILayout.EndHorizontal();
        }
    }
}
