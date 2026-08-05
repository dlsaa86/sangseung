// ─────────────────────────────────────────────────────────────────────────────
// UnityEngine 최소 shim — 클라우드 헤드리스 하네스 전용.
//
// 이 파일은 **프로젝트에 들어가지 않는다.** Unity 에디터 없이 순수 판정 계층
// (Spin / Run / Build / Risk / Data.Profiles / Sim)을 .NET 8 로 컴파일해
// 대량 시뮬레이션을 돌리기 위한 대역이다.
//
// 규칙: 게임 코드가 실제로 부르는 API 만 넣는다. 넣을 때는 **Unity 의 정의와
// 같은 의미**여야 한다 — 여기서 값이 갈라지면 하네스는 다른 게임을 재게 된다.
// ─────────────────────────────────────────────────────────────────────────────
using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float Epsilon = 1.401298E-45f;
        public const float PI = 3.14159274f;
        public const float Infinity = float.PositiveInfinity;

        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(params float[] v) { float m = v[0]; for (int i = 1; i < v.Length; i++) if (v[i] > m) m = v[i]; return m; }
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v)
            => Math.Abs(b - a) < 1e-9f ? 0f : Clamp01((v - a) / (b - a));
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
        public static float Round(float v) => (float)Math.Round(v, MidpointRounding.ToEven);
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Sign(float v) => v >= 0f ? 1f : -1f;
        public static float Repeat(float t, float length) => Clamp(t - (float)Math.Floor(t / length) * length, 0f, length);
        public static bool Approximately(float a, float b)
            => Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8f);
        public static float MoveTowards(float cur, float target, float maxDelta)
            => Math.Abs(target - cur) <= maxDelta ? target : cur + Sign(target - cur) * maxDelta;
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }

        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
        public static Color red => new Color(1f, 0f, 0f, 1f);
        public static Color green => new Color(0f, 1f, 0f, 1f);
        public static Color blue => new Color(0f, 0f, 1f, 1f);

        public static Color Lerp(Color x, Color y, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        }

        public static Color operator *(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a * f);
        public static Color operator *(float f, Color c) => c * f;

        /// <summary>Unity 와 같은 정의 (H·S·V 각각 0~1).</summary>
        public static void RGBToHSV(Color rgb, out float H, out float S, out float V)
        {
            float max = Mathf.Max(rgb.r, Mathf.Max(rgb.g, rgb.b));
            float min = Mathf.Min(rgb.r, Mathf.Min(rgb.g, rgb.b));
            float d = max - min;
            V = max;
            S = max <= 0f ? 0f : d / max;
            if (d <= 0f) { H = 0f; return; }
            float h;
            if (Math.Abs(max - rgb.r) < 1e-9f) h = (rgb.g - rgb.b) / d;
            else if (Math.Abs(max - rgb.g) < 1e-9f) h = 2f + (rgb.b - rgb.r) / d;
            else h = 4f + (rgb.r - rgb.g) / d;
            h /= 6f;
            if (h < 0f) h += 1f;
            H = h;
        }

        public override string ToString() => $"RGBA({r:F3}, {g:F3}, {b:F3}, {a:F3})";
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 one => new Vector2(1f, 1f);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) : this(x, y, 0f) { }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x * f, a.y * f, a.z * f);
        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z);
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }

    /// <summary>하네스 로그는 표준 출력으로 보낸다. 헤드리스 러너가 접두사로 걸러 쓴다.</summary>
    public static class Debug
    {
        public static bool Silent = true;
        public static int WarningCount { get; private set; }
        public static int ErrorCount { get; private set; }
        public static void Log(object m) { if (!Silent) Console.WriteLine("[LOG] " + m); }
        public static void LogWarning(object m) { WarningCount++; if (!Silent) Console.WriteLine("[WARN] " + m); }
        public static void LogError(object m) { ErrorCount++; if (!Silent) Console.WriteLine("[ERROR] " + m); }
        public static void LogFormat(string f, params object[] a) { if (!Silent) Console.WriteLine("[LOG] " + string.Format(f, a)); }
        public static void ResetCounters() { WarningCount = 0; ErrorCount = 0; }
    }

    /// <summary>헤드리스에서는 시간이 흐르지 않는다. 연출 전용 값이라 판정에 쓰이면 안 된다.</summary>
    public static class Time
    {
        public static float unscaledDeltaTime = 0f;
        public static float deltaTime = 0f;
        public static float time = 0f;
        public static float unscaledTime = 0f;
        public static float timeScale = 1f;
        public static int frameCount = 0;
    }

    public static class Application
    {
        public static bool isPlaying => false;
        public static bool isEditor => false;
        public static string persistentDataPath = "/tmp/ascend";
        public static string dataPath = "/tmp/ascend";
    }

    public class Object
    {
        public string name = string.Empty;
        public override string ToString() => name;
        public static void Destroy(Object o) { }
        public static void DestroyImmediate(Object o) { }
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b) || (a is null && b is null);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static implicit operator bool(Object o) => !(o is null);
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
        public static ScriptableObject CreateInstance(Type t) => (ScriptableObject)Activator.CreateInstance(t);
        // `OnValidate`/`OnEnable`/`Awake` 를 여기에 두지 않는다. Unity 는 이들을
        // 가상 멤버가 아니라 **메시지**로 부르므로, 대역에 선언하면 게임 코드의
        // 같은 이름 메서드가 CS0114(숨김) 경고를 낸다 — 실제 유니티에는 없는 경고다.
        // 대역이 만들어 낸 경고를 게임 코드의 문제로 읽게 되므로 두지 않는다.
    }

    public class Component : Object { }

    public class Behaviour : Component { public bool enabled = true; }

    public class MonoBehaviour : Behaviour
    {
        public bool isActiveAndEnabled => enabled;
        protected void print(object m) => Debug.Log(m);
    }

    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class SerializableAttributeMarker : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SpaceAttribute : Attribute { public SpaceAttribute() { } public SpaceAttribute(float h) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MinAttribute : Attribute { public MinAttribute(float min) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TextAreaAttribute : Attribute { public TextAreaAttribute() { } public TextAreaAttribute(int a, int b) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName; public string menuName; public int order;
    }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class RequireComponent : Attribute { public RequireComponent(Type t) { } public RequireComponent(Type a, Type b) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class AddComponentMenu : Attribute { public AddComponentMenu(string m) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class ExecuteAlways : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class ContextMenu : Attribute { public ContextMenu(string m) { } }

    public static class JsonUtility
    {
        public static string ToJson(object o, bool pretty = false) => System.Text.Json.JsonSerializer.Serialize(o);
    }
}
