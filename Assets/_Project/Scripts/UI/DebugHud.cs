using UnityEngine;
using Wyspa.Core;
using Wyspa.Farming;

namespace Wyspa.UI
{
    /// <summary>
    /// Tymczasowy HUD (IMGUI) do testów: monety, Esencja, pora dnia, wybór nasion, przyspieszanie czasu.
    /// Docelowe UI w stylu pixel art przyjdzie później.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] bool visible = true;

        static Rect panelRect;
        static bool panelVisible;

        Vector2 scroll;

        public static bool IsPointerOverHud(Vector2 screenPos)
        {
            if (!panelVisible)
                return false;
            var gui = new Vector2(screenPos.x, Screen.height - screenPos.y); // IMGUI liczy y od góry
            return panelRect.Contains(gui);
        }

        void OnGUI()
        {
            panelVisible = visible;
            var game = GameLoop.Instance;
            if (!visible || game == null)
                return;

            panelRect = new Rect(8, 8, 230, Mathf.Min(Screen.height - 16, 330));
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);

            var clock = game.Clock;
            GUILayout.Label($"Monety: {game.Wallet.Coins}");
            GUILayout.Label($"Esencja: {game.Essence.Amount:0.0} / {game.Essence.Capacity:0}");
            DrawBar(game.Essence.Normalized);
            GUILayout.Label($"Godzina: {clock.Now:HH:mm}  {(clock.IsNight ? "NOC" : "dzień")}");
            GUILayout.Label($"Aktywny: {(game.Activity.IsActive ? "tak" : "nie")} ({game.Activity.SecondsSinceInput:0}s)");
            GUILayout.Label($"Księżyc: {clock.MoonPhase:0.00}{(clock.IsFullMoon ? "  PEŁNIA" : "")}");

            GUILayout.Space(4);
            GUILayout.Label("Nasiona:");
            if (game.Crops != null)
            {
                foreach (var crop in game.Crops.crops)
                {
                    if (crop == null)
                        continue;
                    string mark = SeedSelection.Current == crop ? "> " : "";
                    if (GUILayout.Button($"{mark}{crop.displayName} ({crop.seedPrice})"))
                        SeedSelection.Current = crop;
                }
            }

            GUILayout.Space(4);
            GUILayout.Label("Pora dnia (test):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("12:00")) SetHour(clock, 12f);
            if (GUILayout.Button("19:30")) SetHour(clock, 19.5f);
            if (GUILayout.Button("23:30")) SetHour(clock, 23.5f);
            GUILayout.EndHorizontal();
            if (GUILayout.Button(clock.UseDebugHour ? "Wróć do zegara systemowego" : "Zegar systemowy (aktywny)"))
                clock.UseDebugHour = false;

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Czas x{game.DebugTimeScale:0}");
            if (GUILayout.Button("x1")) game.DebugTimeScale = 1f;
            if (GUILayout.Button("x60")) game.DebugTimeScale = 60f;
            GUILayout.EndHorizontal();
            game.Activity.ForceActive = GUILayout.Toggle(game.Activity.ForceActive, "Wymuś aktywność");

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void SetHour(GameClock clock, float hour)
        {
            clock.UseDebugHour = true;
            clock.DebugHour = hour;
        }

        static void DrawBar(float t)
        {
            var r = GUILayoutUtility.GetRect(200, 10);
            GUI.Box(r, GUIContent.none);
            var fill = new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(t), r.height - 2);
            var old = GUI.color;
            GUI.color = new Color(0.34f, 0.43f, 0.32f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
