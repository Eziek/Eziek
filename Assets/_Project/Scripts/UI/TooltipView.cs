using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wyspa.UI
{
    /// <summary>Dymek przy kursorze. Właściciel (owner) pokazuje i chowa swój dymek.</summary>
    public class TooltipView : MonoBehaviour
    {
        RectTransform canvasRect;
        RectTransform panel;
        TextMeshProUGUI text;
        object owner;

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
            owner = who;
            text.text = content;
            var size = text.GetPreferredValues(content);
            text.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(size.x), Mathf.Ceil(size.y));
            panel.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 8, Mathf.Ceil(size.y) + 6);
            panel.SetAsLastSibling();
            gameObject.SetActive(true);
            Follow();
        }

        public void Hide(object who)
        {
            if (who == owner)
            {
                owner = null;
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
            var p = local + size * 0.5f + new Vector2(8, -6);
            p.x = Mathf.Clamp(p.x, 0, size.x - panel.sizeDelta.x);
            p.y = Mathf.Clamp(p.y, panel.sizeDelta.y, size.y);
            panel.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
        }
    }
}
