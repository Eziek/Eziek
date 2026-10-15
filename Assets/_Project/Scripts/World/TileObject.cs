using UnityEngine;
using Wyspa.Core;
using Wyspa.Input;
using Wyspa.Localization;

namespace Wyspa.World
{
    /// <summary>
    /// Obiekt stojący na kafelku (lampa, skrzynia, kamień runiczny). Island stawia go na wolnym kafelku, gdy jest potrzebny
    /// . Obiekt z kotwicą (np. ognik przy skrzyni) nie zajmuje kafelka.
    /// </summary>
    public class TileObject : MonoBehaviour, IClickable, IContextActions
    {
        [SerializeField] string id;
        [Tooltip("Klucz lokalizacji nazwy (do komunikatu o braku miejsca).")]
        [SerializeField] string nameKey;
        [Tooltip("Stoi obok innego obiektu zamiast na własnym kafelku.")]
        [SerializeField] TileObject anchor;
        [Tooltip("Przesunięcie od kotwicy w pikselach (x w prawo, y w górę).")]
        [SerializeField] Vector2Int anchorOffset;
        [Tooltip("Gdzie w kafelku stoi podstawa (piksele od górnej krawędzi), jak w prototypie (13).")]
        [SerializeField] int baseRow = 13;

        public string Id => id;
        public string NameKey => nameKey;
        public TileObject Anchor => anchor;
        public Vector2Int AnchorOffset => anchorOffset;
        public int BaseRow => baseRow;
        public bool NeedsTile => anchor == null;

        /// <summary>Lampa kupiona w sklepie: id definicji (do odtworzenia z zapisu). Puste dla obiektów ze sceny.</summary>
        public string LampId { get; set; }
        /// <summary>Budowla kupiona w sklepie (np. ołtarz) - do odtworzenia z zapisu.</summary>
        public string BuildingId { get; set; }
        /// <summary>Stoi na stałe (budowla fabularna, §13.11): bez „Przenieś” i „Rozbierz”.</summary>
        public bool Fixed { get; set; }
        public bool Wanted { get; set; }
        public bool Placed { get; set; }
        public TilePos Tile { get; set; }

        public void Setup(string objectId, string nameLocKey, TileObject anchorObject = null, Vector2Int offset = default, int row = 13)
        {
            id = objectId;
            nameKey = nameLocKey;
            anchor = anchorObject;
            anchorOffset = offset;
            baseRow = row;
        }

        public void OnClick()
        {
            // np. drzewo z owocami: klik od razu zbiera, panel dopiero przy kolejnym kliknięciu
            var shortcut = GetComponent<Farming.IClickShortcut>();
            if (shortcut != null && shortcut.TryClick())
                return;
            GameEvents.RequestContext(this);
        }

        /// <summary>Panel kontekstowy: nazwa + to, co dopiszą inne komponenty (skrzynia: sprzedaż, lampa: zasięg). „Przenieś” dodaje UI.</summary>
        public void FillContext(ContextInfo info)
        {
            info.title = string.IsNullOrEmpty(nameKey) ? name : Loc.Get(nameKey);
            info.worldAnchor = transform.position + new Vector3(0f, 1.25f, 0f);
            foreach (var c in GetComponents<IContextActions>())
                if (!ReferenceEquals(c, this))
                    c.FillContext(info);
        }
    }
}
