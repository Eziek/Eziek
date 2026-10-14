using UnityEngine;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Farming;
using Wyspa.Lighting;
using Wyspa.Upgrades;
using Wyspa.Visual;
using Wyspa.World;

namespace Wyspa.UI
{
    /// <summary>
    /// Tymczasowy HUD DEBUGOWY (IMGUI) do testów: waluty, Esencja, rytm pracy, nasiona, ulepszenia, pora dnia, czas.
    /// Nie trafia do gry - napisy są tu na sztywno celowo. Docelowe UI (GDD §12) idzie przez lokalizację (GDD §16).
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        [Tooltip("F1 przełącza. Bez docelowego UI (UiRoot) HUD debugowy jest widoczny od startu.")]
        [SerializeField] bool visible;

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

        void Start()
        {
            if (UiRoot.Instance == null)
                visible = true;
        }

        void Update()
        {
            if (UiInput.F1Pressed)
                visible = !visible;
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
            GUILayout.Label($"Aktywny: {(game.Activity.IsActive ? "tak" : "nie")} ({game.Activity.SecondsSinceInput:0}s / próg {game.Activity.IdleThresholdSeconds:0}s, czas rzeczywisty)");
            GUILayout.Label(PlotUnderMouse(game));
            GUILayout.Label($"Księżyc: {clock.MoonPhase:0.00}{(clock.IsFullMoon ? "  PEŁNIA" : "")}");

            var ups = game.Upgrades;

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

            var auto = game.Automation;
            if (auto != null && auto.Crate != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Skrzynia: {auto.Crate.Count}/{auto.Crate.Capacity}");
                if (GUILayout.Button("Sprzedaj"))
                    auto.Crate.SellAll(wallet, game.Crops, 1f);
                GUILayout.EndHorizontal();
                if (ups.Has(UpgradeIds.WispGardener) && auto.Wisp != null)
                    GUILayout.Label($"Ognik: następny zbiór za {auto.Wisp.SecondsToNext(ups.Value(UpgradeIds.WispGardener, 30f)):0} s");
                if (ups.Has(UpgradeIds.MerchantChest) && auto.MerchantChest != null)
                    GUILayout.Label($"Kupiec: sprzedaż za {auto.MerchantChest.SecondsToNext(ups.Value(UpgradeIds.MerchantChest, 3600f)) / 60f:0} min");
            }
            else
                GUILayout.Label("(brak automatyki w scenie: Wyspa > 3)");

            GUILayout.Space(4);
            GUILayout.Label("Ulepszenia:");
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
            bool fa = GUILayout.Toggle(game.Activity.ForceActive, "Wymuś aktywność");
            if (fa != game.Activity.ForceActive) game.Activity.ForceActive = fa;
            bool fi = GUILayout.Toggle(game.Activity.ForceIdle, "Wymuś bezczynność (przerwa)");
            if (fi != game.Activity.ForceIdle) game.Activity.ForceIdle = fi;
            Lamp.DebugAllOff = GUILayout.Toggle(Lamp.DebugAllOff, "Zgaś lampy (test roślin nocy)");
            r.RhythmEnabled = GUILayout.Toggle(r.RhythmEnabled, "Rytm pracy (seria/przerwa/zmęczenie)");
            GameClock.DebugFullMoon = GUILayout.Toggle(GameClock.DebugFullMoon, "Wymuś pełnię (rośliny nocy x2, krwawa róża)");
            GameLoop.DebugAlwaysMutate = GUILayout.Toggle(GameLoop.DebugAlwaysMutate, "Mutacje zawsze (zamiast 3%)");
            bool fx = GUILayout.Toggle(FxMaterial.Enabled, "Shadery efektów (woda, mgła, kołysanie)");
            if (fx != FxMaterial.Enabled) FxMaterial.Enabled = fx;
            if (GUILayout.Button("Cheat: +500 monet, +5 plonów nocy"))
            {
                wallet.Add(CurrencyType.Coins, 500);
                wallet.Add(CurrencyType.NightHarvest, 5);
            }
            if (GUILayout.Button("Cheat: +30 Esencji"))
                game.Essence.Set(game.Essence.Amount + 30f);
            var merchant = Wyspa.Sea.GhostMerchant.Instance;
            if (merchant != null && !merchant.Present && GUILayout.Button("Przywołaj kupca-widmo"))
                merchant.Arrive();
            var events = Wyspa.Sea.NightEvents.Instance;
            if (events != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ognik")) events.SpawnWisp();
                if (GUILayout.Button("Gwiazda")) events.SpawnStar();
                if (GUILayout.Button("Potwór")) events.SpawnMonster();
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"Worek: nawóz {game.Fertilizer}, hybrydy {game.Seeds.Count("hybridpumpkin")}");

            var island = game.Island;
            if (island != null && island.Ready)
            {
                GUILayout.Space(6);
                var view = ViewController.Instance;
                GUILayout.Label($"Wyspa: ziarno {island.Map.Seed}, {island.Map.Count} kafelków (kupione {island.Map.Purchased})");
                GUILayout.Label($"Następny kafelek: {island.NextTileCoins} monet + {island.NextTileEssence} Esencji, grządka {island.DigCoins}");
                if (view != null)
                    GUILayout.Label($"Kadr {view.View.x}x{view.View.y}, skala x{view.PixelRatio}, UI x{UiRoot.UiScale()}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+1 kafelek")) island.DebugBuyRandom(1);
                if (GUILayout.Button("+10 kafelków")) island.DebugBuyRandom(10);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Nowy świat (nowe ziarno)"))
                    island.NewWorld(GameLoop.NewSeed());
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>Stan grządki pod kursorem: co rośnie, czy jest w świetle i dlaczego nie rośnie.</summary>
        static string PlotUnderMouse(GameLoop game)
        {
            var cam = Camera.main;
            if (cam == null || Event.current == null)
                return "Grządka: -";
            var m = Event.current.mousePosition;
            Vector2 world = cam.ScreenToWorldPoint(new Vector3(m.x, Screen.height - m.y, 0f));
            var hit = Physics2D.OverlapPoint(world);
            if (hit == null || !hit.TryGetComponent(out FarmPlot plot))
                return "Grządka: (najedź kursorem na grządkę)";
            var s = plot.State;
            bool night = game.Clock.IsNight;
            var ctx = game.ContextFor(plot, night);
            string light = ctx.lit ? (night ? "w świetle lampy" : "w świetle dnia") : "w ciemności";
            if (s.IsEmpty)
                return $"Grządka: pusta, {light}";
            if (s.IsRipe)
                return $"Grządka: {s.crop.displayName} dojrzała, {light}";
            string why;
            if (s.CanGrow(ctx))
                why = "rośnie";
            else if (s.crop.kind == CropKind.Day)
                why = !ctx.lit ? "nie rośnie: potrzebuje światła" : "nie rośnie: potrzebuje Twojej aktywności";
            else
                why = !night ? "nie rośnie: czeka na noc" : "nie rośnie: za jasno (lampa)";
            return $"Grządka: {s.crop.displayName} faza {s.Stage}, {light}, {why}";
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
