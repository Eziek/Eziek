using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wyspa.Core;
using Wyspa.Economy;
using Wyspa.Localization;
using Wyspa.Save;

namespace Wyspa.UI
{
    /// <summary>
    /// Przystań we mgle: ekran między rejsami (po „Odpłyń we mgłę”, przed nową wyspą). Wyspa w tle jak w menu głównym.
    /// Gracz wydaje Perły Mgły na losowania darów: jedna perła = jedno losowanie. Ikony wszystkich darów, które mogą jeszcze
    /// wypaść, zlatują się na środek i walczą (odpadają po kolei), zostaje jeden: wylosowany dar rośnie w rozbłysku koloru
    /// rzadkości, potem wszystkie wracają na miejsca w kolekcji. Każde losowanie od razu trafia do zapisu.
    /// „Wypłyń na nową wyspę” zaczyna grę z darami (SaveSystem.LeaveMist).
    /// </summary>
    public class MistHarborView : MonoBehaviour
    {
        const float W = 204f;
        const float Pad = 6f;
        const float BrawlSeconds = 3.2f;

        RectTransform root, panel, stage;
        TooltipView tooltip;
        GiftGrid grid;
        TextMeshProUGUI pearlsLine, resultLine;
        Button rollButton, sailButton, menuButton;
        TextMeshProUGUI rollText;
        bool busy;
        float speed = 1f;   // klik w trakcie walki przyspiesza
        static Sprite burst;

        public static MistHarborView Create(RectTransform canvasRoot, TooltipView tooltip)
        {
            var dim = Ui.Img(canvasRoot, "MistHarbor", null, false, true);
            dim.color = new Color(0.07f, 0.1f, 0.14f, 0.55f);   // mgła nad wyspą w tle
            Ui.Stretch(dim.rectTransform);
            var view = dim.gameObject.AddComponent<MistHarborView>();
            view.root = dim.rectTransform;
            view.tooltip = tooltip;
            return view;
        }

        /// <summary>Po wszystkich Awake (GameLoop czyści perły w Awake): perły i dary z pliku slotu, potem ekran.</summary>
        void Start()
        {
            if (!SaveSystem.LoadMist())
            {
                SaveSystem.MistToMenu();   // slot już nie jest we mgle (np. usunięty): wracamy do menu
                return;
            }
            var summary = SaveSystem.PendingSummary();
            if (summary != null)
                StartCoroutine(ShowSummary(summary));   // prosto po odpłynięciu: najpierw podsumowanie rejsu
            else
                Build();
        }

        // ------------------------------------------------------------ podsumowanie rejsu

        /// <summary>
        /// Pergamin z liczbami skończonego rejsu, wypisywanymi wiersz po wierszu (perły na końcu, złotem).
        /// Mgła po odpłynięciu rozstępuje się nad nim; „Do przystani” otwiera losowanie darów. Pokazywane raz.
        /// </summary>
        IEnumerator ShowSummary(VoyageSummary s)
        {
            busy = true;
            var t = Ui.Theme;
            var curtain = MistCurtain.Create(root, 1f);
            var culture = LocCore.Culture(Loc.Language);
            int days = Mathf.FloorToInt(s.days);
            var lines = new List<(string text, bool gold)>
            {
                (days < 1 ? Loc.Get("ui.summary.days_less") : Loc.Get("ui.summary.days", days), false),
                (Loc.Get("ui.summary.coins", s.coins.ToString("N0", culture)), false),
                (Loc.Get("ui.summary.harvests", s.harvests), false),
                (Loc.Get("ui.summary.sold", s.sold), false),
                (Loc.Get("ui.summary.tiles", s.tiles), false),
                (Loc.Get("ui.summary.wisps", s.wisps), false),
                (Loc.Get("ui.summary.pearls", s.pearls), true),
            };
            // szerokość z najdłuższego wiersza (języki różnie długie); ramki panelu i pergaminu mają po 7 px
            const float pad = 9f, inner = 10f, lineH = 11f;
            var title = Ui.Txt(root, "Title", t.textGold, TextAlignmentOptions.Top);
            title.text = Loc.Get("ui.summary.title", s.voyage);
            float textW = Ui.Width(title);
            var texts = new List<TextMeshProUGUI>();
            for (int i = 0; i < lines.Count; i++)
            {
                var tx = Ui.Txt(root, "Line", lines[i].gold ? t.textGoldOnPage : t.textDark);
                tx.text = lines[i].text;
                textW = Mathf.Max(textW, Ui.Width(tx));   // pomiar na aktywnym obiekcie
                texts.Add(tx);
            }
            float w = Mathf.Clamp(textW + 2 * (pad + inner), 180f, Mathf.Max(180f, root.rect.width - 8f));
            float pageH = lines.Count * lineH + 12f;
            float h = 8 + 10 + 6 + pageH + 6 + 16 + 8;
            var p = Ui.Img(root, "Summary", t.panel, true, true);
            var box = Ui.Place(p.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(w, h));
            title.rectTransform.SetParent(box, false);
            Ui.Place(title.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -8), new Vector2(w, 10));
            var page = Ui.Img(box, "Page", t.panelPage != null ? t.panelPage : t.tooltip, true);
            var pageRt = Ui.Place(page.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(pad, -24), new Vector2(w - 2 * pad, pageH));
            for (int i = 0; i < texts.Count; i++)
            {
                texts[i].rectTransform.SetParent(pageRt, false);
                Ui.Place(texts[i].rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(inner, -6 - i * lineH), new Vector2(w - 2 * (pad + inner), 10));
                texts[i].gameObject.SetActive(false);
            }
            bool next = false;
            var go = Ui.Btn(box, "Next", () => next = true);
            Ui.Place((RectTransform)go.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(Mathf.Min(120f, w - 2 * pad), 16));
            var label = Ui.Txt(go.transform, "Text", t.textLight, TextAlignmentOptions.Center);
            label.text = Loc.Get("ui.summary.next");
            Ui.Stretch(label.rectTransform);
            go.gameObject.SetActive(false);
            curtain.transform.SetAsLastSibling();
            yield return curtain.Open(1.6f);
            Destroy(curtain.gameObject);
            for (int i = 0; i < texts.Count; i++)
            {
                yield return new WaitForSecondsRealtime(i == texts.Count - 1 ? 0.6f : 0.3f);
                texts[i].gameObject.SetActive(true);
                GameEvents.PlaySound(lines[i].gold ? "coin" : "click", lines[i].gold ? 0.8f : 0.4f);
            }
            go.gameObject.SetActive(true);
            while (!next)
                yield return null;
            SaveSystem.MarkSummaryShown();
            Destroy(box.gameObject);
            busy = false;
            Build();
        }

        void OnEnable() => Loc.Changed += Rebuild;
        void OnDisable() => Loc.Changed -= Rebuild;

        void Rebuild()
        {
            if (!busy && panel != null)
                Build();
        }

        void Build()
        {
            if (panel != null)
                Destroy(panel.gameObject);
            if (stage != null)
                Destroy(stage.gameObject);
            var t = Ui.Theme;
            float gridW = W - 2 * Pad - 6f;
            float gridH = GiftGrid.HeightFor(gridW);
            float pageH = gridH + 6f;
            float h = Pad + 10 + 2 + 10 + 4 + pageH + 4 + 10 + 4 + 16 + 3 + 16 + Pad;
            var p = Ui.Img(root, "Panel", t.panel, true, true);
            panel = Ui.Place(p.rectTransform, Ui.Center, Ui.Center, Vector2.zero, new Vector2(W, h));

            float y = Pad;
            var title = Ui.Txt(panel, "Title", t.textGold, TextAlignmentOptions.Top);
            LocText.On(title, "ui.harbor.title");
            Ui.Place(title.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W, 10));
            y += 12;
            pearlsLine = Ui.Txt(panel, "Pearls", t.textLight, TextAlignmentOptions.Top);
            Ui.Place(pearlsLine.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W, 10));
            y += 14;

            var page = Ui.Img(panel, "Page", t.panelPage != null ? t.panelPage : t.tooltip, true);
            Ui.Place(page.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(Pad, -y), new Vector2(W - 2 * Pad, pageH));
            grid = GiftGrid.Create(page.rectTransform, new Vector2(3, -3), gridW, tooltip);
            y += pageH + 4;

            resultLine = Ui.Txt(panel, "Result", t.textMuted, TextAlignmentOptions.Top);
            Ui.Place(resultLine.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0, -y), new Vector2(W, 10));
            resultLine.text = Loc.Get("ui.harbor.hint");
            y += 14;

            rollButton = Ui.Btn(panel, "Roll", Roll);
            Ui.Place((RectTransform)rollButton.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(Pad, -y), new Vector2(W - 2 * Pad, 16));
            rollText = Ui.Txt(rollButton.transform, "Text", t.textLight, TextAlignmentOptions.Center);
            Ui.Stretch(rollText.rectTransform);
            rollButton.gameObject.AddComponent<HoverTip>().Init(tooltip, () => Loc.Get("ui.harbor.roll_tip"));
            y += 19;

            menuButton = TextButton("Menu", Loc.Get("menu.main"), SaveSystem.MistToMenu);
            Ui.Place((RectTransform)menuButton.transform, Ui.TopLeft, Ui.TopLeft, new Vector2(Pad, -y), ((RectTransform)menuButton.transform).sizeDelta);
            sailButton = TextButton("Sail", Loc.Get("ui.harbor.sail"), Sail);
            Ui.Place((RectTransform)sailButton.transform, Ui.TopRight, Ui.TopRight, new Vector2(-Pad, -y), ((RectTransform)sailButton.transform).sizeDelta);

            // scena walki: na całe okno, nad panelem; klik w trakcie przyspiesza
            var st = Ui.Img(root, "Stage", null, false, true);
            st.color = new Color(0f, 0f, 0f, 0f);
            stage = Ui.Stretch(st.rectTransform);
            var skip = st.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(() => speed = 3f);
            stage.gameObject.SetActive(false);
            Refresh();
        }

        Button TextButton(string id, string label, System.Action onClick)
        {
            var b = Ui.Btn(panel, id, onClick);
            var txt = Ui.Txt(b.transform, "Text", Ui.Theme.textLight, TextAlignmentOptions.Center);
            txt.text = label;
            Ui.Stretch(txt.rectTransform);
            ((RectTransform)b.transform).sizeDelta = new Vector2(Mathf.Max(30f, Ui.Width(txt) + 10f), 16f);
            return b;
        }

        void Refresh()
        {
            int owned = 0;
            foreach (var g in Prestige.Gifts)
                if (Prestige.Owned(g)) owned++;
            pearlsLine.text = Loc.Get("ui.harbor.pearls", Prestige.Pearls, Prestige.Voyages + 1, owned, Prestige.Gifts.Length);
            rollButton.interactable = !busy && Prestige.CanRoll;
            rollText.text = Prestige.RollsLeft == 0 ? Loc.Get("ui.harbor.all_owned")
                          : Prestige.Pearls < Prestige.RollCost ? Loc.Get("ui.harbor.no_pearls")
                          : Loc.Get("ui.harbor.roll", Prestige.RollCost);
            sailButton.interactable = !busy;
            menuButton.interactable = !busy;
            if (!busy)
                grid.Refresh();
        }

        void Roll()
        {
            if (busy || !Prestige.CanRoll)
                return;
            var fighters = Prestige.Pool();   // przed losowaniem: dar na ostatnim poziomie też walczy
            var won = Prestige.Roll();
            if (won == null)
                return;
            SaveSystem.SaveMist();
            StartCoroutine(Fight(won, fighters));
        }

        void Sail()
        {
            if (busy)
                return;
            GameEvents.PlaySound("magic");
            SaveSystem.LeaveMist();
        }

        // ------------------------------------------------------------ walka darów

        class Fighter
        {
            public GiftGrid.Slot cell;
            public Vector2 home, pos, vel;
            public float angle, flash;
            public bool out_;
        }

        Vector2 StagePos(RectTransform rt) => stage.InverseTransformPoint(rt.TransformPoint(rt.rect.center));

        static float Smooth(float u) => u * u * (3f - 2f * u);

        float Dt => Time.unscaledDeltaTime * speed;

        IEnumerator Fight(Prestige.Gift winner, List<Prestige.Gift> gifts)
        {
            busy = true;
            speed = 1f;
            Refresh();
            stage.gameObject.SetActive(true);
            stage.SetAsLastSibling();
            resultLine.color = Ui.Theme.textMuted;
            resultLine.text = Loc.Get("ui.harbor.fight");

            // klony ikon w miejscach kolekcji; oryginały przygasają
            var fighters = new List<Fighter>();
            Fighter champ = null;
            foreach (var slot in grid.Slots)
            {
                if (!gifts.Contains(slot.gift))
                {
                    GiftGrid.Paint(slot, 0.35f);
                    continue;
                }
                var c = GiftGrid.BuildCell(stage, slot.gift, Vector2.zero);
                c.rect.anchorMin = c.rect.anchorMax = c.rect.pivot = Ui.Center;
                var f = new Fighter { cell = c, home = StagePos(slot.rect) };
                f.pos = f.home;
                c.rect.anchoredPosition = f.pos;
                Bright(c, 1f);
                GiftGrid.Paint(slot, 0.25f);
                fighters.Add(f);
                if (slot.gift == winner)
                    champ = f;
            }
            if (champ == null)
            {
                Finish(fighters, winner);
                yield break;
            }
            var center = StagePos(grid.Root);
            float r0 = Mathf.Min(90f, stage.rect.width * 0.42f);   // krąg szeroki jak okno (elipsa 0.7 w pionie)
            int n = fighters.Count;

            // 1. zlot na krąg wokół środka
            for (int i = 0; i < n; i++)
                fighters[i].angle = i * Mathf.PI * 2f / n + Random.Range(-0.2f, 0.2f);
            var start = new List<Vector2>();
            foreach (var f in fighters)
                start.Add(f.pos);
            for (float t = 0f; t < 0.7f; t += Dt)
            {
                for (int i = 0; i < n; i++)
                {
                    var f = fighters[i];
                    float u = Smooth(Mathf.Clamp01((t - (i % 6) * 0.03f) / 0.5f));
                    f.pos = Vector2.Lerp(start[i], center + Orbit(f.angle, r0), u);
                }
                Place(fighters);
                yield return null;
            }

            // 2. walka: krąg kręci się coraz szybciej i zacieśnia, co chwilę ktoś wylatuje na bok
            var alive = new List<Fighter>(fighters);
            var order = new List<Fighter>(fighters);
            order.Remove(champ);
            Shuffle(order);
            float spin = 0f, clock = 0f;
            float weightSum = 0f;
            for (int k = 0; k < order.Count; k++)
                weightSum += Weight(k, order.Count);
            for (int k = 0; k < order.Count; k++)
            {
                float dur = Mathf.Max(0.05f, BrawlSeconds * Weight(k, order.Count) / weightSum);
                for (float t = 0f; t < dur; t += Dt)
                {
                    float crowd = alive.Count / (float)n;
                    spin += Dt * (1.4f + 3f * (1f - crowd));
                    clock += Dt;
                    float r = Mathf.Lerp(10f, r0, crowd);
                    for (int i = 0; i < alive.Count; i++)
                    {
                        var f = alive[i];
                        // szarpnięcia: każdy co jakiś czas rzuca się do środka
                        float lunge = Mathf.Max(0f, Mathf.Sin(clock * 9f + f.angle * 5f)) * 0.35f;
                        f.pos = center + Orbit(f.angle + spin, r * (1f - lunge))
                              + new Vector2(Mathf.Sin(clock * 13f + i), Mathf.Cos(clock * 11f + i)) * 1.5f;
                    }
                    Fly(fighters, center);
                    Place(fighters);
                    yield return null;
                }
                var loser = order[k];
                alive.Remove(loser);
                var away = loser.pos - center;
                if (away.sqrMagnitude < 1f)
                    away = Random.insideUnitCircle;
                loser.vel = away.normalized * Random.Range(170f, 240f);
                loser.flash = 0.12f;
                loser.out_ = true;
                GameEvents.PlaySound(alive.Count == 1 ? "thunder" : "chop", alive.Count == 1 ? 0.3f : 0.35f);   // ostatni cios: grzmot
            }

            // 3. zwycięzca na środku, dwa razy większy, w rozbłysku koloru rzadkości
            var from = champ.pos;
            for (float t = 0f; t < 0.3f; t += Dt)
            {
                champ.pos = Vector2.Lerp(from, center, Smooth(t / 0.3f));
                Fly(fighters, center);
                Place(fighters);
                yield return null;
            }
            champ.pos = center;
            champ.cell.rect.localScale = new Vector3(2f, 2f, 1f);
            champ.cell.rect.SetAsLastSibling();
            var glow = Ui.Img(stage, "Burst", Burst(), false);
            var rc = Prestige.RarityColor(winner.rarity);
            Ui.Place(glow.rectTransform, Ui.Center, Ui.Center, center, new Vector2(Burst().rect.width, Burst().rect.height) * 2f);
            glow.transform.SetSiblingIndex(champ.cell.rect.GetSiblingIndex());
            bool big = winner.rarity >= GiftRarity.Epic;
            GameEvents.PlaySound(big ? "fanfare_big" : "fanfare", 0.8f);   // fanfara wygranej, przy epickim i legendarnym z gongiem
            StartCoroutine(Fireworks(winner.rarity, center));
            int lvl = Prestige.Level(winner.id);
            resultLine.color = rc;
            resultLine.text = lvl <= 1
                ? Loc.Get("ui.harbor.won_new", Loc.Get(winner.NameKey), Loc.Get(Prestige.RarityKey(winner.rarity)))
                : Loc.Get("ui.harbor.won_level", Loc.Get(winner.NameKey), lvl, winner.MaxLevel);
            speed = 1f;
            for (float t = 0f; t < 1.6f && speed < 2f; t += Dt)
            {
                float pulse = 0.55f + 0.35f * Mathf.Sin(t * 8f);
                glow.color = new Color(rc.r, rc.g, rc.b, pulse);
                Bright(champ.cell, 1f, t < 0.1f);
                Fly(fighters, center);
                Place(fighters);
                yield return null;
            }
            Destroy(glow.gameObject);

            // 4. wszyscy wracają na swoje miejsca
            champ.cell.rect.localScale = Vector3.one;
            speed = 1f;
            var back = new List<Vector2>();
            foreach (var f in fighters)
                back.Add(f.pos);
            for (float t = 0f; t < 0.6f; t += Dt)
            {
                float u = Smooth(t / 0.6f);
                for (int i = 0; i < n; i++)
                    fighters[i].pos = Vector2.Lerp(back[i], fighters[i].home, u);
                Place(fighters);
                yield return null;
            }
            Finish(fighters, winner);
        }

        void Finish(List<Fighter> fighters, Prestige.Gift winner)
        {
            foreach (var f in fighters)
                if (f.cell.rect != null)
                    Destroy(f.cell.rect.gameObject);
            stage.gameObject.SetActive(false);
            busy = false;
            Refresh();
            foreach (var s in grid.Slots)
                if (s.gift == winner)
                    StartCoroutine(Blink(s));
        }

        /// <summary>Nowy dar w kolekcji mruga kilka razy obwódką.</summary>
        IEnumerator Blink(GiftGrid.Slot s)
        {
            for (int i = 0; i < 4 && s.rect != null; i++)
            {
                s.border.color = Color.white;
                yield return new WaitForSecondsRealtime(0.12f);
                if (s.rect == null || busy)
                    yield break;
                GiftGrid.Paint(s, 1f);
                yield return new WaitForSecondsRealtime(0.12f);
            }
        }

        // ------------------------------------------------------------ fajerwerki

        class Spark
        {
            public Image img;
            public Vector2 pos, vel;
            public float life, age;
            public Color color;
            public bool rocket;
        }

        /// <summary>Ile rakiet na wygraną: zwykły 2, niezwykły 3, rzadki 4, epicki 6, legendarny 9.</summary>
        static int Rockets(GiftRarity r) => r == GiftRarity.Legendary ? 9 : r == GiftRarity.Epic ? 6 : 2 + (int)r;

        /// <summary>
        /// Fajerwerki nad Przystanią: rakiety (2 px ze smugą) wylatują z dołu okna i pękają w górnej połowie na iskry
        /// w kolorze rzadkości, złota i bieli; iskry opadają i gasną. Osobna warstwa nad wszystkim, bez klikania,
        /// więc gra dalej także w trakcie powrotu ikon.
        /// </summary>
        IEnumerator Fireworks(GiftRarity rarity, Vector2 center)
        {
            var layer = Ui.Stretch(Ui.Rect("Fireworks", root));
            layer.SetAsLastSibling();
            var sparks = new List<Spark>();
            var rc = Prestige.RarityColor(rarity);
            var gold = (Color)Ui.Theme.textGold;
            var half = layer.rect.size * 0.5f;
            int rockets = Rockets(rarity);
            float[] launchAt = new float[rockets];
            for (int i = 0; i < rockets; i++)
                launchAt[i] = i * (rarity >= GiftRarity.Epic ? 0.28f : 0.4f) + Random.Range(0f, 0.12f);
            int launched = 0;
            for (float t = 0f; launched < rockets || sparks.Count > 0; t += Time.unscaledDeltaTime)
            {
                while (launched < rockets && t >= launchAt[launched])
                {
                    // rakieta: z dołu, w stronę górnej połowy, rozrzucona na boki od środka walki
                    float x = Mathf.Clamp(center.x + Random.Range(-half.x * 0.8f, half.x * 0.8f), -half.x + 10f, half.x - 10f);
                    float apex = Random.Range(half.y * 0.15f, half.y * 0.75f);
                    var r = NewSpark(layer, sparks, new Vector2(x, -half.y), new Vector2(Random.Range(-15f, 15f), 0f), 2, gold);
                    r.rocket = true;
                    r.vel.y = Mathf.Sqrt(2f * 220f * (apex + half.y));   // dolatuje mniej więcej do apex przy grawitacji 220
                    r.life = r.vel.y / 220f;
                    launched++;
                }
                float dt = Time.unscaledDeltaTime;
                for (int i = sparks.Count - 1; i >= 0; i--)
                {
                    var p = sparks[i];
                    p.age += dt;
                    if (p.rocket)
                    {
                        p.vel.y -= 220f * dt;
                        p.pos += p.vel * dt;
                        if (p.age >= p.life || p.vel.y <= 0f)
                        {
                            Explode(layer, sparks, p.pos, rc, gold, rarity);
                            Kill(sparks, i);
                            continue;
                        }
                        // smuga: co klatkę krótka, gasnąca iskra za rakietą
                        var trail = NewSpark(layer, sparks, p.pos, Vector2.zero, 1, new Color(gold.r, gold.g, gold.b, 0.6f));
                        trail.life = 0.25f;
                    }
                    else
                    {
                        p.vel.y -= 70f * dt;
                        p.vel *= Mathf.Exp(-1.6f * dt);
                        p.pos += p.vel * dt;
                        if (p.age >= p.life)
                        {
                            Kill(sparks, i);
                            continue;
                        }
                        float u = p.age / p.life;
                        // migotanie pod koniec życia, potem znika
                        bool on = u < 0.6f || Mathf.Repeat(p.age * 18f + i, 2f) < 1.2f;
                        p.img.color = on ? new Color(p.color.r, p.color.g, p.color.b, p.color.a * (1f - u * u)) : Color.clear;
                        if (u > 0.5f)
                            p.img.rectTransform.sizeDelta = Vector2.one;
                    }
                    p.img.rectTransform.anchoredPosition = new Vector2(Mathf.Round(p.pos.x), Mathf.Round(p.pos.y));
                }
                yield return null;
            }
            Destroy(layer.gameObject);
        }

        void Explode(RectTransform layer, List<Spark> sparks, Vector2 at, Color rarityColor, Color gold, GiftRarity rarity)
        {
            GameEvents.PlaySound("firework", 0.5f);
            int n = 26 + 6 * (int)rarity;
            float speed = Random.Range(55f, 80f) + 8f * (int)rarity;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n + Random.Range(-0.1f, 0.1f);
                float v = speed * Random.Range(0.75f, 1.05f);
                var c = i % 3 == 0 ? gold : i % 7 == 0 ? Color.white : rarityColor;
                var p = NewSpark(layer, sparks, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v, 2, c);
                p.life = Random.Range(0.8f, 1.3f);
            }
            // błysk w środku wybuchu
            var flash = NewSpark(layer, sparks, at, Vector2.zero, 4, Color.white);
            flash.life = 0.08f;
        }

        static Spark NewSpark(RectTransform layer, List<Spark> sparks, Vector2 pos, Vector2 vel, int size, Color c)
        {
            var img = Ui.Img(layer, "Spark", GiftGrid.White(), false);
            Ui.Place(img.rectTransform, Ui.Center, Ui.Center, pos, new Vector2(size, size));
            img.color = c;
            var p = new Spark { img = img, pos = pos, vel = vel, color = c, life = 1f };
            sparks.Add(p);
            return p;
        }

        static void Kill(List<Spark> sparks, int i)
        {
            if (sparks[i].img != null)
                Destroy(sparks[i].img.gameObject);
            sparks.RemoveAt(i);
        }

        static float Weight(int k, int count) => 0.35f + 1.3f * Mathf.Pow(count > 1 ? k / (float)(count - 1) : 1f, 2f);

        static Vector2 Orbit(float angle, float r) => new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 0.7f);

        /// <summary>Wybici lecą na bok, hamują i szarzeją (zostają przy krawędzi okna aż do powrotu).</summary>
        void Fly(List<Fighter> fighters, Vector2 center)
        {
            var half = stage.rect.size * 0.5f - new Vector2(10f, 10f);
            foreach (var f in fighters)
            {
                if (!f.out_)
                    continue;
                f.pos += f.vel * Dt;
                f.vel *= Mathf.Exp(-4f * Dt);
                f.pos = new Vector2(Mathf.Clamp(f.pos.x, -half.x, half.x), Mathf.Clamp(f.pos.y, -half.y, half.y));
                f.flash -= Dt;
                if (f.flash > 0f)
                    Bright(f.cell, 1f, true);
                else
                    Dull(f.cell);
            }
        }

        static void Place(List<Fighter> fighters)
        {
            foreach (var f in fighters)
                f.cell.rect.anchoredPosition = new Vector2(Mathf.Round(f.pos.x), Mathf.Round(f.pos.y));
        }

        /// <summary>Klon w walce: pełny kolor i ramka rzadkości (białe błyśnięcie przy trafieniu).</summary>
        static void Bright(GiftGrid.Slot c, float alpha, bool flash = false)
        {
            var rc = Prestige.RarityColor(c.gift.rarity);
            c.icon.color = new Color(1f, 1f, 1f, alpha);
            c.border.color = flash ? Color.white : new Color(rc.r, rc.g, rc.b, alpha);
            c.back.color = new Color(1f, 1f, 1f, alpha);
            foreach (var p in c.pips)
                p.enabled = false;
        }

        static void Dull(GiftGrid.Slot c)
        {
            var rc = Prestige.RarityColor(c.gift.rarity);
            c.icon.color = new Color(0.55f, 0.55f, 0.6f, 0.7f);
            c.border.color = new Color(rc.r, rc.g, rc.b, 0.4f);
            c.back.color = new Color(1f, 1f, 1f, 0.7f);
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Rozbłysk 33x33: osiem promieni z przerywanym końcem i romb w środku, biały (kolor rzadkości nadaje Image).</summary>
        static Sprite Burst()
        {
            if (burst != null)
                return burst;
            const int n = 33, c = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, name = "GiftBurst" };
            var px = new Color32[n * n];
            void Put(int x, int y, byte a)
            {
                if (x < 0 || y < 0 || x >= n || y >= n)
                    return;
                int i = y * n + x;
                if (px[i].a < a)
                    px[i] = new Color32(255, 255, 255, a);
            }
            int[,] dirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 }, { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 } };
            for (int d = 0; d < 8; d++)
            {
                bool straight = d < 4;
                int len = straight ? 16 : 11;
                for (int k = 2; k <= len; k++)
                {
                    if (k > len - 5 && k % 2 == 1)
                        continue;   // przerywany koniec promienia
                    byte a = (byte)Mathf.Lerp(255f, 90f, k / (float)len);
                    Put(c + dirs[d, 0] * k, c + dirs[d, 1] * k, a);
                }
            }
            for (int y = -3; y <= 3; y++)
                for (int x = -3; x <= 3; x++)
                    if (Mathf.Abs(x) + Mathf.Abs(y) <= 3)
                        Put(c + x, c + y, (byte)(Mathf.Abs(x) + Mathf.Abs(y) == 3 ? 160 : 90));
            tex.SetPixels32(px);
            tex.Apply(false);
            burst = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            return burst;
        }
    }
}
