using System;
using System.Collections;
using UnityEngine;
using Wyspa.Core;
using Wyspa.Input;
using Wyspa.Localization;
using Wyspa.Visual;

namespace Wyspa.World
{
    /// <summary>
    /// Dekoracja na kafelku (drzewo, kamień, krzak, pień, gruz). Kilka szybkich kliknięć usuwa ją (koszt w Żarze, nagroda,
    /// GDD §14, §9 p. 121): każde kliknięcie to uderzenie (drgnięcie, odpryski), przerwa ponad ChopProgress.Window kasuje serię.
    /// Zabytek (landmark): klik otwiera panel z kawałkiem historii.
    /// Animacje skokowe ~10 kl./s: potrząśnięcie, przewracanie / pęknięcie / ścięcie, cząsteczki, potem pozostałość.
    /// </summary>
    public class Decoration : MonoBehaviour, IClickable, IContextActions
    {
        const float Ppu = 16f;
        const float Step = 0.1f;

        Island island;
        DecoArt art;
        SpriteRenderer sr;
        bool removing;
        Coroutine shake;
        Vector3 home;   // pozycja sprzed drgania (szybkie kliknięcia nie przesuwają obiektu)

        public TilePos Tile { get; private set; }
        public DecoKind Kind { get; private set; }
        public DecoArt Art => art;
        public bool Removing => removing;
        public string NameKey => art != null && !string.IsNullOrEmpty(art.nameKey) ? art.nameKey : "deco." + Kind;

        public void Init(Island owner, TilePos tile, DecoKind kind, DecoArt decoArt, SpriteRenderer renderer)
        {
            island = owner;
            Tile = tile;
            Kind = kind;
            art = decoArt;
            sr = renderer;
        }

        /// <summary>Ile szybkich uderzeń potrzeba: krzaki i drobiazgi 3, pnie i kłody 4, drzewa 5, kamienie 6.</summary>
        public int HitsNeeded
        {
            get
            {
                switch (Kind)
                {
                    case DecoKind.Rock: case DecoKind.BrokenWall: case DecoKind.FallenColumn: case DecoKind.BrokenPillar: return 6;
                    case DecoKind.TreeRound: case DecoKind.Spruce: case DecoKind.DeadTree: case DecoKind.Mangrove: return 5;
                    case DecoKind.Stump: case DecoKind.StumpSpruce: case DecoKind.StumpDead: case DecoKind.RottenLog: return 4;
                    default: return 3;
                }
            }
        }

        public void OnClick()
        {
            if (removing || art == null)
                return;
            if (art.landmark)
            {
                GameEvents.RequestContext(this);
                return;
            }
            if (!art.removable)
                return;
            var game = GameLoop.Instance;
            if (game != null && game.Essence.Amount < art.essenceCost)
            {
                // za mało Żaru: wyraźne „nie” (drgnięcie bez odprysków) i ile brakuje, zamiast cichego braku reakcji
                GameEvents.RaiseToast("ui.deco.need_have", art.essenceCost, Mathf.FloorToInt(game.Essence.Amount));
                StopShake();
                home = transform.position;
                shake = StartCoroutine(Refuse());
                return;
            }
            var top = sr != null ? new Vector3(transform.position.x, sr.bounds.max.y, 0f) : transform.position + Vector3.up;
            int hits = ChopProgress.For(gameObject).Hit(HitsNeeded, top);
            StopShake();
            if (hits >= HitsNeeded)
            {
                ChopProgress.For(gameObject).Clear();
                island.TryRemove(this);
                return;
            }
            home = transform.position;
            shake = StartCoroutine(HitShake());
        }

        void StopShake()
        {
            if (shake == null)
                return;
            StopCoroutine(shake);
            shake = null;
            transform.position = home;
        }

        /// <summary>Odmowa (za mało Żaru): dwa szybkie drgnięcia, bez dźwięku uderzenia i odprysków.</summary>
        IEnumerator Refuse()
        {
            for (int i = 0; i < 4 && !removing; i++)
            {
                transform.position = home + new Vector3((i % 2 == 0 ? 1f : -1f) / Ppu, 0f, 0f);
                yield return new WaitForSecondsRealtime(0.05f);
            }
            transform.position = home;
            shake = null;
        }

        /// <summary>Jedno uderzenie: drgnięcie o piksel, kilka odprysków (drzazgi, okruchy, liście), stuk.</summary>
        IEnumerator HitShake()
        {
            var fx = PixelParticles.Instance;
            int order = sr != null ? sr.sortingOrder : 0;
            if (fx != null && art.chips != null && art.chips.Length > 0)
                fx.Burst(transform.position + new Vector3(0f, 6f / Ppu, 0f), art.chips, 2, 30f, 140f, 0.45f, order + 2);
            GameEvents.PlaySound("chop", 0.6f);
            for (int i = 0; i < 3 && !removing; i++)
            {
                transform.position = home + new Vector3((i % 2 == 0 ? 1f : -1f) / Ppu, 0f, 0f);
                yield return new WaitForSecondsRealtime(0.04f);
            }
            transform.position = home;
            shake = null;
        }

        public void FillContext(ContextInfo info)
        {
            info.title = Loc.Get(NameKey);
            info.worldAnchor = transform.position + new Vector3(0f, 1.25f, 0f);
            if (art != null && art.landmark)
            {
                // zabytek: nie da się usunąć, za to opowiada kawałek historii wyspy
                if (!string.IsNullOrEmpty(art.loreKey))
                {
                    info.lines.Add(Loc.Get(art.loreKey));
                    Lore.Chronicle.Note("lore:" + art.loreKey);
                }
                return;
            }
            if (art == null || !art.removable)
                return;
            info.lines.Add(Loc.Get("ui.deco.cost", art.essenceCost));
            info.lines.Add(Loc.Get("ui.deco.reward." + art.reward.ToString().ToLowerInvariant(), art.rewardAmount));
            var game = GameLoop.Instance;
            bool afford = game != null && game.Essence.Amount >= art.essenceCost;
            info.Button(Loc.Get("act.remove"), () => island.TryRemove(this), afford);
        }

        /// <summary>Animacja usuwania; onDone woła wyspa (pozostałość albo wolny kafelek).</summary>
        public void PlayRemoval(Action onDone)
        {
            if (removing)
                return;
            removing = true;
            StartCoroutine(Remove(onDone));
        }

        IEnumerator Remove(Action onDone)
        {
            var wait = new WaitForSecondsRealtime(Step);
            var basePos = transform.position;
            var fx = PixelParticles.Instance;
            int order = sr != null ? sr.sortingOrder : 0;
            var chipsAt = basePos + new Vector3(0f, 6f / Ppu, 0f);
            // potrząśnięcie o piksel (jak w prototypie)
            for (int i = 0; i < 4; i++)
            {
                transform.position = basePos + new Vector3((i % 2 == 0 ? 1f : -1f) / Ppu, 0f, 0f);
                yield return wait;
            }
            transform.position = basePos;

            var frames = art != null ? art.frames : null;
            switch (art != null ? art.anim : RemoveAnim.Poof)
            {
                case RemoveAnim.Fall:
                    if (frames != null)
                        for (int i = 0; i < frames.Length; i++)
                        {
                            if (frames[i] != null) sr.sprite = frames[i];
                            if (i == frames.Length - 2 && fx != null)
                                fx.Burst(chipsAt, art.chips, 8, 40f, 150f, 0.6f, order + 2);
                            yield return new WaitForSecondsRealtime(0.12f);
                        }
                    Dust(fx, basePos + new Vector3(12f / Ppu, 2f / Ppu, 0f), order);
                    yield return new WaitForSecondsRealtime(0.25f);
                    break;
                case RemoveAnim.Crack:
                    if (frames != null && frames.Length > 0 && frames[0] != null)
                        sr.sprite = frames[0];
                    if (fx != null)
                        fx.Burst(chipsAt, art.chips, 6, 36f, 160f, 0.5f, order + 2);
                    yield return new WaitForSecondsRealtime(0.35f);
                    Dust(fx, basePos, order);
                    break;
                case RemoveAnim.Cut:
                    if (frames != null && frames.Length > 0 && frames[0] != null)
                        sr.sprite = frames[0];
                    if (fx != null)
                        fx.Burst(chipsAt, art.chips, 6, 30f, 120f, 0.6f, order + 2);
                    // ścięty krzak znika po chwili (GDD §14.2), migając jak cząsteczki
                    yield return new WaitForSecondsRealtime(2.5f);
                    for (int i = 0; i < 6; i++)
                    {
                        sr.enabled = i % 2 == 1;
                        yield return wait;
                    }
                    break;
                default:
                    if (fx != null)
                        fx.Burst(chipsAt, art != null ? art.chips : null, 6, 34f, 150f, 0.5f, order + 2);
                    Dust(fx, basePos, order);
                    break;
            }
            onDone?.Invoke();
        }

        void Dust(PixelParticles fx, Vector3 at, int order)
        {
            var dust = island != null && island.Art != null ? island.Art.dust : null;
            if (fx == null || dust == null || dust.Length == 0)
                return;
            for (int i = 0; i < 3; i++)
                fx.Spawn(at + new Vector3((i - 1) * 5f / Ppu, 2f / Ppu, 0f), dust, new Vector2((i - 1) * 6f, 8f), 0f, 0.4f, order + 3, false, 8f);
        }
    }
}
