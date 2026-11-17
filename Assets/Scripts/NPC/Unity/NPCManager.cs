using System;
using System.IO;
using UnityEngine;

namespace SoccerIsland.NPC.UnityIntegration
{
    /// <summary>
    /// シーンに1つ置くNPC管理コンポーネント。
    /// 起動時にセーブがあればロード、なければ島民1000人を生成する。
    /// ゲーム側からは NPCManager.Instance.Population 経由で会話・検索・ランキングを使う。
    /// セーブは端末内のみ。端末の個人情報は読まず、通信もしない（PrivacyPolicy 参照）。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class NPCManager : MonoBehaviour
    {
        public static NPCManager Instance { get; private set; }

        [Tooltip("未設定ならデフォルト設定で生成")]
        public NPCGenerationConfig config;

        [Tooltip("起動時にセーブデータがあればロードする")]
        public bool loadSaveOnStart = true;

        public string saveFileName = "island_npcs.json";

        public IslandPopulation Population { get; private set; }
        public NPCDatabase Database => Population?.Database;

        /// <summary>会話が終わったとき（UI・相関図・新聞の更新用）。</summary>
        public event Action<ConversationResult> OnConversation;
        /// <summary>市場価値が変わったとき。</summary>
        public event Action<MarketValueChange> OnMarketValueChanged;
        /// <summary>1日が終わったとき。</summary>
        public event Action<DayReport> OnDayAdvanced;

        string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (!(loadSaveOnStart && TryLoad())) GenerateNew();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void GenerateNew() => GenerateNew(config != null ? config.settings : NPCGenerationSettings.CreateDefault());

        public void GenerateNew(NPCGenerationSettings settings)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Attach(IslandPopulation.CreateNew(settings));
            Debug.Log($"[NPCManager] 島民 {Database.Count} 人を生成しました (seed={settings.seed}, {sw.ElapsedMilliseconds}ms)");
        }

        void Attach(IslandPopulation population)
        {
            if (Population != null) Population.Market.OnChanged -= RaiseMarketChanged;
            Population = population;
            Population.Market.OnChanged += RaiseMarketChanged;
        }

        void RaiseMarketChanged(MarketValueChange c) => OnMarketValueChanged?.Invoke(c);

        // ------------------------------------------------------------------
        // ゲーム側API
        // ------------------------------------------------------------------

        public ConversationResult Talk(int npcId)
        {
            var result = Population.Talk(npcId);
            OnConversation?.Invoke(result);
            return result;
        }

        public NPCProfileView GetProfile(int npcId) => Population.GetProfile(npcId);

        public DayReport AdvanceDay()
        {
            var report = Population.AdvanceDay();
            OnDayAdvanced?.Invoke(report);
            return report;
        }

        // ------------------------------------------------------------------
        // セーブ / ロード
        // ------------------------------------------------------------------

        public void Save()
        {
            var json = JsonUtility.ToJson(Population.CreateSave());
            File.WriteAllText(SavePath, json);
#if UNITY_IOS && !UNITY_EDITOR
            // iCloud バックアップに含めない（セーブデータを端末の外に出さない）
            UnityEngine.iOS.Device.SetNoBackupFlag(SavePath);
#endif
            // パスには端末のユーザー名などが含まれ得るのでログに出さない
            Debug.Log($"[NPCManager] セーブしました ({json.Length / 1024}KB)");
        }

        public bool TryLoad()
        {
            if (!File.Exists(SavePath)) return false;
            try
            {
                var save = JsonUtility.FromJson<IslandSaveData>(File.ReadAllText(SavePath));
                if (save?.database == null || save.database.npcs.Count == 0) return false;
                Attach(IslandPopulation.FromSave(save));
                Debug.Log($"[NPCManager] ロードしました: 島民 {Database.Count} 人 / {Database.currentYear}年 {Database.currentDay}日目");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NPCManager] セーブの読み込みに失敗したので新規生成します: {e.Message}");
                return false;
            }
        }

        public void DeleteSave()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
    }
}
