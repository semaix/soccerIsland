using System;
using System.IO;
using SoccerIsland.NPC;
using SoccerIsland.NPC.UnityIntegration;
using UnityEngine;

namespace SoccerIsland.World.UnityIntegration
{
    /// <summary>
    /// 試合予定・お店をシーンで動かす。NPCManager と同じ GameObject（または同じシーン）に置く。
    /// 日付は NPCManager.AdvanceDay() で進める（その前後でこのコンポーネントが試合・買い物を処理する）。
    /// セーブは端末内のみ。
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class WorldManager : MonoBehaviour
    {
        public static WorldManager Instance { get; private set; }

        public string saveFileName = "island_world.json";

        public IslandWorld World { get; private set; }
        public WorldState State => World?.State;

        /// <summary>今日の試合結果が出たとき。</summary>
        public event Action<WorldDayReport> OnWorldDayProcessed;

        NPCManager _npc;
        string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            _npc = NPCManager.Instance;
            if (_npc == null)
            {
                Debug.LogError("[WorldManager] NPCManager がシーンにありません");
                enabled = false;
                return;
            }

            WorldState saved = _npc.LoadedFromSave ? TryLoadState() : null;
            Build(_npc.Population, saved);

            _npc.OnBeforeDayAdvance += HandleBeforeDay;
            _npc.OnDayAdvanced += HandleAfterDay;
            _npc.OnSave += Save;
            _npc.OnPopulationReplaced += HandlePopulationReplaced;
        }

        void OnDestroy()
        {
            if (_npc != null)
            {
                _npc.OnBeforeDayAdvance -= HandleBeforeDay;
                _npc.OnDayAdvanced -= HandleAfterDay;
                _npc.OnSave -= Save;
                _npc.OnPopulationReplaced -= HandlePopulationReplaced;
            }
            if (Instance == this) Instance = null;
        }

        void Build(IslandPopulation population, WorldState state)
        {
            if (World != null)
            {
                World.Population.Conversation.RemoveTopicProvider(World.MatchTopics);
                World.Population.Conversation.RemoveTopicProvider(World.PurchaseTopics);
            }
            World = new IslandWorld(population, state);
        }

        void HandlePopulationReplaced(IslandPopulation population) => Build(population, null);

        void HandleBeforeDay()
        {
            var report = World.BeforeDayAdvance();
            OnWorldDayProcessed?.Invoke(report);
        }

        void HandleAfterDay(DayReport _) => World.AfterDayAdvance();

        public void Save()
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(World.State));
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.SetNoBackupFlag(SavePath);
#endif
        }

        WorldState TryLoadState()
        {
            if (!File.Exists(SavePath)) return null;
            try
            {
                return JsonUtility.FromJson<WorldState>(File.ReadAllText(SavePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WorldManager] ワールドのセーブを読み込めなかったので作り直します: {e.Message}");
                return null;
            }
        }
    }
}
