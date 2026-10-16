using UnityEngine;
using Wyspa.Automation;
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
        static int debugFx;

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

            GUILayout.Label($"Godzina: {clock.Now:HH:mm}  {(clock.IsNight ? "NOC" : "dzień")}");
            GUILayout.Label(PlotUnderMouse(game));
            GUILayout.Label($"Księżyc: {clock.MoonPhase:0.00}{(clock.IsFullMoon ? "  PEŁNIA" : "")}   Pogoda: {Weather.WeatherSystem.Current}");
            GUILayout.Label($"Pora roku: {clock.Season}, dzień {clock.SeasonDay}/{GameClock.SeasonCycles}{(GameClock.DebugSeason != null ? " (wymuszona)" : "")}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pogoda: z cyklu")) Weather.WeatherSystem.DebugForce = null;
            if (GUILayout.Button("Deszcz")) Weather.WeatherSystem.DebugForce = Weather.WeatherKind.Rain;
            if (GUILayout.Button("Burza")) Weather.WeatherSystem.DebugForce = Weather.WeatherKind.Storm;
            if (GUILayout.Button("Mgła")) Weather.WeatherSystem.DebugForce = Weather.WeatherKind.Mist;
            GUILayout.EndHorizontal();

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
            if (auto != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Skrzynie: {HarvestCrate.All.Count}, plonów {HarvestCrate.TotalCount()}");
                if (GUILayout.Button("Sprzedaj"))
                    foreach (var c in HarvestCrate.All.ToArray())
                        c.SellAll(wallet, game.Crops, Wyspa.Economy.SaleChannel.Crate);
                GUILayout.EndHorizontal();
                if (FruitTree.All.Count > 0 && GUILayout.Button($"Drzewa ({FruitTree.All.Count}): dorosłe z owocami"))
                    foreach (var t in FruitTree.All.ToArray())
                        t.DebugMature();
                if (GUILayout.Button("Odblokuj wszystkie nasiona"))
                    game.DebugUnlockAll();
                if (GUILayout.Button($"Perły mgły +5 (jest {Economy.Prestige.Pearls})"))
                    Economy.Prestige.DebugAddPearls(5);
                GUILayout.Label($"Ogniki: {Wisp.All.Count}, gniazda: {WispNest.All.Count}, duszki: {MerchantSpirit.All.Count}, runy: {WildRune.All.Count}");
                if (Wisp.All.Count > 0 && GUILayout.Button("Ogniki: +50 doświadczenia"))
                    foreach (var w in Wisp.All.ToArray())
                        w.DebugAddXp(50);
                if (MerchantSpirit.All.Count > 0)
                    GUILayout.Label($"Duszki-handlarze: {MerchantSpirit.All.Count} (sprzedają pełne skrzynie)");
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
            if (GUILayout.Button("+20 min")) GameClock.DebugOffsetSeconds += 1200;
            if (GUILayout.Button("+1 cykl")) GameClock.DebugOffsetSeconds += GameClock.CycleSeconds;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Wyspa.Familiars.FamiliarSystem.Instance != null && GUILayout.Button("Wszystkie chowańce"))
                Wyspa.Familiars.FamiliarSystem.Instance.DebugArriveAll();
            if (Wyspa.Familiars.FamiliarSystem.Instance != null && GUILayout.Button("Więź +1"))
                Wyspa.Familiars.FamiliarSystem.Instance.DebugBondAll();
            if (GUILayout.Button("Ogniki zmęczone"))
                foreach (var w in Wyspa.Automation.Wisp.All) w.SetMood(15f);
            if (GUILayout.Button("Ogniki wypoczęte"))
                foreach (var w in Wyspa.Automation.Wisp.All) w.SetMood(100f);
            if (Wyspa.Orders.OrderBook.Instance != null && GUILayout.Button("Zamówienie teraz"))
                Wyspa.Orders.OrderBook.Instance.DebugNextNow();
            GUILayout.EndHorizontal();
            Lamp.DebugAllOff = GUILayout.Toggle(Lamp.DebugAllOff, "Zgaś lampy (test roślin nocy)");
            GameClock.DebugFullMoon = GUILayout.Toggle(GameClock.DebugFullMoon, "Wymuś pełnię (rośliny nocy x2, krwawa róża)");
            GameLoop.DebugAlwaysMutate = GUILayout.Toggle(GameLoop.DebugAlwaysMutate, "Mutacje zawsze (zamiast 3%)");
            bool fx = GUILayout.Toggle(FxMaterial.Enabled, "Shadery efektów (woda, mgła, kołysanie)");
            if (fx != FxMaterial.Enabled) FxMaterial.Enabled = fx;
            // diagnostyka wody i mgły: 1 = dane wody (odległość czerwona, klasa zielona), 2 = cały sprite różowy
            if (GUILayout.Button($"Diagnostyka wody/mgły: {debugFx} (0 zwykle, 1 dane, 2 różowe)"))
            {
                debugFx = (debugFx + 1) % 3;
                Shader.SetGlobalFloat("_WyspaDebugFx", debugFx);
            }
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
            var chronicle = Wyspa.Lore.Chronicle.Instance;
            if (chronicle != null)
            {
                GUILayout.Label($"Księżyc: {chronicle.MoonPoints} pkt, cykl da {chronicle.PendingPoints}");
                if (GUILayout.Button("Zamknij cykl księżyca teraz"))
                    chronicle.CloseCycle(GameClock.CycleIndex(System.DateTime.UtcNow));
                if (GUILayout.Button("+5 pkt księżyca"))
                    chronicle.DebugAddPoints(5);
            }
            var events = Wyspa.Sea.NightEvents.Instance;
            if (events != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ognik")) events.SpawnWisp();
                if (GUILayout.Button("Gwiazda")) events.SpawnStar();
                if (GUILayout.Button("Potwór")) events.SpawnMonster();
                GUILayout.EndHorizontal();
            }
            int hybrids = 0;
            if (game.Crops != null)
                foreach (var c in game.Crops.crops)
                    if (c != null && c.hybrid) hybrids += game.Seeds.Count(c.id);
            GUILayout.Label($"Worek: nawóz {game.Fertilizer}, nasiona hybryd {hybrids}");
            var boat = Island.Instance != null ? Island.Instance.Boat : null;
            if (boat != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Wyprawa: wróć teraz")) boat.DebugFinish();
                if (GUILayout.Button("Wyprawy +3 (cele)")) boat.DebugAddTrips(3);
                GUILayout.EndHorizontal();
            }
            if (!Wyspa.Lore.Story.Finished && GUILayout.Button("Historia: odkryj zapiski (finał przy ołtarzu)"))
                foreach (var k in Wyspa.Lore.LoreBook.All)
                    if (System.Array.IndexOf(Wyspa.Lore.Story.Ending, k) < 0)
                        Wyspa.Lore.Chronicle.Note("lore:" + k);

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
                if (GUILayout.Button("Do wysepki")) island.DebugTowardIslet();
                if (GUILayout.Button("Monument: etap")) island.DebugAdvanceMonuments();
                if (GUILayout.Button("Rzadka noc") && Visual.RareNights.Instance != null) Visual.RareNights.Instance.DebugNext();
                if (GUILayout.Button("Horyzont") && Visual.HorizonSights.Instance != null) Visual.HorizonSights.Instance.DebugNext();
                if (GUILayout.Button("Pora roku: następna")) Seasons.DebugNext(game.Clock);
                if (GUILayout.Button("Pora roku: z cyklu")) GameClock.DebugSeason = null;
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
