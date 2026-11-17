using System;
using UnityEngine;
namespace UnityEditor
{
    public class EditorWindow : ScriptableObject { public Rect position; public static T GetWindow<T>(string title) where T : EditorWindow => null; }
    public enum MessageType { Info, Warning, Error }
    public static class EditorStyles { public static GUIStyle toolbar, toolbarButton, miniLabel, miniBoldLabel, boldLabel, helpBox, label, miniButton; }
    public static class EditorGUILayout
    {
        public static void BeginHorizontal(params GUILayoutOption[] o) { } public static void BeginHorizontal(GUIStyle s, params GUILayoutOption[] o) { } public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] o) { } public static void EndVertical() { }
        public static Vector2 BeginScrollView(Vector2 v, params GUILayoutOption[] o) => v; public static void EndScrollView() { }
        public static UnityEngine.Object ObjectField(UnityEngine.Object o, Type t, bool scene, params GUILayoutOption[] op) => o;
        public static int IntField(string l, int v, params GUILayoutOption[] o) => v;
        public static string TextField(string l, string v, params GUILayoutOption[] o) => v;
        public static int Popup(int i, string[] d, params GUILayoutOption[] o) => i;
        public static Enum EnumPopup(Enum e, params GUILayoutOption[] o) => e;
        public static void HelpBox(string m, MessageType t) { }
        public static Rect GetControlRect(params GUILayoutOption[] o) => new Rect();
    }
    public static class EditorGUI { public static void ProgressBar(Rect r, float v, string t) { } }
    public static class EditorApplication { public static Action delayCall; }
    public class AssetImporter : UnityEngine.Object { }
    public enum TextureImporterType { Default, Sprite }
    public enum TextureImporterCompression { Uncompressed, Compressed }
    public enum TextureImporterNPOTScale { None, ToNearest }
    public class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType; public FilterMode filterMode; public TextureImporterCompression textureCompression;
        public bool mipmapEnabled, isReadable, alphaIsTransparency; public TextureImporterNPOTScale npotScale;
    }
    public class AssetPostprocessor { public string assetPath; public AssetImporter assetImporter; }
    [AttributeUsage(AttributeTargets.All)] public class MenuItemAttribute : Attribute { public MenuItemAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.All)] public class InitializeOnLoadAttribute : Attribute { }
}
namespace UnityEditor.Analytics
{
    public static class AnalyticsSettings { public static bool enabled; }
    public static class PerformanceReportingSettings { public static bool enabled; }
}
namespace UnityEditor.CrashReporting { public static class CrashReportingSettings { public static bool enabled; } }
namespace UnityEditor.Android { public interface IPostGenerateGradleAndroidProject { int callbackOrder { get; } void OnPostGenerateGradleAndroidProject(string path); } }
