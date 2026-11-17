using UnityEditor;
using UnityEngine;

namespace SoccerIsland.Privacy.EditorTools
{
    /// <summary>
    /// プロジェクトを開くたびに、Unity が自動で外部送信する機能をオフにする。
    /// ・Analytics（利用状況の送信）
    /// ・Performance Reporting（パフォーマンス情報の送信）
    /// ・Crash Reporting / Cloud Diagnostics（クラッシュ情報の送信）
    /// メニュー SoccerIsland > Privacy > Apply Privacy Settings から手動でも実行できる。
    /// </summary>
    [InitializeOnLoad]
    static class PrivacyProjectSettings
    {
        static PrivacyProjectSettings()
        {
            EditorApplication.delayCall += () => Apply(false);
        }

        [MenuItem("SoccerIsland/Privacy/Apply Privacy Settings")]
        static void ApplyFromMenu() => Apply(true);

        static void Apply(bool verbose)
        {
            bool changed = false;

            if (UnityEditor.Analytics.AnalyticsSettings.enabled)
            {
                UnityEditor.Analytics.AnalyticsSettings.enabled = false;
                changed = true;
            }
            if (UnityEditor.Analytics.PerformanceReportingSettings.enabled)
            {
                UnityEditor.Analytics.PerformanceReportingSettings.enabled = false;
                changed = true;
            }
            if (UnityEditor.CrashReporting.CrashReportingSettings.enabled)
            {
                UnityEditor.CrashReporting.CrashReportingSettings.enabled = false;
                changed = true;
            }

            if (changed) Debug.Log("[Privacy] Analytics / Performance Reporting / Crash Reporting をオフにしました（外部送信なし）");
            else if (verbose) Debug.Log("[Privacy] 外部送信の設定はすべてオフです");
        }
    }
}
