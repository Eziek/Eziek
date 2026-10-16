using UnityEngine;

namespace Wyspa.UI
{
    /// <summary>
    /// Wejście dla UI i świata - działa z Input System i ze starym Input. Dotyk (telefon, ekran dotykowy; §9 p. 134)
    /// udaje mysz: krótkie stuknięcie = lewy klik (przy puszczeniu), przytrzymanie w miejscu = prawy przycisk (panel informacji
    /// o obiekcie albo pierścień akcji; §9 p. 136), chyba że świat przejął dotyk (ClaimPointer: przytrzymanie drzewa ścina je),
    /// przeciągnięcie jednym palcem = przesuwanie widoku (TouchPan), dwa palce: przesuwanie i szczypanie (PinchStep = przybliżenie).
    /// Pozycja „kursora” przy dotyku to ostatnie miejsce palca (dymki pokazują się tam, gdzie był palec).
    /// </summary>
    public static class UiInput
    {
        /// <summary>Ile sekund trzymania palca w miejscu to prawy klik.</summary>
        const float LongPress = 0.45f;
        /// <summary>O ile pikseli palec może się przesunąć, żeby to nadal było stuknięcie (a nie przesuwanie widoku).</summary>
        const float TapSlop = 14f;
        /// <summary>Szczypanie: zmiana odległości palców (stosunek), od której przybliżenie zmienia się o jeden stopień.</summary>
        const float PinchStepRatio = 1.3f;

#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Mouse M => UnityEngine.InputSystem.Mouse.current;
        static UnityEngine.InputSystem.Keyboard K => UnityEngine.InputSystem.Keyboard.current;
        static Vector2 MouseRaw => M != null ? M.position.ReadValue() : Vector2.zero;
        public static Vector2 MousePosition { get { Poll(); return touchUsed ? touchPos : MouseRaw; } }
        // przy dotyku przyciski myszy się nie liczą: Windows z ekranem dotykowym udaje też mysz (klik byłby podwójny)
        static bool MouseOk => M != null && !touchUsed;
        public static bool LeftPressed { get { Poll(); return (MouseOk && M.leftButton.wasPressedThisFrame) || tap; } }
        /// <summary>Wciśnięcie lewego albo dotknięcie ekranu jednym palcem (od razu, nie przy puszczeniu; np. przytrzymanie, by ściąć).</summary>
        public static bool PointerDown { get { Poll(); return (MouseOk && M.leftButton.wasPressedThisFrame) || touchDown; } }
        /// <summary>Lewy trzymany albo jeden palec trzymany w miejscu (przesunięty palec to już przesuwanie widoku).</summary>
        public static bool PointerHeld { get { Poll(); return (MouseOk && M.leftButton.isPressed) || (fingers == 1 && !multi && !moved); } }
        /// <summary>Puszczenie lewego albo podniesienie palca w tej klatce.</summary>
        public static bool PointerUp { get { Poll(); return (MouseOk && M.leftButton.wasReleasedThisFrame) || touchUp; } }
        public static bool RightPressed { get { Poll(); return (MouseOk && M.rightButton.wasPressedThisFrame) || longStart; } }
        public static bool RightHeld { get { Poll(); return (MouseOk && M.rightButton.isPressed) || (longFired && fingers == 1); } }
        public static bool RightReleased { get { Poll(); return (MouseOk && M.rightButton.wasReleasedThisFrame) || longEnd; } }
        public static bool EscapePressed => K != null && K.escapeKey.wasPressedThisFrame;
        public static bool F1Pressed => K != null && K.f1Key.wasPressedThisFrame;
        /// <summary>Kółko myszy w tej klatce: +1 w górę, -1 w dół, 0 brak.</summary>
        public static int Scroll => M == null ? 0 : Step(M.scroll.ReadValue().y);
        public static bool CtrlHeld => K != null && (K.leftCtrlKey.isPressed || K.rightCtrlKey.isPressed);
        /// <summary>Ctrl+S: zapis gry.</summary>
        public static bool SavePressed => K != null && CtrlHeld && K.sKey.wasPressedThisFrame;
#else
        public static Vector2 MousePosition => UnityEngine.Input.mousePosition;
        public static bool LeftPressed => UnityEngine.Input.GetMouseButtonDown(0);
        public static bool PointerDown => UnityEngine.Input.GetMouseButtonDown(0);
        public static bool PointerHeld => UnityEngine.Input.GetMouseButton(0);
        public static bool PointerUp => UnityEngine.Input.GetMouseButtonUp(0);
        public static bool RightPressed => UnityEngine.Input.GetMouseButtonDown(1);
        public static bool RightHeld => UnityEngine.Input.GetMouseButton(1);
        public static bool RightReleased => UnityEngine.Input.GetMouseButtonUp(1);
        public static bool EscapePressed => UnityEngine.Input.GetKeyDown(KeyCode.Escape);
        public static bool F1Pressed => UnityEngine.Input.GetKeyDown(KeyCode.F1);
        public static int Scroll => Step(UnityEngine.Input.mouseScrollDelta.y);
        public static bool CtrlHeld => UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
        public static bool SavePressed => CtrlHeld && UnityEngine.Input.GetKeyDown(KeyCode.S);
#endif

        /// <summary>Przesunięcie palca (jednego albo środka dwóch) w tej klatce, w pikselach ekranu; zero bez przesuwania.</summary>
        public static Vector2 TouchPan { get { Poll(); return pan; } }
        /// <summary>Szczypanie w tej klatce: +1 przybliż, -1 oddal, 0 nic (stopniami, jak kółko myszy).</summary>
        public static int PinchStep { get { Poll(); return pinchStep; } }
        /// <summary>
        /// Ten dotyk należy już do świata (przytrzymanie, by ściąć): przy puszczeniu nie będzie stuknięcia, a trzymanie
        /// nie zamieni się w prawy przycisk (panel informacji). Ważne do podniesienia palca.
        /// </summary>
        public static void ClaimPointer() => claimed = true;

        /// <summary>Ostatnie wejście było dotykiem (np. żeby nie chować HUD-u, bo palec nie „rusza myszą”).</summary>
        public static bool TouchUsed { get { Poll(); return touchUsed; } }

        // ---- stan dotyku, liczony raz na klatkę przy pierwszym pytaniu
        static int polledFrame = -1;
        static int fingers;                       // palce na ekranie w poprzedniej klatce
        static Vector2 touchPos, downPos, lastCenter;
        static float downTime, lastDistance, pinchAccum = 1f;
        static bool touchUsed, multi, moved, longFired, claimed;
        static bool tap, longStart, longEnd, touchDown, touchUp;   // zdarzenia tej klatki
        static Vector2 pan;
        static int pinchStep;

        static void Poll()
        {
            if (polledFrame == Time.frameCount)
                return;
            polledFrame = Time.frameCount;
            tap = longStart = longEnd = touchDown = touchUp = false;
            pan = Vector2.zero;
            pinchStep = 0;
#if ENABLE_INPUT_SYSTEM
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            int n = 0;
            Vector2 a = Vector2.zero, b = Vector2.zero;
            if (ts != null)
                for (int i = 0; i < ts.touches.Count; i++)
                {
                    var tc = ts.touches[i];
                    if (tc == null || !tc.press.isPressed)
                        continue;
                    if (n == 0) a = tc.position.ReadValue();
                    else if (n == 1) b = tc.position.ReadValue();
                    n++;
                }
            if (n > 0)
                touchUsed = true;
            else if (fingers == 0 && M != null && (M.leftButton.wasPressedThisFrame || M.rightButton.wasPressedThisFrame))
                touchUsed = false;   // wrócił do myszy (prawdziwy klik, bez palca na ekranie)
            if (n >= 2)
            {
                // dwa palce: przesuwanie środkiem, szczypanie odległością; to już nie stuknięcie ani przytrzymanie
                var center = (a + b) * 0.5f;
                float distance = (a - b).magnitude;
                if (fingers >= 2)
                {
                    pan = center - lastCenter;
                    if (lastDistance > 1f)
                        pinchAccum *= distance / lastDistance;
                    if (pinchAccum >= PinchStepRatio) { pinchStep = 1; pinchAccum = 1f; }
                    else if (pinchAccum <= 1f / PinchStepRatio) { pinchStep = -1; pinchAccum = 1f; }
                }
                else
                    pinchAccum = 1f;
                if (longFired) { longFired = false; longEnd = true; }
                if (!multi && fingers == 1) touchUp = true;   // drugi palec kończy przytrzymanie pierwszego
                multi = true;
                lastCenter = center;
                lastDistance = distance;
                touchPos = center;
            }
            else if (n == 1)
            {
                if (fingers == 0)
                {
                    downPos = a;
                    downTime = Time.unscaledTime;
                    multi = moved = longFired = claimed = false;
                    touchDown = true;
                }
                else if (!multi)
                {
                    if (!longFired && (a - downPos).magnitude > TapSlop)
                        moved = true;
                    if (moved && !longFired)
                        pan = a - touchPos;   // jeden palec przeciąga widok
                    if (!moved && !longFired && !claimed && Time.unscaledTime - downTime >= LongPress)
                    {
                        longFired = true;     // przytrzymanie = prawy przycisk
                        longStart = true;
                    }
                }
                touchPos = a;
            }
            else if (fingers > 0)
            {
                // palec puszczony: krótko i w miejscu = lewy klik, po przytrzymaniu = puszczenie prawego
                if (longFired)
                    longEnd = true;
                else if (!multi && !moved && !claimed && Time.unscaledTime - downTime < LongPress)
                    tap = true;
                if (!multi)
                    touchUp = true;
                longFired = false;
            }
            fingers = n;
#endif
        }

        // Mathf.Sign(0) zwraca 1, więc znak liczymy sami
        static int Step(float v) => v > 0.01f ? 1 : (v < -0.01f ? -1 : 0);
    }
}
