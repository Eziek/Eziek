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
    /// Dekoracja na kafelku (drzewo, kamień, krzak, pień, gruz). Klik = panel „Usuń” z kosztem w Esencji i nagrodą (GDD §14).
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

        public void OnClick()
        {
            if (!removing && art != null && art.removable)
                GameEvents.RequestContext(this);
        }

        public void FillContext(ContextInfo info)
        {
            info.title = Loc.Get(NameKey);
            info.worldAnchor = transform.position + new Vector3(0f, 1.25f, 0f);
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
