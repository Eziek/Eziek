// Atrapy API Unity - tylko do sprawdzenia kompilacji kodu gry poza Unity.
#pragma warning disable
using System;
using System.Collections;
using System.Collections.Generic;
namespace UnityEngine
{
    public class Object { public string name; public static implicit operator bool(Object o) => o != null; }
    public class Component : Object { public Transform transform; public GameObject gameObject; public T GetComponent<T>() => default; public bool TryGetComponent<T>(out T c) { c = default; return false; } public string tag; }
    public class Behaviour : Component { public bool enabled; public bool isActiveAndEnabled; }
    public class MonoBehaviour : Behaviour { public Coroutine StartCoroutine(IEnumerator e) => null; public void StopCoroutine(Coroutine c) { } }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject => default; }
    public class GameObject : Object { public GameObject(string n) { } public Transform transform; public string tag; public T AddComponent<T>() where T : Component => default; public T GetComponent<T>() => default; }
    public class Transform : Component { public Vector3 position, localPosition, localScale; public void SetParent(Transform p, bool w) { } }
    public class Renderer : Component { public int sortingOrder; public Material sharedMaterial; public bool enabled; }
    public class SpriteRenderer : Renderer { public Sprite sprite; }
    public class Sprite : Object { public Bounds bounds; }
    public struct Bounds { public Vector3 size; }
    public class Material : Object { public Material(Shader s) { } }
    public class Shader : Object { public static Shader Find(string n) => null; }
    public class TextAsset : Object { public string text; }
    public class Texture2D : Object { public static Texture2D whiteTexture; }
    public class Camera : Behaviour { public static Camera main; public bool orthographic; public float orthographicSize; public CameraClearFlags clearFlags; public Color backgroundColor; public Vector3 ScreenToWorldPoint(Vector3 p) => p; }
    public enum CameraClearFlags { Skybox, SolidColor }
    public class Collider2D : Behaviour { }
    public class BoxCollider2D : Collider2D { public Vector2 size; }
    public static class Input { public static Vector3 mousePosition; public static bool GetMouseButtonDown(int b) => false; }
    public static class Physics2D { public static Collider2D OverlapPoint(Vector2 p) => null; }
    public class Coroutine { }
    public class WaitForSecondsRealtime { public WaitForSecondsRealtime(float s) { } }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public float sqrMagnitude => 0; public static Vector2 operator -(Vector2 a, Vector2 b) => a; public static implicit operator Vector2(Vector3 v) => default; public static implicit operator Vector3(Vector2 v) => default; }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static Vector3 zero, one; public static Vector3 operator -(Vector3 a, Vector3 b) => a; }
    public struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } public static Vector4 zero; }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white; public static implicit operator Color(Color32 c) => default; }
    public struct Color32 { public Color32(byte r, byte g, byte b, byte a) { } }
    public struct Rect { public float x, y, width, height; public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; } public bool Contains(Vector2 p) => false; }
    public static class Mathf { public static float Clamp(float v, float a, float b) => v; public static int Clamp(int v, int a, int b) => v; public static float Clamp01(float v) => v; public static float Min(float a, float b) => a; public static int Min(int a, int b) => a; public static float Max(float a, float b) => a; public static int Max(int a, int b) => a; public static float Floor(float f) => f; public static float Sin(float f) => f; public static float Abs(float f) => f; public static float Repeat(float t, float l) => t; public static bool Approximately(float a, float b) => false; }
    public static class Random { public static float value; }
    public static class Application { public static bool runInBackground; public static bool isFocused; public static int targetFrameRate; public static string persistentDataPath; public static event Action<bool> focusChanged; }
    public static class QualitySettings { public static int vSyncCount; }
    public static class Time { public static float unscaledDeltaTime, unscaledTime; }
    public static class Screen { public static int height; }
    public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } public static void LogError(object o) { } }
    public static class JsonUtility { public static string ToJson(object o, bool p) => ""; public static T FromJson<T>(string s) => default; }
    public class Gradient { public void SetKeys(GradientColorKey[] c, GradientAlphaKey[] a) { } public Color Evaluate(float t) => default; }
    public struct GradientColorKey { public GradientColorKey(Color c, float t) { } }
    public struct GradientAlphaKey { public GradientAlphaKey(float a, float t) { } }
    public class AnimationCurve { public AnimationCurve(params Keyframe[] k) { } public float Evaluate(float t) => 0; }
    public struct Keyframe { public Keyframe(float t, float v) { } }
    public enum FullScreenMode { Windowed }
    public enum FilterMode { Point }
    public enum TextureWrapMode { Clamp }
    public enum SpriteAlignment { Center, Custom = 9 }
    public enum SpriteMeshType { FullRect, Tight }
    public class Event { public static Event current; public Vector2 mousePosition; }
    public class GUIContent { public static GUIContent none; }
    public class GUISkin { public GUIStyle box; }
    public class GUIStyle { }
    public static class GUI { public static GUISkin skin; public static Color color; public static bool enabled; public static void Box(Rect r, GUIContent c) { } public static void DrawTexture(Rect r, Texture2D t) { } }
    public class GUILayoutOption { }
    public static class GUILayout { public static void BeginArea(Rect r, GUIStyle s) { } public static void EndArea() { } public static Vector2 BeginScrollView(Vector2 v, params GUILayoutOption[] o) => v; public static void EndScrollView() { } public static void Label(string s, params GUILayoutOption[] o) { } public static bool Button(string s, params GUILayoutOption[] o) => false; public static bool Toggle(bool v, string s, params GUILayoutOption[] o) => v; public static void Space(float f) { } public static void BeginHorizontal(params GUILayoutOption[] o) { } public static void EndHorizontal() { } }
    public static class GUILayoutUtility { public static Rect GetRect(float w, float h) => default; }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int o) { } }
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
}
namespace UnityEngine.SceneManagement { public struct Scene { } }
namespace UnityEngine.Rendering { public class RenderPipelineAsset : UnityEngine.Object { } public static class GraphicsSettings { public static RenderPipelineAsset defaultRenderPipeline; } }
namespace UnityEngine.Rendering.Universal
{
    // UWAGA: klasa Renderer2D jest w URP 17 wewnętrzna (internal), więc nie ma jej w atrapach.
    public class UniversalRenderPipelineAsset : UnityEngine.Rendering.RenderPipelineAsset { }
    public class Light2D : UnityEngine.Behaviour { public enum LightType { Parametric, Freeform, Sprite, Point, Global } public LightType lightType { get; set; } public float intensity { get; set; } public UnityEngine.Color color { get; set; } public float pointLightOuterRadius { get; set; } public float pointLightInnerRadius { get; set; } public float falloffIntensity { get; set; } }
    public class PixelPerfectCamera : UnityEngine.MonoBehaviour { public enum GridSnapping { None, PixelSnapping, UpscaleRenderTexture } public int assetsPPU { get; set; } public int refResolutionX { get; set; } public int refResolutionY { get; set; } public GridSnapping gridSnapping { get; set; } }
}
namespace UnityEngine.InputSystem
{
    public class ButtonControl { public bool wasPressedThisFrame; }
    public class Vector2Control { public UnityEngine.Vector2 ReadValue() => default; }
    public class Mouse { public static Mouse current; public ButtonControl leftButton; public Vector2Control position; }
}
namespace UnityEditor
{
    using UnityEngine;
    public class AssetImporter : UnityEngine.Object { }
    public class TextureImporter : AssetImporter { public TextureImporterType textureType; public SpriteImportMode spriteImportMode; public float spritePixelsPerUnit; public FilterMode filterMode; public TextureImporterCompression textureCompression; public bool mipmapEnabled, alphaIsTransparency; public TextureWrapMode wrapMode; public void ReadTextureSettings(TextureImporterSettings s) { } public void SetTextureSettings(TextureImporterSettings s) { } }
    public class TextureImporterSettings { public SpriteMeshType spriteMeshType; public uint spriteExtrude; public int spriteAlignment; public Vector2 spritePivot; public Vector4 spriteBorder; }
    public enum TextureImporterType { Sprite }
    public enum SpriteImportMode { Single }
    public enum TextureImporterCompression { Uncompressed }
    public class AssetPostprocessor { public string assetPath; public AssetImporter assetImporter; }
    public enum ImportAssetOptions { ForceUpdate }
    public static class AssetDatabase { public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object => default; public static void CreateAsset(UnityEngine.Object o, string p) { } public static void SaveAssets() { } public static void Refresh() { } public static bool IsValidFolder(string p) => true; public static string CreateFolder(string a, string b) => ""; public static string[] FindAssets(string f, string[] d) => null; public static string GUIDToAssetPath(string g) => ""; public static void ImportAsset(string p, ImportAssetOptions o) { } }
    public class MenuItem : Attribute { public MenuItem(string s) { } public int priority; }
    public static class EditorUtility { public static void SetDirty(UnityEngine.Object o) { } }
    public static class PlayerSettings { public static FullScreenMode fullScreenMode; public static int defaultScreenWidth, defaultScreenHeight; public static bool resizableWindow, runInBackground, visibleInBackground; }
    public class SerializedObject { public SerializedObject(UnityEngine.Object o) { } public SerializedProperty FindProperty(string n) => null; public bool ApplyModifiedPropertiesWithoutUndo() => true; }
    public class SerializedProperty { public UnityEngine.Object objectReferenceValue; public bool isArray; public int arraySize; public int intValue; public SerializedProperty GetArrayElementAtIndex(int i) => null; }
    public class EditorBuildSettingsScene { public EditorBuildSettingsScene(string p, bool e) { } }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
}
namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene }
    public enum NewSceneMode { Single }
    public static class EditorSceneManager { public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup s, NewSceneMode m) => default; public static bool SaveScene(UnityEngine.SceneManagement.Scene s, string p) => true; public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true; }
}
