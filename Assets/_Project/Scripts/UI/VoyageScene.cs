using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Automation;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Lighting;
using Wyspa.Localization;
using Wyspa.Save;
using Wyspa.World;

namespace Wyspa.UI
{
    /// <summary>
    /// Scenki rejsu we mgłę (GDD §11.8).
    /// Odpłynięcie: interfejs znika, ogniki wracają do gniazd, lampy gasną po kolei (od najdalszej), kamera zsuwa się
    /// w stronę morza, a mgła zamyka okno od brzegów; potem Przystań we mgle.
    /// Przypłynięcie (nowa wyspa po rejsie): okno zaczyna w mgle, mgła rozstępuje się od środka, kamera podpływa do wyspy,
    /// latarnia rozbłyska, a na dole pojawia się linijka zapisków Opiekuna (inna co rejs). Klik przyspiesza.
    /// </summary>
    public class VoyageScene : MonoBehaviour
    {
        public const int ArriveLines = 8;

        public static bool Playing { get; private set; }

        RectTransform canvasRoot;
        MistCurtain curtain;
        Image blocker;
        float speed = 1f;

        public static VoyageScene Create(RectTransform canvasRoot)
        {
            Playing = false;   // statyczne: odpłynięcie kończy się przeładowaniem sceny w trakcie scenki
            var go = Ui.Rect("VoyageScene", canvasRoot).gameObject;
            var scene = go.AddComponent<VoyageScene>();
            scene.canvasRoot = canvasRoot;
            // gra po rejsie: od pierwszej klatki mgła na całe okno (scenka ruszy po wczytaniu zapisu)
            if (!SaveSystem.InMenu && SaveSystem.PeekArrival())
            {
                scene.Begin();
                scene.curtain.Coverage = 1f;
            }
            return scene;
        }

        float Dt => Time.unscaledDeltaTime * speed;

        /// <summary>Wejście w scenkę: blokada klików (klik = szybciej), bez HUD, kamera bez przeciągania.</summary>
        void Begin()
        {
            Playing = true;
            speed = 1f;
            if (blocker == null)
            {
                blocker = Ui.Img(canvasRoot, "SceneBlocker", null, false, true);
                blocker.color = new Color(0f, 0f, 0f, 0f);
                Ui.Stretch(blocker.rectTransform);
                var b = blocker.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => speed = 3f);
            }
            blocker.gameObject.SetActive(true);
            blocker.transform.SetAsLastSibling();
            if (curtain == null)
                curtain = MistCurtain.Create(canvasRoot, 0f);
            curtain.transform.SetAsLastSibling();
            var ui = UiRoot.Instance;
            if (ui != null && ui.Hud != null)
                ui.Hud.gameObject.SetActive(false);
            if (ViewController.Instance != null)
                ViewController.Instance.Locked = true;
        }

        void End()
        {
            Playing = false;
            if (blocker != null)
                blocker.gameObject.SetActive(false);
            var ui = UiRoot.Instance;
            if (ui != null && ui.Hud != null)
                ui.Hud.gameObject.SetActive(true);
            if (ViewController.Instance != null)
            {
                ViewController.Instance.Locked = false;
                ViewController.Instance.SceneOffset = Vector2.zero;
            }
        }

        void Start()
        {
            if (Playing && !SaveSystem.InMenu)
                StartCoroutine(Arrive());
        }

        // ------------------------------------------------------------ odpłynięcie

        public void Depart(Action done)
        {
            if (Playing)
                return;
            StartCoroutine(DepartRoutine(done));
        }

        IEnumerator DepartRoutine(Action done)
        {
            Begin();
            GameEvents.PlaySound("magic", 0.5f);
            foreach (var w in Wisp.All.ToArray())
                w.RecallHome();
            var origin = IslandCenter();
            Lamp.ChainExtinguish(origin, 0.25f);
            var line = Line(Loc.Get("voyage.depart"), null);
            var view = ViewController.Instance;
            bool closing = false;
            for (float t = 0f; t < 5.2f; t += Dt)
            {
                if (view != null)
                    view.SceneOffset = new Vector2(0f, -Mathf.Round(Smooth(Mathf.Clamp01(t / 5f)) * 36f));   // kamera zsuwa się ku morzu
                Type(line, t, 0.4f);
                if (!closing && t >= 1.2f)
                {
                    closing = true;
                    StartCoroutine(ScaledTo(1f, 3.4f));
                }
                yield return null;
            }
            curtain.Coverage = 1f;
            GameEvents.PlaySound("tile", 0.6f);
            yield return Wait(0.4f);
            done?.Invoke();   // SailIntoMist ładuje scenę Przystani (scenka kończy się razem ze sceną)
        }

        // ------------------------------------------------------------ przypłynięcie

        IEnumerator Arrive()
        {
            // czekamy, aż zapis się wczyta (GameLoop.Start) i wyspa stanie
            for (int i = 0; i < 30 && (Island.Instance == null || !Island.Instance.Ready); i++)
                yield return null;
            yield return null;
            var view = ViewController.Instance;
            var title = Loc.Get("voyage.arrive.title", Prestige.Voyages + 1);
            int k = Mathf.Max(0, Prestige.Voyages - 1) % ArriveLines;
            var line = Line(Loc.Get("voyage.arrive." + k), title);
            line.gameObject.SetActive(false);
            yield return Wait(0.4f);
            GameEvents.PlaySound("tile", 0.7f);
            StartCoroutine(ScaledTo(0f, 3.2f));
            bool flared = false;
            for (float t = 0f; t < 3.6f; t += Dt)
            {
                if (view != null)
                    view.SceneOffset = new Vector2(0f, -Mathf.Round((1f - Smooth(Mathf.Clamp01(t / 3.4f))) * 40f));   // podpływamy
                if (!flared && t >= 2.4f)
                {
                    flared = true;
                    var lamp = NearestLamp(IslandCenter());
                    if (lamp != null)
                    {
                        lamp.Flare(4f);
                        Visual.IslandFx.FindPopped(lamp.LightCenter);
                        GameEvents.PlaySound("magic", 0.7f);
                    }
                }
                yield return null;
            }
            if (view != null)
                view.SceneOffset = Vector2.zero;
            curtain.Coverage = 0f;
            // zapiski Opiekuna: litera po literze, potem chwila na przeczytanie
            line.gameObject.SetActive(true);
            speed = 1f;
            for (float t = 0f; t < 5.5f && speed < 2f; t += Dt)
            {
                Type(line, t, 0f);
                yield return null;
            }
            Destroy(line.gameObject);
            End();
        }

        // ------------------------------------------------------------ pomocnicze

        /// <summary>Tabliczka z tekstem na dole okna (tytuł złoty nad linijką). Tekst pisze się w Type.</summary>
        TextMeshProUGUI Line(string text, string title)
        {
            var t = Ui.Theme;
            var plate = Ui.Img(canvasRoot, "SceneLine", t.tooltip, true);
            float w = Mathf.Min(canvasRoot.rect.width - 16f, 260f);
            var rt = Ui.Place(plate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(w, title != null ? 34f : 24f));
            plate.transform.SetSiblingIndex(curtain != null ? curtain.transform.GetSiblingIndex() + 1 : canvasRoot.childCount - 1);
            if (title != null)
            {
                var head = Ui.Txt(rt, "Title", t.textGold, TextAlignmentOptions.Top);
                head.text = title;
                Ui.Place(head.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -3), new Vector2(w, 10));
            }
            var body = Ui.Txt(rt, "Text", t.textLight, TextAlignmentOptions.Top);
            body.textWrappingMode = TextWrappingModes.Normal;
            Ui.Place(body.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(6, title != null ? -14 : -4), new Vector2(w - 12, 20));
            body.text = text;
            body.maxVisibleCharacters = 0;
            return body;
        }

        /// <summary>Pisanie litera po literze (ok. 30 znaków na sekundę) od chwili start.</summary>
        static void Type(TextMeshProUGUI body, float t, float start)
        {
            if (body != null)
                body.maxVisibleCharacters = Mathf.Max(0, Mathf.FloorToInt((t - start) * 30f));
        }

        IEnumerator ScaledTo(float target, float seconds)
        {
            float from = curtain.Coverage;
            for (float t = 0f; t < seconds; t += Dt)
            {
                curtain.Coverage = Mathf.Lerp(from, target, Smooth(t / seconds));
                yield return null;
            }
            curtain.Coverage = target;
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
                yield return null;
        }

        static float Smooth(float u) => u * u * (3f - 2f * u);

        static Vector2 IslandCenter()
        {
            var lamp = NearestLamp(Island.TileCenter(new TilePos(15, 15)));
            return lamp != null ? (Vector2)lamp.transform.position : Island.TileCenter(new TilePos(15, 15));
        }

        static Lamp NearestLamp(Vector2 to)
        {
            Lamp best = null;
            foreach (var l in Lamp.All)
                if (l != null && (best == null || ((Vector2)l.transform.position - to).sqrMagnitude < ((Vector2)best.transform.position - to).sqrMagnitude))
                    best = l;
            return best;
        }
    }
}
