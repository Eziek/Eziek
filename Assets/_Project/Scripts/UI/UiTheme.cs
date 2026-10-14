using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wyspa.UI
{
    [Serializable]
    public class NamedSprite
    {
        public string name;
        public Sprite sprite;
    }

    /// <summary>Wygląd UI (GDD §12): elementy 9-slice, ikony, font, tabele lokalizacji, kolory z palety.</summary>
    [CreateAssetMenu(menuName = "Wyspa/Motyw UI", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject
    {
        public Font font;
        public TextAsset[] tables;

        [Header("9-slice")]
        public Sprite panel;
        public Sprite panelPage;
        public Sprite buttonNormal, buttonHover, buttonPressed, buttonDisabled;
        public Sprite tabActive, tabInactive;
        public Sprite slot, slotSelected, slotLocked;
        public Sprite tooltip, toast, barFrame;
        public Sprite barFillEssence, barFillGrowth, barFillRhythm;
        public Sprite wheel;

        [Header("Ikony (nazwa bez przedrostka icon_)")]
        public List<NamedSprite> icons = new List<NamedSprite>();

        [Header("Kolory (paleta GDD §5.3)")]
        public Color textLight = new Color32(0xd8, 0xcc, 0xab, 0xff);
        public Color textMuted = new Color32(0x84, 0x8e, 0x9b, 0xff);
        public Color textGold = new Color32(0xff, 0xe0, 0x8a, 0xff);
        public Color textWisp = new Color32(0x5f, 0xcf, 0xb8, 0xff);
        public Color textDark = new Color32(0x1c, 0x15, 0x20, 0xff);
        public Color textWarn = new Color32(0xa2, 0x4d, 0x48, 0xff);

        Dictionary<string, Sprite> lookup;

        public Sprite Icon(string name)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, Sprite>();
                foreach (var n in icons)
                    if (n != null && !string.IsNullOrEmpty(n.name))
                        lookup[n.name] = n.sprite;
            }
            return name != null && lookup.TryGetValue(name, out var s) ? s : null;
        }
    }
}
