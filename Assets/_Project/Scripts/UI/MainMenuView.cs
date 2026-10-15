using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wyspa.Localization;
using Wyspa.Save;

namespace Wyspa.UI
{
    /// <summary>
    /// Menu główne: logo i trzy przyciski: Start (jeden zapis: wczytanie albo nowa wyspa), Ustawienia (Księga z samymi
    /// opcjami) i Wyjdź. W tle wyspa w burzy (Visual.MenuStorm): pioruny uderzają w morze za wyspą, klik w tło
    /// przywołuje piorun w to miejsce. Start ładuje scenę od nowa już z zapisem.
    /// </summary>
    public class MainMenuView : MonoBehaviour, IPointerClickHandler
    {
        const float W = 150f;
        const float ButtonH = 18f, ButtonGap = 5f;
        const float LogoGap = 6f;

        RectTransform root, panel;
        Visual.MenuStorm storm;

        public static MainMenuView Create(RectTransform canvasRoot)
        {
            // lekkie przyciemnienie na cały ekran: łapie kliknięcia (wyspa w tle nie reaguje, klik = piorun)
            var dim = Ui.Img(canvasRoot, "MainMenu", null, false, true);
            dim.color = new Color(0f, 0f, 0f, 0.15f);
            Ui.Stretch(dim.rectTransform);
            var view = dim.gameObject.AddComponent<MainMenuView>();
            view.root = dim.rectTransform;
            view.storm = new GameObject("MenuStorm").AddComponent<Visual.MenuStorm>();
            view.Build();
            return view;
        }

        void OnEnable() => Loc.Changed += Build;
        void OnDisable() => Loc.Changed -= Build;

        void OnDestroy()
        {
            if (storm != null)
                Destroy(storm.gameObject);
        }

        /// <summary>Klik w tło (nie w przyciski): piorun w to miejsce.</summary>
        public void OnPointerClick(PointerEventData e)
        {
            if (storm != null && e != null && e.pointerCurrentRaycast.gameObject == gameObject)   // nie w panel z przyciskami
                storm.StrikeAtScreen(e.position);
        }

        void Build()
        {
            if (root == null)
                return;
            if (panel != null)
                Destroy(panel.gameObject);
            var t = Ui.Theme;
            var logoSize = t.logo != null ? t.logo.rect.size : Vector2.zero;
            float top = t.logo != null ? logoSize.y + 2f * LogoGap : 20f;
            float w = Mathf.Max(W, logoSize.x + 16f);
            float h = top + 3 * (ButtonH + ButtonGap) + 14f;
            var p = Ui.Img(root, "Panel", t.panel, true, true);
            panel = Ui.Place(p.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(w, h));

            if (t.logo != null)
            {
                var logo = Ui.Img(panel, "Logo", t.logo, false);
                Ui.Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -LogoGap), logoSize);
            }
            else
            {
                var title = Ui.Txt(panel, "Title", t.textGold, TextAlignmentOptions.Top);
                title.text = Loc.Get("game.title");
                Ui.Place(title.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -6), new Vector2(w, 10));
            }

            float y = top;
            MenuButton("Start", Loc.Get("ui.menu.start"), y, w, () => SaveSystem.PlayMain());
            y += ButtonH + ButtonGap;
            MenuButton("Settings", Loc.Get("menu.options"), y, w, OpenSettings);
            y += ButtonH + ButtonGap;
            MenuButton("Quit", Loc.Get("act.quit"), y, w, UiRoot.QuitGame);

            // numer wersji pod przyciskami; sam numer, bez tekstu do tłumaczenia
            var ver = Ui.Txt(panel, "Version", t.textMuted, TextAlignmentOptions.Top);
            ver.text = "v" + Application.version;
            Ui.Place(ver.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -(h - 12f)), new Vector2(w, 10));
        }

        void MenuButton(string id, string label, float y, float w, System.Action onClick)
        {
            var b = Ui.Btn(panel, id, onClick);
            var txt = Ui.Txt(b.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
            txt.text = label;
            Ui.Stretch(txt.rectTransform);
            Ui.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -y), new Vector2(w - 30f, ButtonH));
        }

        /// <summary>Ustawienia: Księga otwarta na opcjach (w menu bez innych zakładek).</summary>
        static void OpenSettings()
        {
            var ui = UiRoot.Instance;
            if (ui != null && ui.Book != null)
                ui.Book.Open(BookView.Tab.Options);
        }
    }
}
