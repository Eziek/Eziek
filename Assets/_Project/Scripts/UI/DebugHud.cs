using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Upgrades;

namespace Wyspa.UI
{
    /// <summary>
    /// Tymczasowy HUD DEBUGOWY (IMGUI) do testów: waluty, Esencja, rytm pracy, nasiona, ulepszenia, pora dnia, czas.
    /// Nie trafia do gry - napisy są tu na sztywno celowo. Docelowe UI (GDD §12) idzie przez lokalizację (GDD §16).
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

            panelRect = new Rect(8, 8, 250, Mathf.Min(Screen.height - 16, 560));
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);

            var clock = game.Clock;
            var wallet = game.Wallet;
            GUILayout.Label($"Monety: {wallet.Coins}   Plony nocy: {wallet.Get(CurrencyType.NightHarvest)}");
            GUILayout.Label($"Esencja: {game.Essence.Amount:0.0} / {game.Essence.Capacity:0}");
            DrawBar(game.Essence.Normalized);

            var r = game.Rhythm;
            string state = r.FatigueLevel == 2 ? "duże zmęczenie" : (r.FatigueLevel == 1 ? "zmęczenie" : "");
            if (r.Refreshed) state += (state.Length > 0 ? ", " : "") + $"odświeżony {r.RefreshedMinutesLeft:0} min";
            GUILayout.Label($"Seria: {r.StreakMinutes:0} min{(r.StreakBonusActive ? " (+25%)" : "")}");
            GUILayout.Label($"Dziś: {r.DailyActiveHours:0.0} h  x{r.EssenceMultiplier:0.00} {state}");

            GUILayout.Label($"Godzina: {clock.Now:HH:mm}  {(clock.IsNight ? "NOC" : "dzień")}");
            GUILayout.Label($"Aktywny: {(game.Activity.IsActive ? "tak" : "nie")} ({game.Activity.SecondsSinceInput:0}s)");
            GUILayout.Label($"Księżyc: {clock.MoonPhase:0.00}{(clock.IsFullMoon ? "  PEŁNIA" : "")}");

            GUILayout.Space(4);
            GUILayout.Label("Nasiona:");
            if (game.Crops == null || game.Crops.crops.Count == 0)
                GUILayout.Label("(brak bazy roślin: obiekt Game > GameLoop > Crops)");
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
            GUILayout.Label("Ulepszenia:");
            var ups = game.Upgrades;
            if (ups.Database == null)
                GUILayout.Label("(brak bazy ulepszeń: uruchom Wyspa > 3)");
            else
            {
                foreach (var def in ups.Database.upgrades)
                {
                    if (def == null)
                        continue;
                    var next = ups.NextLevel(def);
                    string lvl = $"{ups.Level(def.id)}/{def.MaxLevel}";
                    if (next == null)
                    {
                        GUILayout.Label($"{def.debugName} {lvl} (max)");
                        continue;
                    }
                    string cost = "";
                    foreach (var c in next.cost)
                        cost += $" {c.amount} {Short(c.type)}";
                    GUI.enabled = ups.CanBuy(def, wallet);
                    string req = ups.RequirementsMet(def) ? "" : " [wymaga]";
                    if (GUILayout.Button($"{def.debugName} {lvl}:{cost}{req}"))
                        ups.TryBuy(def, wallet);
                    GUI.enabled = true;
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
            r.RhythmEnabled = GUILayout.Toggle(r.RhythmEnabled, "Rytm pracy (seria/przerwa/zmęczenie)");
            if (GUILayout.Button("Cheat: +500 monet, +5 plonów nocy"))
            {
                wallet.Add(CurrencyType.Coins, 500);
                wallet.Add(CurrencyType.NightHarvest, 5);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static string Short(CurrencyType t)
        {
            switch (t)
            {
                case CurrencyType.Coins: return "mon.";
                case CurrencyType.NightHarvest: return "pl.nocy";
                case CurrencyType.StarShard: return "odł.";
                case CurrencyType.Scale: return "łusk.";
                case CurrencyType.DeepGift: return "dar";
                case CurrencyType.Wood: return "drewno";
                case CurrencyType.Stone: return "kamień";
                default: return t.ToString();
            }
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
