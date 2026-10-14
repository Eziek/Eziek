using System.Collections.Generic;
using UnityEngine;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Localization;
using Wyspa.Visual;

namespace Wyspa.Alchemy
{
    /// <summary>
    /// Kocioł alchemiczny: klik → przepisy. Składniki bierze ze skrzyń na plony, warzy kilka minut
    /// (ogień i bąble), potem wywar świeci na fioletowo - klik „Wypij” włącza miksturę na 2 cykle gry (wabik: do przypłynięcia kupca).
    /// Warzenie liczy się w czasie gry (z przyspieszeniem z HUD debugowego), tylko gdy gra działa.
    /// </summary>
    public class Cauldron : MonoBehaviour, IContextActions
    {
        SpriteRenderer body, glow;
        Sprite idle;
        Sprite[] brewFrames, brewGlow, readyFrames, readyGlow;

        public static readonly List<Cauldron> All = new List<Cauldron>();

        /// <summary>Przepis, który ognik-alchemik warzy sam (null = nic); zapis w obiekcie.</summary>
        public string AutoRecipe { get; set; }
        public bool Idle => brewing == null;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        string brewing;      // id przepisu w kotle
        float left;          // sekundy do końca warzenia
        bool ready;

        public string Brewing => brewing;
        public float Left => left;
        public bool Ready => ready;

        public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, Sprite idleSprite, Sprite[] brew, Sprite[] brewG,
                          Sprite[] readyS, Sprite[] readyG)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            idle = idleSprite;
            brewFrames = brew;
            brewGlow = brewG;
            readyFrames = readyS;
            readyGlow = readyG;
        }

        /// <summary>Z zapisu: co się warzy, ile zostało, czy gotowe.</summary>
        public void Restore(string recipe, float secondsLeft, bool isReady)
        {
            brewing = Recipe.Find(recipe) != null ? recipe : null;
            left = brewing != null ? Mathf.Max(0f, secondsLeft) : 0f;
            ready = brewing != null && isReady;
        }

        void Update()
        {
            if (brewing != null && !ready)
            {
                float scale = GameLoop.Instance != null ? Mathf.Max(1f, GameLoop.Instance.DebugTimeScale) : 1f;
                left -= Time.unscaledDeltaTime * scale;
                if (left <= 0f)
                {
                    left = 0f;
                    ready = true;
                    IslandFx.FindPopped(transform.position + new Vector3(0f, 1f, 0f));
                    GameEvents.RaiseToast("toast.potion.ready", Loc.Get("potion." + brewing));
                    GameEvents.PlaySound("magic");
                }
            }
            Animate();
        }

        void Animate()
        {
            if (body == null)
                return;
            Sprite[] frames = null, glows = null;
            if (brewing != null && ready) { frames = readyFrames; glows = readyGlow; }
            else if (brewing != null) { frames = brewFrames; glows = brewGlow; }
            if (frames == null || frames.Length == 0)
            {
                body.sprite = idle;
                if (glow != null) glow.enabled = false;
                return;
            }
            int f = Mathf.FloorToInt(Time.unscaledTime * (ready ? 3f : 6f)) % frames.Length;
            body.sprite = frames[f];
            if (glow != null)
            {
                glow.enabled = glows != null && f < glows.Length && glows[f] != null;
                if (glow.enabled) glow.sprite = glows[f];
            }
        }

        void StartBrew(Recipe r)
        {
            var game = GameLoop.Instance;
            if (game == null || brewing != null || !Potions.CanBrew(r, game.Crops))
                return;
            Potions.TakeIngredients(r, game.Crops);
            BeginBrew(r);
        }

        /// <summary>Ognik-alchemik przyniósł składniki (już zabrane ze skrzyń): warzenie od razu.</summary>
        public void StartByWisp(Recipe r)
        {
            if (r != null && brewing == null)
                BeginBrew(r);
        }

        void BeginBrew(Recipe r)
        {
            brewing = r.id;
            left = r.brewSeconds;
            ready = false;
            IslandFx.FindPopped(transform.position + new Vector3(0f, 0.8f, 0f));
            GameEvents.PlaySound("bubble");
            GameEvents.RaiseToast("toast.potion.brewing", Loc.Get(r.NameKey), Mathf.CeilToInt(r.brewSeconds / 60f));
        }

        /// <summary>Gotowa mikstura (id) albo null.</summary>
        public string ReadyRecipe => ready ? brewing : null;

        public void Drink()
        {
            var r = Recipe.Find(brewing);
            if (r == null || !ready)
                return;
            Potions.Activate(r);
            brewing = null;
            ready = false;
            IslandFx.FindPopped(transform.position + new Vector3(0f, 1f, 0f));
            GameEvents.RaiseToast("toast.potion.drunk", Loc.Get(r.NameKey), Loc.Get(r.EffectKey));
        }

        static string Needs(Recipe r)
        {
            var game = GameLoop.Instance;
            var parts = new List<string>();
            foreach (var ing in r.needs)
            {
                int have = game != null ? Potions.Available(ing, game.Crops) : 0;
                parts.Add(Loc.Get("ui.cauldron.need", IngredientName(ing.what), Mathf.Min(have, ing.count), ing.count));
            }
            return string.Join(", ", parts);
        }

        /// <summary>Następny znany przepis dla alchemika (po ostatnim: nic).</summary>
        void CycleAuto()
        {
            var game = GameLoop.Instance;
            var known = new List<Recipe>();
            foreach (var r in Recipe.All)
                if (Known(r, game)) known.Add(r);
            int i = known.FindIndex(r => r.id == AutoRecipe);
            AutoRecipe = i + 1 < known.Count ? known[i + 1].id : null;
            StartCoroutine(ReopenPanel());
        }

        System.Collections.IEnumerator ReopenPanel()
        {
            yield return null;   // przycisk zamyka panel; otwieramy go od nowa z nowym wyborem
            var obj = GetComponent<World.TileObject>();
            if (obj != null)
                GameEvents.RequestContext(obj);
        }

        /// <summary>Przepis widać, gdy wszystkie konkretne rośliny z niego są odblokowane w sklepie nasion.</summary>
        public static bool Known(Recipe r, GameLoop game)
        {
            if (game == null || game.Crops == null)
                return true;
            foreach (var ing in r.needs)
                if (ing.what.StartsWith("crop:") && !game.IsUnlocked(game.Crops.Find(ing.what.Substring(5))))
                    return false;
            return true;
        }

        public static string IngredientName(string what)
        {
            switch (what)
            {
                case "day": return Loc.Get("ui.cauldron.any_day");
                case "night": return Loc.Get("ui.cauldron.any_night");
                case "fruit": return Loc.Get("ui.cauldron.any_fruit");
                default: return what.StartsWith("crop:") ? Loc.Get("crop." + what.Substring(5)) : what;
            }
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }

        public void FillContext(ContextInfo info)
        {
            info.verticalButtons = true;
            // działające mikstury
            foreach (var id in Potions.ActiveIds())
            {
                float sec = Potions.SecondsLeft(id);
                info.lines.Add(sec < 0f ? Loc.Get("ui.cauldron.active_until_use", Loc.Get("potion." + id))
                                        : Loc.Get("ui.cauldron.active", Loc.Get("potion." + id), FormatTime(sec)));
            }
            // ognik-alchemik z gniazda w pobliżu warzy sam wybraną miksturę (i pije ją, gdy przestaje działać)
            if (Wisp.AnyOfKind(WispKind.Alchemist))
            {
                var auto = Recipe.Find(AutoRecipe);
                info.Button(Loc.Get("ui.cauldron.auto", auto != null ? Loc.Get(auto.NameKey) : Loc.Get("ui.cauldron.auto_none")),
                            CycleAuto);
            }
            if (brewing != null)
            {
                var r = Recipe.Find(brewing);
                if (ready)
                {
                    info.lines.Add(Loc.Get(r.EffectKey));
                    info.Button(Loc.Get("ui.cauldron.drink", Loc.Get(r.NameKey)), Drink);
                }
                else
                    info.lines.Add(Loc.Get("ui.cauldron.brewing", Loc.Get(r.NameKey), FormatTime(left)));
                return;
            }
            // przepisy: jeden przycisk na przepis (nazwa, krótkie działanie, składniki ze skrzyń: masz / potrzeba);
            // przepisy z nasionami, których gracz jeszcze nie odblokował, są ukryte (panel mieści się w małym oknie)
            var game = GameLoop.Instance;
            foreach (var r in Recipe.All)
            {
                if (!Known(r, game))
                    continue;
                var recipe = r;
                bool can = game != null && Potions.CanBrew(r, game.Crops);
                info.Button(Loc.Get("ui.cauldron.recipe", Loc.Get(r.NameKey), Loc.Get("potion." + r.id + ".short"), Needs(r)),
                            () => StartBrew(recipe), can);
            }
        }
    }
}
