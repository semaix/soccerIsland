using UnityEngine;

namespace SoccerIsland.NPC.UnityIntegration
{
    /// <summary>
    /// NPC生成設定アセット。Project ビューで
    /// Create > SoccerIsland > NPC Generation Config から作成し、インスペクタで街や職業の比率を調整する。
    /// </summary>
    [CreateAssetMenu(fileName = "NPCGenerationConfig", menuName = "SoccerIsland/NPC Generation Config")]
    public class NPCGenerationConfig : ScriptableObject
    {
        public NPCGenerationSettings settings = NPCGenerationSettings.CreateDefault();

        void OnValidate()
        {
            if (settings == null) settings = NPCGenerationSettings.CreateDefault();
            settings.FillDefaultsIfEmpty();
        }

        [ContextMenu("Reset To Defaults")]
        void ResetToDefaults() => settings = NPCGenerationSettings.CreateDefault();
    }
}
