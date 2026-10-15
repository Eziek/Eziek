using System.Collections.Generic;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Localization;

namespace Wyspa.Familiars
{
    /// <summary>
    /// Domek chowańca (GDD §9 p. 103): kocie posłanie, grzęda kruka, oczko wodne, budka nietoperzy, latarenka ćmy,
    /// dziupla sowy, mszysty kamień, buda lisa-widma. Chowaniec kręci się wokół niego, wraca do niego i w nim śpi,
    /// a bonus chowańca liczy się jak przy więzi o poziom wyższej.
    /// </summary>
    public class FamiliarHome : MonoBehaviour, IContextActions
    {
        static readonly Dictionary<string, FamiliarHome> homes = new Dictionary<string, FamiliarHome>();

        string familiar;
        SpriteRenderer body;

        public string Familiar => familiar;

        public static FamiliarHome Of(string kind) => kind != null && homes.TryGetValue(kind, out var h) && h != null ? h : null;

        public void Setup(string familiarKind)
        {
            familiar = familiarKind;
            body = GetComponent<SpriteRenderer>();
            if (isActiveAndEnabled)
                homes[familiar] = this;
        }

        void OnEnable()
        {
            if (familiar != null)
                homes[familiar] = this;
        }

        void OnDisable()
        {
            if (familiar != null && homes.TryGetValue(familiar, out var h) && h == this)
                homes.Remove(familiar);
        }

        /// <summary>Gdzie chowaniec siedzi albo śpi w domku (piksele od podstawy budowli) i czy jest wtedy przed nią.</summary>
        public Vector3 Spot
        {
            get
            {
                Vector2 px;
                switch (familiar)
                {
                    case FamiliarSystem.Cat: px = new Vector2(0, 3); break;      // w koszu
                    case FamiliarSystem.Raven: px = new Vector2(0, 19); break;   // na poprzeczce
                    case FamiliarSystem.Toad: px = new Vector2(4, 4); break;     // na liściu grzybieni
                    case FamiliarSystem.Bat: px = new Vector2(5, 3); break;      // pod skrzynką
                    case FamiliarSystem.Moth: px = new Vector2(4, 13); break;    // przy latarence
                    case FamiliarSystem.Owl: px = new Vector2(0, 6); break;      // w dziupli
                    case FamiliarSystem.Snail: px = new Vector2(-2, 8); break;   // na kamieniu
                    default: px = new Vector2(0, -2); break;                    // lis: przed budą
                }
                return transform.position + new Vector3(px.x / 16f, px.y / 16f, 0f);
            }
        }

        /// <summary>Kolejność rysowania chowańca w domku: przed budowlą.</summary>
        public int FrontOrder => body != null ? body.sortingOrder + 2 : 0;

        /// <summary>Losowy punkt na ziemi wokół domku (spacery w pobliżu).</summary>
        public Vector3 Around() => transform.position + new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(-1.2f, 0.4f), 0f);

        public void FillContext(ContextInfo info)
        {
            info.lines.Add(Loc.Get("ui.home.info", Loc.Get("familiar." + familiar)));
            info.lines.Add(FamiliarSystem.BonusText(familiar));
        }
    }
}
