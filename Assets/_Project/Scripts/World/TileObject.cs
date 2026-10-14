using UnityEngine;

namespace Wyspa.World
{
    /// <summary>
    /// Obiekt stojący na kafelku (lampa, skrzynia, kamień runiczny). Island stawia go na wolnym kafelku, gdy jest potrzebny
    /// (do Etapu 3c samoczynnie; potem gracz w trybie stawiania). Obiekt z kotwicą (np. ognik przy skrzyni) nie zajmuje kafelka.
    /// </summary>
    public class TileObject : MonoBehaviour
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
    }
}
