// Atrapy API Unity - tylko do sprawdzenia kompilacji kodu gry poza Unity.
#pragma warning disable
using System;
using System.Collections;
using System.Collections.Generic;
namespace UnityEngine
{
    public class Object { public string name; public static implicit operator bool(Object o) => o != null; public static void Destroy(Object o) { } public static T FindAnyObjectByType<T>() where T : Object => default; }
    public class Component : Object { public Transform transform; public GameObject gameObject; public T GetComponent<T>() => default; public T[] GetComponentsInChildren<T>(bool inactive) => null; public T GetComponentInChildren<T>() => default; public T[] GetComponents<T>() => null; public void SendMessage(string m, SendMessageOptions o) { } public bool TryGetComponent<T>(out T c) { c = default; return false; } public string tag; }
    public class Behaviour : Component { public bool enabled; public bool isActiveAndEnabled; }
    public class MonoBehaviour : Behaviour { public Coroutine StartCoroutine(IEnumerator e) => null; public void StopCoroutine(Coroutine c) { } public void StopAllCoroutines() { } }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject => default; }
    public class GameObject : Object { public GameObject(string n) { } public GameObject(string n, params Type[] components) { } public Transform transform; public string tag; public bool activeSelf; public void SetActive(bool v) { } public T AddComponent<T>() where T : Component => default; public T GetComponent<T>() => default; }
    public class Transform : Component, IEnumerable { public Vector3 position, localPosition, localScale; public Transform parent; public void SetParent(Transform p, bool w) { } public void SetAsLastSibling() { } public Vector3 TransformPoint(Vector3 p) => p; public Vector3 InverseTransformPoint(Vector3 p) => p; public IEnumerator GetEnumerator() => null; }
    public class RectTransform : Transform { public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, offsetMin, offsetMax; public Rect rect; }
    public static class RectTransformUtility { public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint) { localPoint = default; return false; } }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public class Canvas : Behaviour { public RenderMode renderMode; public bool pixelPerfect; public int sortingOrder; }
    public class CanvasGroup : Behaviour { public float alpha; public bool blocksRaycasts, interactable; }
    public class Font : Object { }
    public static class PlayerPrefs { public static bool HasKey(string k) => false; public static float GetFloat(string k) => 0; public static float GetFloat(string k, float d) => d; public static void SetFloat(string k, float v) { } public static int GetInt(string k) => 0; public static int GetInt(string k, int d) => d; public static void SetInt(string k, int v) { } public static string GetString(string k, string d = "") => d; public static void SetString(string k, string v) { } public static void Save() { } }
    public enum SystemLanguage { Chinese, German, Japanese, Polish, English, ChineseSimplified, ChineseTraditional, Unknown }
    public enum KeyCode { Escape, F1, LeftControl, RightControl }
    public class Renderer : Component { public Bounds bounds; public int sortingOrder; public Material sharedMaterial; public Material material; public bool enabled; public void GetPropertyBlock(MaterialPropertyBlock b) { } public void SetPropertyBlock(MaterialPropertyBlock b) { } }
    public class MaterialPropertyBlock { public void SetFloat(int id, float v) { } public void SetColor(int id, Color c) { } }
    public class SpriteRenderer : Renderer { public Sprite sprite; public Color color; public bool flipX, flipY; public SpriteMaskInteraction maskInteraction { get; set; } }
    public enum SpriteMaskInteraction { None = 0, VisibleInsideMask = 1, VisibleOutsideMask = 2 }
    public class SpriteMask : Renderer { public Sprite sprite { get; set; } public float alphaCutoff { get; set; } }
    public class Sprite : Object { public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu, uint extrude, SpriteMeshType mesh) => null; public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu) => null; public Bounds bounds; public Vector2 pivot; public Texture2D texture; }
    public struct Bounds { public Vector3 size, center, max, min; }
    public class Material : Object { public Material(Shader s) { } public Material(Material m) { } public Shader shader; public void SetFloat(int id, float v) { } public void SetFloat(string n, float v) { } public bool HasProperty(int id) => false; public float GetFloat(int id) => 0; public void SetColor(string n, Color c) { } public void SetColor(int id, Color c) { } public void SetTexture(string n, Texture t) { } public void SetTexture(int id, Texture t) { } public void SetVector(int id, Vector4 v) { } public void SetVectorArray(int id, Vector4[] v) { } }
    public class Shader : Object { public static Shader Find(string n) => null; public static void SetGlobalFloat(int id, float v) { } public static int PropertyToID(string n) => 0; public bool isSupported; }
    public class TextAsset : Object { public string text; }
    public class Texture : Object { public FilterMode filterMode; public TextureWrapMode wrapMode; }
    public enum TextureFormat { RGBA32 = 4 }
    public class Texture2D : Texture { public static Texture2D whiteTexture; public Texture2D(int w, int h, TextureFormat f, bool mip, bool linear) { } public void SetPixels32(Color32[] c) { } public void LoadRawTextureData(byte[] d) { } public void Apply(bool mips) { } }
    public class Camera : Behaviour { public static Camera main; public float aspect; public bool orthographic; public float orthographicSize; public CameraClearFlags clearFlags; public Color backgroundColor; public bool allowHDR; public Vector3 ScreenToWorldPoint(Vector3 p) => p; public Vector3 WorldToScreenPoint(Vector3 p) => p; }
    public enum CameraClearFlags { Skybox, SolidColor }
    public class Collider2D : Behaviour { public Vector2 offset; }
    public class BoxCollider2D : Collider2D { public Vector2 size; }
    public static class Input { public static Vector3 mousePosition; public static Vector2 mouseScrollDelta; public static bool GetKey(KeyCode k) => false; public static bool GetMouseButtonDown(int b) => false; public static bool GetMouseButton(int b) => false; public static bool GetMouseButtonUp(int b) => false; public static bool GetKeyDown(KeyCode k) => false; }
    public static class Physics2D { public static Collider2D OverlapPoint(Vector2 p) => null; public static Collider2D[] OverlapPointAll(Vector2 p) => null; }
    public class Coroutine { }
    public class WaitForSecondsRealtime { public WaitForSecondsRealtime(float s) { } }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 Min(Vector2 lhs, Vector2 rhs) => lhs; public static Vector2 Max(Vector2 lhs, Vector2 rhs) => lhs; public float sqrMagnitude => 0; public float magnitude => 0; public static float Distance(Vector2 a, Vector2 b) => 0; public static Vector2 operator /(Vector2 a, float f) => a; public static Vector2 zero, one; public static Vector2 operator -(Vector2 a, Vector2 b) => a; public static Vector2 operator +(Vector2 a, Vector2 b) => a; public static Vector2 operator *(Vector2 a, float f) => a; public static Vector2 operator *(float f, Vector2 a) => a; public static implicit operator Vector2(Vector3 v) => default; public static implicit operator Vector3(Vector2 v) => default; }
    public struct Vector2Int { public int x, y; public Vector2Int(int x, int y) { this.x = x; this.y = y; } public static implicit operator Vector2(Vector2Int v) => default; public static Vector2Int operator +(Vector2Int a, Vector2Int b) => a; public static Vector2Int zero; public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y; public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b); public override bool Equals(object o) => o is Vector2Int v && v == this; public override int GetHashCode() => x ^ y; }
    public struct Vector3 { public float x, y, z; public float sqrMagnitude => 0; public static float Distance(Vector3 a, Vector3 b) => 0; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } public static Vector3 zero, one; public static Vector3 operator -(Vector3 a, Vector3 b) => a; public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator *(Vector3 a, float f) => a; public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a; public static Vector3 ClampMagnitude(Vector3 vector, float maxLength) => vector; }
    public struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } public static Vector4 zero; }
    public struct Color { public static Color Lerp(Color a, Color b, float t) => a; public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white, clear; public static bool operator ==(Color a, Color b) => true; public static bool operator !=(Color a, Color b) => false; public override bool Equals(object o) => false; public override int GetHashCode() => 0; public static implicit operator Color(Color32 c) => default; }
    public struct Color32 { public Color32(byte r, byte g, byte b, byte a) { } }
    public struct Rect { public float x, y, width, height; public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; } public bool Contains(Vector2 p) => false; public float xMin => x; public float yMin => y; public float xMax => x + width; public float yMax => y + height; public Vector2 size => default; public Vector2 center => default; public static Rect MinMaxRect(float a, float b, float c, float d) => default; }
    public static class Mathf { public static float Sqrt(float f) => f; public static float Exp(float f) => f; public static float Pow(float f, float p) => f; public static float Atan2(float y, float x) => y; public static float Lerp(float a, float b, float t) => a; public const float PI = 3.14159265f; public static float Ceil(float f) => f; public static int CeilToInt(float f) => 0; public static float Round(float f) => f; public static float Cos(float f) => f; public static float MoveTowards(float c, float t, float d) => c; public static float Clamp(float v, float a, float b) => v; public static int Clamp(int v, int a, int b) => v; public static float Clamp01(float v) => v; public static float Min(float a, float b) => a; public static int Min(int a, int b) => a; public static float Max(float a, float b) => a; public static int Max(int a, int b) => a; public static float Floor(float f) => f; public static int FloorToInt(float f) => 0; public static int RoundToInt(float f) => 0; public static float Sin(float f) => f; public static float Abs(float f) => f; public static float Repeat(float t, float l) => t; public static bool Approximately(float a, float b) => false; }
    public static class Random { public static float value; public static float Range(float a, float b) => a; public static int Range(int a, int b) => a; }
    public static class Application { public static void Quit() { } public static SystemLanguage systemLanguage; public static bool runInBackground; public static bool isFocused; public static int targetFrameRate; public static string persistentDataPath; public static event Action<bool> focusChanged; }
    public static class QualitySettings { public static int vSyncCount; }
    public static class Time { public static float unscaledDeltaTime, unscaledTime, timeSinceLevelLoad; public static int frameCount; }
    public static class Screen { public static int width, height; public static FullScreenMode fullScreenMode; public static Resolution currentResolution; public static void SetResolution(int w, int h, FullScreenMode m) { } }
    public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } public static void LogError(object o) { } }
    public static class JsonUtility { public static string ToJson(object o, bool p) => ""; public static T FromJson<T>(string s) => default; }
    public class Gradient { public void SetKeys(GradientColorKey[] c, GradientAlphaKey[] a) { } public Color Evaluate(float t) => default; }
    public struct GradientColorKey { public GradientColorKey(Color c, float t) { } }
    public struct GradientAlphaKey { public GradientAlphaKey(float a, float t) { } }
    public class AnimationCurve { public AnimationCurve(params Keyframe[] k) { } public float Evaluate(float t) => 0; }
    public struct Keyframe { public Keyframe(float t, float v) { } }
    public enum FullScreenMode { ExclusiveFullScreen, FullScreenWindow, MaximizedWindow, Windowed }
    public enum SendMessageOptions { RequireReceiver, DontRequireReceiver }
    public struct Resolution { public int width, height; }
    public enum FilterMode { Point, Bilinear }
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
    public class AudioClip : Object { public float length; }
    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public float spatialBlend { get; set; }
        public bool isPlaying => false;
        public void Play() { }
        public void Stop() { }
        public void PlayOneShot(AudioClip clip, float volumeScale) { }
    }
    public class AudioListener : Behaviour { public static float volume { get; set; } }
    public static class Resources { public static T Load<T>(string path) where T : Object => default; }
    public enum RuntimeInitializeLoadType { AfterSceneLoad = 0, BeforeSceneLoad = 1, AfterAssembliesLoaded = 2, BeforeSplashScreen = 3, SubsystemRegistration = 4 }
    [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute() { } public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int o) { } }
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
}
namespace UnityEngine.SceneManagement { public struct Scene { public int buildIndex => 0; public string name => ""; } public static class SceneManager { public static Scene GetActiveScene() => default; public static void LoadScene(int buildIndex) { } } }
namespace AOT { [System.AttributeUsage(System.AttributeTargets.Method)] public class MonoPInvokeCallbackAttribute : System.Attribute { public MonoPInvokeCallbackAttribute(System.Type t) { } } }
namespace UnityEngine.Rendering { public class RenderPipelineAsset : UnityEngine.Object { } public static class GraphicsSettings { public static RenderPipelineAsset defaultRenderPipeline; } }
namespace UnityEngine.Rendering.Universal
{
    // UWAGA: klasa Renderer2D jest w URP 17 wewnętrzna (internal), więc nie ma jej w atrapach.
    public class UniversalRenderPipelineAsset : UnityEngine.Rendering.RenderPipelineAsset { }
    public class Light2D : UnityEngine.Behaviour { public enum LightType { Parametric, Freeform, Sprite, Point, Global } public LightType lightType { get; set; } public float intensity { get; set; } public UnityEngine.Color color { get; set; } public float pointLightOuterRadius { get; set; } public float pointLightInnerRadius { get; set; } public float falloffIntensity { get; set; } public float volumeIntensity { get; set; } public bool volumeIntensityEnabled { get; set; } public UnityEngine.Sprite lightCookieSprite { get; set; } }
    public class PixelPerfectCamera : UnityEngine.MonoBehaviour { public enum GridSnapping { None, PixelSnapping, UpscaleRenderTexture } public int assetsPPU { get; set; } public int refResolutionX { get; set; } public int refResolutionY { get; set; } public GridSnapping gridSnapping { get; set; } }
}
namespace UnityEngine.InputSystem
{
    public class ButtonControl { public bool wasPressedThisFrame, wasReleasedThisFrame, isPressed; }
    public class KeyControl : ButtonControl { }
    public class Vector2Control { public UnityEngine.Vector2 ReadValue() => default; }
    public class Mouse { public static Mouse current; public ButtonControl leftButton, rightButton; public Vector2Control position, scroll; }
    public class Keyboard { public static Keyboard current; public KeyControl escapeKey, f1Key, leftCtrlKey, rightCtrlKey; }
}
namespace UnityEditor
{
    using UnityEngine;
    public static class EditorApplication { public static bool isPlaying; }
    public class AssetImporter : UnityEngine.Object { }
    public class TextureImporter : AssetImporter { public bool sRGBTexture; public TextureImporterType textureType; public SpriteImportMode spriteImportMode; public float spritePixelsPerUnit; public FilterMode filterMode; public TextureImporterCompression textureCompression; public bool mipmapEnabled, alphaIsTransparency; public TextureWrapMode wrapMode; public void ReadTextureSettings(TextureImporterSettings s) { } public void SetTextureSettings(TextureImporterSettings s) { } }
    public class TextureImporterSettings { public SpriteMeshType spriteMeshType; public uint spriteExtrude; public int spriteAlignment; public Vector2 spritePivot; public Vector4 spriteBorder; }
    public enum TextureImporterType { Sprite }
    public enum SpriteImportMode { Single }
    public enum TextureImporterCompression { Uncompressed }
    public class AssetPostprocessor { public string assetPath; public AssetImporter assetImporter; }
    public enum ImportAssetOptions { ForceUpdate }
    public static class AssetDatabase { public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object => default; public static void CreateAsset(UnityEngine.Object o, string p) { } public static void SaveAssets() { } public static void Refresh() { } public static bool IsValidFolder(string p) => true; public static string CreateFolder(string a, string b) => ""; public static string[] FindAssets(string f, string[] d) => null; public static string GUIDToAssetPath(string g) => ""; public static void ImportAsset(string p, ImportAssetOptions o) { } }
    public class MenuItem : Attribute { public MenuItem(string s) { } public int priority; }
    public static class EditorUtility { public static void SetDirty(UnityEngine.Object o) { } }
    public static class PlayerSettings { public static FullScreenMode fullScreenMode; public static int defaultScreenWidth, defaultScreenHeight; public static bool useFlipModelSwapchain; public static bool resizableWindow, runInBackground, visibleInBackground; }
    public class SerializedObject { public SerializedObject(UnityEngine.Object o) { } public SerializedProperty FindProperty(string n) => null; public bool ApplyModifiedPropertiesWithoutUndo() => true; }
    public class SerializedProperty { public UnityEngine.Object objectReferenceValue; public bool isArray; public int arraySize; public int intValue; public SerializedProperty GetArrayElementAtIndex(int i) => null; public void InsertArrayElementAtIndex(int i) { } }
    public class EditorBuildSettingsScene { public EditorBuildSettingsScene(string p, bool e) { } }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
}
namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene }
    public enum NewSceneMode { Single }
    public static class EditorSceneManager { public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup s, NewSceneMode m) => default; public static bool SaveScene(UnityEngine.SceneManagement.Scene s, string p) => true; public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true; }
}

namespace UnityEngine.Events
{
    public class UnityEvent { public void AddListener(Action a) { } public void RemoveAllListeners() { } }
    public class UnityEvent<T0> { public void AddListener(Action<T0> a) { } public void RemoveAllListeners() { } }
}
namespace UnityEngine.EventSystems
{
    public class UIBehaviour : UnityEngine.MonoBehaviour { }
    public class EventSystem : UIBehaviour { public static EventSystem current; public bool IsPointerOverGameObject() => false; public void RaycastAll(PointerEventData e, System.Collections.Generic.List<RaycastResult> results) { } }
    public struct RaycastResult { public UnityEngine.GameObject gameObject; }
    public class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class BaseEventData { }
    public class PointerEventData : BaseEventData { public PointerEventData(EventSystem es) { } public UnityEngine.Vector2 position; }
    public interface IEventSystemHandler { }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData e); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData e); }
}
namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;
    public class Graphic : UIBehaviour { public UnityEngine.Color color; public bool raycastTarget; public UnityEngine.RectTransform rectTransform => null; }
    public class MaskableGraphic : Graphic { }
    public class Image : MaskableGraphic { public enum Type { Simple, Sliced, Tiled, Filled } public UnityEngine.Sprite sprite; public Type type; public float pixelsPerUnitMultiplier; public bool preserveAspect; }
    public struct SpriteState { public UnityEngine.Sprite highlightedSprite, pressedSprite, selectedSprite, disabledSprite; }
    public struct Navigation { public enum Mode { None, Horizontal, Vertical, Automatic, Explicit } public Mode mode; }
    public class Selectable : UIBehaviour { public enum Transition { None, ColorTint, SpriteSwap, Animation } public Transition transition; public Graphic targetGraphic; public SpriteState spriteState; public Navigation navigation; public bool interactable; }
    public class Button : Selectable { public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { } public ButtonClickedEvent onClick; }
    public class CanvasScaler : UIBehaviour { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize } public ScaleMode uiScaleMode; public float referencePixelsPerUnit; public float scaleFactor; }
    public class GraphicRaycaster : UIBehaviour { }
    public class RectMask2D : UIBehaviour { }
}
namespace UnityEngine.InputSystem.UI { public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { } }
namespace UnityEngine.TextCore.LowLevel { public enum GlyphRenderMode { SMOOTH_HINTED = 4121, SMOOTH = 4117, RASTER_HINTED = 4161, RASTER = 4157, SDF = 4134, SDFAA = 4165 } }
namespace TMPro
{
    using UnityEngine;
    public enum TextAlignmentOptions { TopLeft = 257, Top = 258, TopRight = 260, Left = 513, Center = 514, Right = 516, BottomLeft = 1025, BottomRight = 1028 }
    public enum TextWrappingModes { NoWrap = 0, Normal = 1, PreserveWhitespace = 2, PreserveWhitespaceNoWrap = 3 }
    public enum TextOverflowModes { Overflow = 0, Ellipsis = 1, Masking = 2, Truncate = 3 }
    public enum AtlasPopulationMode { Static = 0, Dynamic = 1, DynamicOS = 2 }
    public class TMP_Settings : ScriptableObject { public static TMP_Settings instance; }
    public class TMP_FontAsset : ScriptableObject
    { public System.Collections.Generic.List<TMP_FontAsset> fallbackFontAssetTable;
        public Texture2D atlasTexture;
        public static TMP_FontAsset CreateFontAsset(Font font, int samplingPointSize, int atlasPadding, UnityEngine.TextCore.LowLevel.GlyphRenderMode renderMode, int atlasWidth, int atlasHeight, AtlasPopulationMode atlasPopulationMode = AtlasPopulationMode.Dynamic, bool enableMultiAtlasSupport = true) => null;
    }
    public class TMP_Text : UnityEngine.UI.MaskableGraphic
    {
        public string text; public TMP_FontAsset font; public float fontSize; public TextAlignmentOptions alignment;
        public TextWrappingModes textWrappingMode; public TextOverflowModes overflowMode; public bool richText;
        public Vector2 GetPreferredValues(string text) => default; public Vector2 GetPreferredValues(string text, float width, float height) => default; public void ForceMeshUpdate() { }
    }
    public class TextMeshProUGUI : TMP_Text { }
    public class TMP_InputField : UnityEngine.UI.Selectable
    {
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        public class OnChangeEvent : UnityEngine.Events.UnityEvent<string> { }
        public class SubmitEvent : UnityEngine.Events.UnityEvent<string> { }
        public string text { get; set; }
        public int characterLimit { get; set; }
        public LineType lineType { get; set; }
        public RectTransform textViewport { get; set; }
        public TMP_Text textComponent { get; set; }
        public UnityEngine.UI.Graphic placeholder { get; set; }
        public Color caretColor { get; set; }
        public bool customCaretColor { get; set; }
        public int caretWidth { get; set; }
        public Color selectionColor { get; set; }
        public OnChangeEvent onValueChanged { get; set; }
        public SubmitEvent onSubmit { get; set; }
        public void ActivateInputField() { }
    }
}
