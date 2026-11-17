// コンパイル確認専用の UnityEngine / UnityEditor の最小スタブ（実行はしない）
using System;
namespace UnityEngine
{
    public class Object { public static void Destroy(Object o) { } public static void DontDestroyOnLoad(Object o) { } public string name; }
    public class Component : Object { public GameObject gameObject; public T GetComponent<T>() => default(T); }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { }
    public class GameObject : Object { }
    public class ScriptableObject : Object { }
    public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } public static void LogError(object o) { } }
    public static class Application { public static string persistentDataPath = ""; public static bool isPlaying; }
    public static class JsonUtility { public static string ToJson(object o) => ""; public static T FromJson<T>(string s) => default(T); }
    public static class Screen { public static int width, height; }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public bool Contains(Vector2 p) => p.x >= x && p.y >= y && p.x <= x + width && p.y <= y + height;
    }
    public enum EventType { MouseDown, MouseUp, MouseDrag, Repaint, Layout }
    public class Event { public static Event current = new Event(); public EventType type; public Vector2 mousePosition; public void Use() { } }
    public static class GUILayoutUtility { public static Rect GetRect(float w, float h, params GUILayoutOption[] o) => new Rect(0, 0, w, h); }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator Color32(Color c) => new Color32(); }
    public struct Color32 { public Color32(byte r, byte g, byte b, byte a) { } }
    public enum TextureFormat { RGBA32 }
    public enum FontStyle { Normal, Bold }
    public class Texture2D : Object { public Texture2D(int w, int h, TextureFormat f, bool mip) { } public void SetPixels32(Color32[] p) { } public void Apply() { } }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b); public static int Max(int a, int b) => Math.Max(a, b); public static float Abs(float a) => Math.Abs(a);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f); public static int RoundToInt(float f) => (int)Math.Round(f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
    public class GUIStyle { public GUIStyle() { } public GUIStyle(GUIStyle o) { } public int fontSize; public FontStyle fontStyle; }
    public class GUISkin { public GUIStyle label = new GUIStyle(); }
    public class GUILayoutOption { }
    public static class GUI
    {
        public static GUISkin skin = new GUISkin();
        public static void Box(Rect r, string s) { }
        public static void DrawTexture(Rect r, Texture2D t) { }
    }
    public static class GUILayout
    {
        public static void BeginArea(Rect r) { } public static void EndArea() { }
        public static void BeginHorizontal(params GUILayoutOption[] o) { } public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] o) { } public static void EndVertical() { }
        public static Vector2 BeginScrollView(Vector2 v, params GUILayoutOption[] o) => v; public static void EndScrollView() { }
        public static void Label(string s, params GUILayoutOption[] o) { } public static void Label(string s, GUIStyle st, params GUILayoutOption[] o) { }
        public static bool Button(string s, params GUILayoutOption[] o) => false; public static bool Button(string s, GUIStyle st, params GUILayoutOption[] o) => false;
        public static bool Toggle(bool v, string s, params GUILayoutOption[] o) => v;
        public static int Toolbar(int sel, string[] t, params GUILayoutOption[] o) => sel;
        public static int SelectionGrid(int sel, string[] t, int x, params GUILayoutOption[] o) => sel;
        public static string TextField(string s, params GUILayoutOption[] o) => s;
        public static void Space(float f) { } public static void FlexibleSpace() { }
        public static GUILayoutOption Width(float f) => null; public static GUILayoutOption Height(float f) => null;
    }
    [AttributeUsage(AttributeTargets.All)] public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.All)] public class ContextMenuAttribute : Attribute { public ContextMenuAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.All)] public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
    [AttributeUsage(AttributeTargets.All)] public class DefaultExecutionOrderAttribute : Attribute { public DefaultExecutionOrderAttribute(int o) { } }
}
namespace UnityEngine.iOS { public static class Device { public static void SetNoBackupFlag(string p) { } } }
