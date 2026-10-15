using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wyspa.UI
{
    /// <summary>
    /// Dymek przy kursorze. Właściciel (owner) pokazuje i chowa swój dymek. Najwyżej 220 px szerokości (dłuższy tekst się
    /// zawija), przy krawędzi okna przeskakuje na drugą stronę kursora zamiast wjeżdżać pod niego.
    /// </summary>
    public class TooltipView : MonoBehaviour
    {
        RectTransform canvasRect;
        RectTransform panel;
        TextMeshProUGUI text;
        object owner;
        string shown;
        const float MaxWidth = 220f;

        /// <summary>Kto teraz pokazuje dymek (null = schowany).</summary>
        public object Owner => gameObject.activeSelf ? owner : null;

        public static TooltipView Create(RectTransform canvasRoot)
        {
            var img = Ui.Img(canvasRoot, "Tooltip", Ui.Theme.tooltip, true);
            Ui.Place(img.rectTransform, Ui.BottomLeft, Ui.TopLeft, Vector2.zero, new Vector2(40, 16));
            var view = img.gameObject.AddComponent<TooltipView>();
            view.canvasRect = canvasRoot;
            view.panel = img.rectTransform;
            view.text = Ui.Txt(img.transform, "Text", Ui.Theme.textLight);
            Ui.Place(view.text.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(4, -3), new Vector2(200, 10));
            img.gameObject.SetActive(false);
            return view;
        }

        public void Show(object who, string content)
        {
            // wołane co klatkę przez grządki i tryby: bez zmian nic nie przeliczamy (pomiar tekstu przebudowuje płótno)
            bool same = gameObject.activeSelf && who == owner && content == shown;
            owner = who;
            if (!same)
            {
                shown = content;
                text.text = content;
                float maxW = Mathf.Max(80f, Mathf.Min(MaxWidth, canvasRect.rect.width - 16f));
                var size = text.GetPreferredValues(content);
                text.textWrappingMode = size.x > maxW ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                if (size.x > maxW)
                    size = new Vector2(maxW, text.GetPreferredValues(content, maxW, 0f).y);
                text.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(size.x), Mathf.Ceil(size.y));
                panel.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 8, Mathf.Ceil(size.y) + 6);
            }
            var parent = panel.parent;
            if (parent != null && panel.GetSiblingIndex() != parent.childCount - 1)
                panel.SetAsLastSibling();
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            Follow();
        }

        public void Hide(object who)
        {
            if (who == owner)
            {
                owner = null;
                shown = null;
                gameObject.SetActive(false);
            }
        }

        void LateUpdate() => Follow();

        void Follow()
        {
            if (!gameObject.activeSelf)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, UiInput.MousePosition, null, out var local);
            // lokalne współrzędne liczone od środka płótna -> przesuwamy do lewego dolnego rogu
            var size = canvasRect.rect.size;
            var w = panel.sizeDelta;
            var cursor = local + size * 0.5f;
            var p = cursor + new Vector2(8, -6);                        // domyślnie w prawo i w dół od kursora
            if (p.x + w.x > size.x) p.x = cursor.x - 8 - w.x;           // brak miejsca z prawej: na lewo od kursora
            if (p.y - w.y < 0) p.y = cursor.y + 6 + w.y;                // brak miejsca na dole: nad kursorem
            p.x = Mathf.Clamp(p.x, 0, Mathf.Max(0, size.x - w.x));
            p.y = Mathf.Clamp(p.y, Mathf.Min(w.y, size.y), size.y);
            panel.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
        }
    }
}
