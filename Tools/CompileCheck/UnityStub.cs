// コンパイル確認専用の UnityEngine / UnityEditor の最小スタブ（実行はしない）
using System;
namespace UnityEngine
{
    public class Object { public static void Destroy(Object o) { } public static void DontDestroyOnLoad(Object o) { } public string name; }
    public class Component : Object { public GameObject gameObject; public Transform transform; public T GetComponent<T>() => default(T); }
    public class Transform : Component { public Vector3 position; public Vector3 localScale; public void SetParent(Transform t) { } }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { }
    public class GameObject : Object
    {
        public Transform transform = new Transform();
        public GameObject() { } public GameObject(string name) { this.name = name; }
        public T AddComponent<T>() where T : Component, new() => new T();
        public T GetComponent<T>() => default(T);
    }
    public class Renderer : Component { public int sortingOrder; public Color color; }
    public class SpriteRenderer : Renderer { public Sprite sprite; public bool flipX; }
    public class Sprite : Object { public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu) => new Sprite(); }
    public static class Resources { public static T Load<T>(string path) where T : Object => null; }
    public static class Time { public static float deltaTime; }
    public enum KeyCode { None, Z, X, Space, Return, Escape, UpArrow, DownArrow, LeftArrow, RightArrow, W, A, S, D, M }
    public static class Input { public static bool GetKey(KeyCode k) => false; public static bool GetKeyDown(KeyCode k) => false; }
    public class Camera : Behaviour { public static Camera main; public bool orthographic; public float orthographicSize; public Color backgroundColor; public float aspect; }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d) => b;
    }
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
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator Color32(Color c) => new Color32(); public static Color white => new Color(1, 1, 1); }
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public enum TextureFormat { RGBA32 }
    public enum FontStyle { Normal, Bold }
    public class Texture2D : Object
    {
        public int width, height; public FilterMode filterMode; public TextureWrapMode wrapMode;
        public Texture2D(int w, int h, TextureFormat f, bool mip) { width = w; height = h; }
        public void SetPixels32(Color32[] p) { } public Color32[] GetPixels32() => new Color32[width * height]; public void Apply() { }
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b); public static int Max(int a, int b) => Math.Max(a, b); public static float Abs(float a) => Math.Abs(a);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f); public static int RoundToInt(float f) => (int)Math.Round(f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Clamp(float v, float a, float b) => Math.Max(a, Math.Min(b, v)); public static float Round(float f) => (float)Math.Round(f);
    }
    public class GUIStyleState { public Color textColor; }
    public class GUIStyle { public GUIStyle() { } public GUIStyle(GUIStyle o) { } public int fontSize; public FontStyle fontStyle; public bool wordWrap; public GUIStyleState normal = new GUIStyleState(); }
    public class GUISkin { public GUIStyle label = new GUIStyle(); }
    public class GUILayoutOption { }
    public static class GUI
    {
        public static GUISkin skin = new GUISkin();
        public static void Box(Rect r, string s) { }
        public static void Label(Rect r, string s, GUIStyle st) { }
        public static Color color;
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
