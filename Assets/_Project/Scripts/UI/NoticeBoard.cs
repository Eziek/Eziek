using UnityEngine;
using Wyspa.Core;
using Wyspa.Localization;
using Wyspa.Visual;

namespace Wyspa.UI
{
    /// <summary>
    /// Tablica ogłoszeń na nowej wyspie (Etap 6d, GDD §12.8). Po wykonanym kroku karteczka się odrywa (cząsteczki),
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

        void Update()
        {
            if (repinAt < 0f || Time.unscaledTime < repinAt || body == null)
                return;
            repinAt = -1f;
            var intro = IntroNotes.Instance;
            body.sprite = intro != null && intro.Active ? withNote : empty;
        }

        public void FillContext(ContextInfo info)
        {
            var intro = IntroNotes.Instance;
            if (intro != null && intro.Active)
            {
                info.lines.Add(Loc.Get(intro.CurrentKey));
                info.lines.Add(Loc.Get(intro.Finished ? "ui.board.done" : "ui.board.skip"));
            }
            else
                info.lines.Add(Loc.Get("ui.board.done"));
        }
    }
}
