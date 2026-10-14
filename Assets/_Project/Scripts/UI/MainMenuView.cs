using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Localization;
using Wyspa.Save;

namespace Wyspa.UI
{
    /// <summary>
    /// Menu główne: trzy sloty zapisu z nazwą (do 10 znaków), datą i godziną zapisu.
    /// Pełny slot: „Graj” i „Usuń” (drugi klik potwierdza), pusty: „Nowa gra” z polem na nazwę. Na dole język i wyjście.
    /// Za menu widać wyspę w tle (nie jest zapisywana). Wybór slotu ładuje scenę od nowa już z tym zapisem.
    /// </summary>
    public class MainMenuView : MonoBehaviour
    {
        const float W = 204f;
        const float RowH = 28f, RowGap = 3f;
        const float TopBand = 18f, BottomBand = 24f;
        const float LogoGap = 6f;   // logo: margines nad i pod (pasek tytułu rośnie o wysokość logo)

        RectTransform root, panel;
        int editing = -1;
        int armedDelete = -1;
        float armedUntil;
        string draftName;
        List<SlotInfo> slots;

        public static MainMenuView Create(RectTransform canvasRoot)
        {
            // przyciemnienie na cały ekran: łapie kliknięcia, więc wyspa w tle nie reaguje
            var dim = Ui.Img(canvasRoot, "MainMenu", null, false, true);
            dim.color = new Color(0f, 0f, 0f, 0.35f);
            Ui.Stretch(dim.rectTransform);
            var view = dim.gameObject.AddComponent<MainMenuView>();
            view.root = dim.rectTransform;
            view.slots = SaveSystem.ListSlots();
            view.Build();
            return view;
        }

        void OnEnable() => Loc.Changed += Build;
        void OnDisable() => Loc.Changed -= Build;

        void Update()
        {
            // potwierdzenie usunięcia wygasa po 4 s
            if (armedDelete >= 0 && Time.unscaledTime > armedUntil)
            {
                armedDelete = -1;
                Build();
            }
        }

        void Build()
        {
            if (root == null)
                return;
            if (panel != null)
                Destroy(panel.gameObject);
            var t = Ui.Theme;
            // logo gry (ten sam napis we wszystkich językach, GDD §16.5) albo tytuł tekstem, gdy logo nie ma
            var logoSize = t.logo != null ? t.logo.rect.size : Vector2.zero;
            float top = t.logo != null ? logoSize.y + 2f * LogoGap : TopBand;
            float h = top + SaveSystem.Slots * (RowH + RowGap) + 6f + BottomBand;
            var p = Ui.Img(root, "Panel", t.panel, true, true);
            panel = Ui.Place(p.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(W, h));

            if (t.logo != null)
            {
                var logo = Ui.Img(panel, "Logo", t.logo, false);
                Ui.Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -LogoGap), logoSize);
            }
            else
            {
                var title = Ui.Txt(panel, "Title", t.textGold, TextAlignmentOptions.Top);
                title.text = Loc.Get("game.title");
                Ui.Place(title.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -5), new Vector2(W, 10));
            }

            var page = Ui.Img(panel, "Page", t.panelPage != null ? t.panelPage : t.tooltip, true);
            float pageH = SaveSystem.Slots * (RowH + RowGap) + 3f;
            Ui.Place(page.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(6, -top), new Vector2(W - 12, pageH));
            for (int i = 0; i < SaveSystem.Slots; i++)
                BuildSlot(page.rectTransform, slots[i], 3f + i * (RowH + RowGap));

            // dół: język (kolejny po kliknięciu) i wyjście z gry
            float y = -(h - BottomBand + 4f);
            TextButton(panel, "Lang", Loc.Get("lang." + Loc.Language), new Vector2(8, y), () =>
            {
                var langs = Loc.Languages;
                int idx = 0;
                for (int k = 0; k < langs.Count; k++)
                    if (langs[k] == Loc.Language) idx = k;
                Loc.SetLanguage(langs[(idx + 1) % langs.Count]);   // Loc.Changed przebuduje menu
            });
            var quit = TextButton(panel, "Quit", Loc.Get("act.quit"), Vector2.zero, UiRoot.QuitGame);
            var qrt = (RectTransform)quit.transform;
            Ui.Place(qrt, Ui.TopRight, Ui.TopRight, new Vector2(-8, y), qrt.sizeDelta);
            // numer wersji (Player Settings, Wyspa → Wydanie) między przyciskami; sam numer, bez tekstu do tłumaczenia
            var ver = Ui.Txt(panel, "Version", t.textMuted, TextAlignmentOptions.Top);
            ver.text = "v" + Application.version;
            Ui.Place(ver.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, y - 5f), new Vector2(W, 10));
        }

        void BuildSlot(RectTransform page, SlotInfo info, float top)
        {
            var t = Ui.Theme;
            int i = info.index;
            var row = Ui.Img(page, "Slot" + (i + 1), t.slot, true);
            var rt = Ui.Place(row.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(3, -top), new Vector2(W - 18, RowH));

            if (editing == i)
            {
                BuildNameInput(rt, i);
                return;
            }

            // slot to ciemna ramka: jasny tekst
            var name = Ui.Txt(rt, "Name", info.exists ? t.textLight : t.textMuted);
            name.text = info.exists ? (string.IsNullOrEmpty(info.slotName) ? Loc.Get("ui.menu.default_name", i + 1) : info.slotName)
                                    : Loc.Get("ui.menu.empty");
            Ui.Place(name.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(5, -4), new Vector2(110, 10));
            if (info.exists)
            {
                var when = Ui.Txt(rt, "When", t.textMuted);
                // data i godzina zapisu w formacie języka gry (np. 14.10.2026 13:45)
                when.text = info.savedAtUtcTicks > 0
                    ? Loc.Get("ui.menu.slot_info", info.SavedLocal.ToString("g", LocCore.Culture(Loc.Language)))
                    : Loc.Get("ui.menu.unreadable");   // plik jest, ale się nie czyta (kopia trafi do *.corrupt.json)
                Ui.Place(when.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(5, -15), new Vector2(120, 10));
            }

            float x = -4f;
            if (info.exists)
            {
                bool armed = armedDelete == i;
                var del = TextButton(rt, "Delete", Loc.Get(armed ? "ui.menu.delete_confirm" : "ui.menu.delete"), Vector2.zero, () =>
                {
                    if (armedDelete == i)
                    {
                        SaveSystem.DeleteSlot(i);
                        armedDelete = -1;
                        slots = SaveSystem.ListSlots();
                    }
                    else
                    {
                        armedDelete = i;
                        armedUntil = Time.unscaledTime + 4f;
                    }
                    Build();
                });
                x = PlaceRight(del, x);
                var play = TextButton(rt, "Play", Loc.Get("ui.menu.play"), Vector2.zero, () => SaveSystem.Play(i));
                PlaceRight(play, x);
            }
            else
            {
                var create = TextButton(rt, "New", Loc.Get("ui.menu.new"), Vector2.zero, () =>
                {
                    editing = i;
                    armedDelete = -1;   // wygasające „Na pewno?” nie przebuduje menu w trakcie pisania
                    draftName = Loc.Get("ui.menu.default_name", i + 1);
                    Build();
                });
                PlaceRight(create, x);
            }
        }

        /// <summary>Nowa gra: pole na nazwę (do 10 znaków), Enter albo „Start” zaczyna, „X” wraca.</summary>
        void BuildNameInput(RectTransform row, int slot)
        {
            var t = Ui.Theme;
            var cancel = TextButton(row, "Cancel", "X", Vector2.zero, () => { editing = -1; Build(); });
            float x = PlaceRight(cancel, -4f);
            TMP_InputField field = null;
            var start = TextButton(row, "Start", Loc.Get("ui.menu.start"), Vector2.zero, () => SaveSystem.Play(slot, field != null ? field.text : draftName));
            x = PlaceRight(start, x);

            var label = Ui.Txt(row, "Label", t.textLight);
            label.text = Loc.Get("ui.menu.name");
            Ui.Place(label.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(5, -2), new Vector2(100, 10));

            var bg = Ui.Img(row, "NameField", t.tooltip, true, true);
            float fieldW = row.sizeDelta.x + x - 10f;
            Ui.Place(bg.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(5, -12), new Vector2(Mathf.Max(40f, fieldW), 13));
            var area = Ui.Stretch(Ui.Rect("Area", bg.transform), 3f);
            area.gameObject.AddComponent<RectMask2D>();
            var text = Ui.Txt(area, "Text", t.textLight, TextAlignmentOptions.Left);
            Ui.Stretch(text.rectTransform);
            var placeholder = Ui.Txt(area, "Placeholder", t.textMuted, TextAlignmentOptions.Left);
            placeholder.text = Loc.Get("ui.menu.default_name", slot + 1);
            Ui.Stretch(placeholder.rectTransform);

            // TMP_InputField buduje karetkę i zaznaczenie w OnEnable, gdy zna już pole tekstowe: konfiguracja przy wyłączonym obiekcie
            bg.gameObject.SetActive(false);
            field = bg.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = SaveSystem.MaxNameLength;
            field.customCaretColor = true;
            field.caretColor = t.textGold;
            field.caretWidth = 1;
            field.selectionColor = new Color(t.textWisp.r, t.textWisp.g, t.textWisp.b, 0.4f);
            field.text = draftName ?? "";
            field.onValueChanged.AddListener(v => draftName = v);
            field.onSubmit.AddListener(v => SaveSystem.Play(slot, v));
            bg.gameObject.SetActive(true);
            field.ActivateInputField();
        }

        static Button TextButton(RectTransform parent, string id, string label, Vector2 pos, System.Action onClick)
        {
            var b = Ui.Btn(parent, id, onClick);
            var txt = Ui.Txt(b.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
            txt.text = label;
            float w = Mathf.Max(16f, Ui.Width(txt) + 10f);
            Ui.Stretch(txt.rectTransform);
            Ui.Place((RectTransform)b.transform, Ui.TopLeft, Ui.TopLeft, pos, new Vector2(w, 16));
            return b;
        }

        /// <summary>Przycisk przy prawej krawędzi wiersza, na lewo od poprzedniego (x = przesunięcie od prawej). Zwraca nowe x.</summary>
        static float PlaceRight(Button b, float x)
        {
            var rt = (RectTransform)b.transform;
            Ui.Place(rt, Ui.TopRight, Ui.TopRight, new Vector2(x, -6), rt.sizeDelta);
            return x - rt.sizeDelta.x - 3f;
        }
    }
}
