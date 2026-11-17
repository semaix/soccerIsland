using System;
using System.IO;
using SoccerIsland.NPC;
using SoccerIsland.NPC.UnityIntegration;
using SoccerIsland.World;
using SoccerIsland.World.UnityIntegration;
using UnityEngine;

namespace SoccerIsland.Career.UnityIntegration
{
    /// <summary>
    /// 主人公のお金・スカウト・株・toto・知名度・新聞をシーンで動かす。
    /// NPCManager・WorldManager と同じシーンに置く。日付は NPCManager.AdvanceDay() で進める。
    /// セーブは端末内のみ。
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class CareerManager : MonoBehaviour
    {
        public static CareerManager Instance { get; private set; }

        public string saveFileName = "island_career.json";

        public IslandCareer Career { get; private set; }
        public PlayerState Player => Career?.Player;

        /// <summary>朝刊が届いたとき。</summary>
        public event Action<NewspaperIssue> OnNewspaper;
        /// <summary>1日が終わったとき（お金・知名度の出来事を含む）。</summary>
        public event Action<CareerDayReport> OnCareerDay;

        NPCManager _npc;
        WorldManager _world;
        string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            _npc = NPCManager.Instance;
            _world = WorldManager.Instance;
            if (_npc == null || _world == null || _world.World == null)
            {
                Debug.LogError("[CareerManager] NPCManager と WorldManager がシーンに必要です");
                enabled = false;
                return;
            }

            Build(_npc.LoadedFromSave ? TryLoad() : null);

            _world.OnWorldDayProcessed += HandleWorldDay;
            _npc.OnDayAdvanced += HandleDayAdvanced;   // WorldManager より後に登録される
            _npc.OnSave += Save;
            _npc.OnPopulationReplaced += HandlePopulationReplaced;
        }

        void OnDestroy()
        {
            if (_world != null) _world.OnWorldDayProcessed -= HandleWorldDay;
            if (_npc != null)
            {
                _npc.OnDayAdvanced -= HandleDayAdvanced;
                _npc.OnSave -= Save;
                _npc.OnPopulationReplaced -= HandlePopulationReplaced;
            }
            if (Instance == this) Instance = null;
        }

        void Build(CareerSaveData save) => Career = new IslandCareer(_world.World, save);

        void HandlePopulationReplaced(IslandPopulation _) => Build(null); // WorldManager が先に作り直している

        void HandleWorldDay(WorldDayReport report) => Career.BeforeDayAdvance(report);

        void HandleDayAdvanced(DayReport report)
        {
            var day = Career.AfterDayAdvance(report);
            OnCareerDay?.Invoke(day);
            if (day.newspaper != null) OnNewspaper?.Invoke(day.newspaper);
        }

        /// <summary>引退して次の人生を始める。島民・ワールド・主人公がすべて作り直される。</summary>
        public LegacyRecord RetireAndStartNextLife()
        {
            var legacy = ReincarnationSystem.Retire(Career);
            if (legacy == null) return null;
            var next = ReincarnationSystem.StartNextCycle(legacy, Career.PastLives);
            _npc.Adopt(next.Population);
            _world.Adopt(next.World);
            Career = next;
            return legacy;
        }

        /// <summary>NPCに話しかける（会話＋ファンのお願い）。</summary>
        public Interaction Interact(int npcId) => Career.Interact(npcId);

        public void Save()
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(Career.CreateSave()));
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.SetNoBackupFlag(SavePath);
#endif
        }

        CareerSaveData TryLoad()
        {
            if (!File.Exists(SavePath)) return null;
            try
            {
                var save = JsonUtility.FromJson<CareerSaveData>(File.ReadAllText(SavePath));
                return save?.player == null ? null : save;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CareerManager] セーブを読み込めなかったので新規作成します: {e.Message}");
                return null;
            }
        }
    }
}
