using UnityEngine;
using Wyspa.Core;
using Wyspa.Localization;
using Wyspa.Visual;

namespace Wyspa.UI
{
    /// <summary>
    /// Tablica ogłoszeń na nowej wyspie (GDD §12.8). Po wykonanym kroku karteczka się odrywa (cząsteczki),
    /// a po chwili przybija się następna. Klik: panel z treścią karteczki i „Rozbierz” (koniec wprowadzenia, drewno).
    /// </summary>
    public class NoticeBoard : MonoBehaviour, IContextActions
    {
        SpriteRenderer body;
        Sprite withNote, empty;
        float repinAt = -1f;

        public void Setup(SpriteRenderer renderer, Sprite noteSprite, Sprite emptySprite)
        {
            body = renderer;
            withNote = noteSprite;
            empty = emptySprite != null ? emptySprite : noteSprite;
        }

        /// <summary>Krok wykonany: karteczka odpada, nowa pojawia się po chwili (ostatnia zostaje do rozbiórki).</summary>
        public void TearNote()
        {
            if (body == null)
                return;
            body.sprite = empty;
            repinAt = Time.unscaledTime + 0.8f;
            IslandFx.FindPopped(transform.position + new Vector3(-0.1f, 0.75f, 0f));
        }

        float refreshTimer;

        void Update()
        {
            if (body == null)
                return;
            if (repinAt >= 0f)
            {
                if (Time.unscaledTime < repinAt)
                    return;
                repinAt = -1f;
                Refresh();
                return;
            }
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 1f;
                Refresh();
            }
        }

        /// <summary>Karteczka wisi, gdy trwa wprowadzenie albo czeka jakieś zamówienie.</summary>
        public void Refresh()
        {
            if (body == null || repinAt >= 0f)
                return;
            var intro = IntroNotes.Instance;
            var orders = Orders.OrderBook.Instance;
            bool note = (intro != null && intro.Active) || (orders != null && orders.Orders.Count > 0);
            body.sprite = note ? withNote : empty;
        }

        public void FillContext(ContextInfo info)
        {
            var intro = IntroNotes.Instance;
            if (intro != null && intro.Active && !intro.Finished)
            {
                info.lines.Add(Loc.Get(intro.CurrentKey));
                info.lines.Add(Loc.Get("ui.board.skip"));
                return;
            }
            // po wprowadzeniu: zamówienia od mieszkańców mgły
            var orders = Orders.OrderBook.Instance;
            var game = GameLoop.Instance;
            if (orders != null && game != null)
                orders.Describe(info.lines, game.Clock.CycleNumber);
        }
    }
}
