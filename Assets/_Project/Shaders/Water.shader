// Woda (GDD §4.5) liczona w shaderze z mapy danych (Ground/water_data.png, opis w tools/pixel-proto/export_unity_fx.py):
// głębia, ryby pod wodą (ósemki), coś dużego w głębinie, połyski, grzbiety fal, przybój, pulsująca piana.
// Port prototypu tools/pixel-proto/water.py (10 kl./s). Ryby i potwora ustawia WaterAnimator (tablice z C#).
// obraz w 32 px, dno z paralaksą i załamaniem, światełka
// pod wodą, fale załamujące się w pianę, kręgi. Dane (odległość, klasy) zostają w siatce 16 px.
Shader "Wyspa/Water"
{
    Properties
    {
        _MainTex("Static water (fallback)", 2D) = "white" {}
        _DataTex("Water data", 2D) = "black" {}
        _ColO("o", Color) = (0.059, 0.039, 0.063, 1)
        _ColP0("p0", Color) = (0.110, 0.082, 0.125, 1)
        _ColB0("b0", Color) = (0.071, 0.110, 0.153, 1)
        _ColB1("b1", Color) = (0.122, 0.188, 0.267, 1)
        _ColB2("b2", Color) = (0.188, 0.282, 0.373, 1)
        _ColB3("b3", Color) = (0.337, 0.412, 0.478, 1)
        _ColFo("fo", Color) = (0.518, 0.557, 0.608, 1)
        _ColGlow("Glowing fish", Color) = (0.373, 0.812, 0.722, 1)
        _ColEye("Beast eye", Color) = (0.824, 1.0, 0.941, 1)
        _Night("Night (0..1, set by WaterAnimator)", Range(0, 1)) = 0
        _FishPeriod("Fish loop (s)", Float) = 30
        _BeastPeriod("Beast drift loop (s)", Float) = 40
        _ShoreRange("Water only near shore (px, 0 = everywhere)", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex WaterVertex
            #pragma fragment WaterFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
            #include "WyspaCommon.hlsl"

            TEXTURE2D(_DataTex);
            SAMPLER(sampler_DataTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _DataTex_TexelSize;
                half4 _ColO, _ColP0, _ColB0, _ColB1, _ColB2, _ColB3, _ColFo, _ColGlow, _ColEye;
                float _Night;
                float _FishPeriod;
                float _BeastPeriod;
                float _ShoreRange;
            CBUFFER_END

            // Tablice ustawia WaterAnimator (SetVectorArray). Współrzędne w pikselach kadru, y w dół (jak w Pythonie).
            float4 _Fish[16];       // cx, cy, rx, ry
            float _WyspaBiolum;     // świecący przypływ (rzadka noc, Visual/RareNights): 0..1, globalnie
            float4 _FishB[16];      // faza (rad), świeci (0/1), gatunek (FishKinds), mnożnik czasu okrążenia
            float _FishCount;
            float4 _Beast;          // x, y, włączony (0/1)

            Varyings WaterVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = input.color * _Color;
                return o;
            }

            half3 BaseColor(int c)
            {
                if (c == 1) return _ColB2.rgb;
                if (c == 2) return _ColB1.rgb;
                if (c == 3) return _ColB0.rgb;
                return _ColP0.rgb;
            }

            // ryba to sylwetka 2 odcienie ciemniejsza od wody nad nią
            half3 DarkerColor(int c)
            {
                return c == 1 ? _ColB0.rgb : _ColO.rgb;
            }

            // kształty ryby (10x4), bit i = znak i w wierszu z water.py (FISH_A, FISH_B)
            uint FishRow(int shape, int row)
            {
                if (row == 0) return 0x30u;
                if (row == 2) return shape == 0 ? 0x3FEu : 0x3FDu;
                return shape == 0 ? 0x1F9u : 0x1FAu;
            }

            // ławica: 4 rybki 4x2 (przesunięcia w ramce 12x7)
            static const int2 FISH_SCHOOL[4] = { int2(0, 0), int2(6, 1), int2(2, 3), int2(8, 4) };

            // Gatunki ryb (GDD §9 p. 112, FishKinds w IslandPainter.cs): 0 zwykła 10x4, 1 ławica, 2 węgorz, 3 płaszczka,
            // 4 karp, 5 meduza, 6 żabnica. lx, ly = piksel względem lewego górnego rogu ramki; k liczy od ogona do głowy.
            // Wynik: 0 nic, 1 ciemna sylwetka, 2 jaśniej od wody (meduza), 3 przynęta żabnicy (świeci nocą).
            int FishPixel(int kind, int lx, int ly, bool right, float t, int f, int i)
            {
                if (kind == 1)
                {
                    [unroll] for (int j = 0; j < 4; j++)
                    {
                        int mx = lx - FISH_SCHOOL[j].x;
                        int my = ly - FISH_SCHOOL[j].y - (int)round(0.6 * sin(t * 2.0 + j * 1.7));
                        if (mx < 0 || mx >= 4 || my < 0 || my >= 2)
                            continue;
                        int k = right ? mx : 3 - mx;
                        uint row = my == 0 ? 0x6u : (((f + j) % 2) == 0 ? 0xFu : 0xEu);   // ogonek miga
                        if (((row >> (uint)k) & 1u) != 0u)
                            return 1;
                    }
                    return 0;
                }
                if (kind == 2)
                {
                    if (lx < 0 || lx >= 16 || ly < 0 || ly >= 5)
                        return 0;
                    int k = right ? lx : 15 - lx;
                    int c = 2 + (int)round(1.3 * sin(k * 0.55 - t * 5.0 + i));   // wężowy ruch wzdłuż ciała
                    int thick = k >= 12 ? 2 : (k >= 1 ? 1 : 0);
                    return ly >= c && ly < c + thick ? 1 : 0;
                }
                if (kind == 3)
                {
                    if (lx < 0 || lx >= 14 || ly < -1 || ly >= 7)
                        return 0;
                    float k = right ? lx : 13 - lx;
                    float wing = 3.0 + 0.9 * sin(t * 2.2 + i);   // skrzydła falują
                    float bx = (k - 8.5) / 5.0, by = (ly - 2.5) / wing;
                    if (bx * bx + by * by <= 1.0 && abs(bx) + abs(by) <= 1.25)
                        return 1;
                    return k < 4.0 && ly == 2 ? 1 : 0;           // cienki ogon
                }
                if (kind == 4)
                {
                    if (lx < 0 || lx >= 15 || ly < 0 || ly >= 5)
                        return 0;
                    float k = right ? lx : 14 - lx;
                    float bx = (k - 8.5) / 6.0, by = (ly - 2.0) / 2.2;
                    if (bx * bx + by * by <= 1.0)
                        return 1;
                    // rozwidlona płetwa ogonowa, macha co kilka klatek
                    int dy = ly - 2 + ((f + i) % 4 < 2 ? 0 : (ly < 2 ? 1 : 0));
                    if (k < 0.5) return abs(dy) == 2 || abs(dy) == 1 ? 1 : 0;
                    if (k < 1.5) return abs(dy) <= 1 ? 1 : 0;
                    return k < 2.5 && dy == 0 ? 1 : 0;
                }
                if (kind == 5)
                {
                    if (lx < 0 || lx >= 7 || ly < 0 || ly >= 6)
                        return 0;
                    float pulse = 0.4 * sin(t * 3.0 + i);           // dzwon kurczy się i rozpręża
                    float bx = (lx - 3.0) / (3.0 + pulse), by = (ly - 1.5) / 1.6;
                    if (ly <= 2 && bx * bx + by * by <= 1.0)
                        return 2;
                    if (ly >= 3)
                        [unroll] for (int j = 1; j <= 5; j += 2)
                            if (lx == j + (int)round(0.6 * sin(t * 2.0 + ly + j)))
                                return 2;                           // czułki
                    return 0;
                }
                if (kind == 6)
                {
                    if (lx < -1 || lx >= 11 || ly < -1 || ly >= 5)
                        return 0;
                    float k = right ? lx : 9 - lx;
                    float bx = (k - 4.5) / 4.5, by = (ly - 2.5) / 1.8;
                    if (bx * bx + by * by <= 1.0)
                        return 1;
                    if (k == 8.0 && ly == 0)
                        return 1;                                   // wędka na głowie
                    return k == 9.0 && ly == -1 ? 3 : 0;            // przynęta
                }
                if (lx < 0 || lx >= 10 || ly < 0 || ly >= 4)
                    return 0;
                int k0 = right ? lx : 9 - lx;
                int shape = ((f + i) % 4) < 2 ? 0 : 1;
                return ((FishRow(shape, ly) >> (uint)k0) & 1u) != 0u ? 1 : 0;
            }

            // gładki szum wartości 0..1 (dno, przerwy w grzbietach fal)
            float WNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = WyspaHash(i).x, b = WyspaHash(i + float2(1, 0)).x;
                float c = WyspaHash(i + float2(0, 1)).x, e = WyspaHash(i + float2(1, 1)).x;
                return lerp(lerp(a, b, f.x), lerp(c, e, f.x), f.y);
            }

            // odległość od brzegu (piksele danych) interpolowana dwuliniowo: w 32 px pasy głębi są gładkie, a nie schodkowe
            float SmoothDist(float2 uv, float2 size)
            {
                float2 tp = uv * size - 0.5;
                float2 i0 = floor(tp), fr = tp - i0;
                float2 ts = _DataTex_TexelSize.xy;
                float a = SAMPLE_TEXTURE2D_LOD(_DataTex, sampler_DataTex, (i0 + float2(0.5, 0.5)) * ts, 0).r;
                float b = SAMPLE_TEXTURE2D_LOD(_DataTex, sampler_DataTex, (i0 + float2(1.5, 0.5)) * ts, 0).r;
                float c = SAMPLE_TEXTURE2D_LOD(_DataTex, sampler_DataTex, (i0 + float2(0.5, 1.5)) * ts, 0).r;
                float e = SAMPLE_TEXTURE2D_LOD(_DataTex, sampler_DataTex, (i0 + float2(1.5, 1.5)) * ts, 0).r;
                return lerp(lerp(a, b, fr.x), lerp(c, e, fr.x), fr.y) * 255.0 / 4.0;
            }

            half4 WaterFragment(Varyings input) : SV_Target
            {
                float2 size = _DataTex_TexelSize.zw;
                float2 pix = floor(input.uv * size);
                half4 data = SAMPLE_TEXTURE2D_LOD(_DataTex, sampler_DataTex, (pix + 0.5) * _DataTex_TexelSize.xy, 0);
                int cls = (int)round(data.g * 255.0 / 32.0);
                // obraz w 32 px na kafelek (2 piksele na piksel danych), X/Y w pikselach 32 px (y w dół),
                // x/y w pikselach danych (ryby, potwór i klasy brzegu jak dotąd)
                float2 hp = floor(input.uv * size * 2.0);
                int X = (int)hp.x;
                int Y = (int)(size.y * 2.0 - 1.0 - hp.y);
                int x = X >> 1;
                int y = Y >> 1;
                float d = SmoothDist(input.uv, size);
                float f = WyspaFrame();
                float t = _Time.y;
                float th = WyspaBayer(X, Y);
                bool water = cls >= 1 && cls <= 4;
                bool night = _Night > 0.5;
                bool emit = false;
                half3 emitCol = _ColGlow.rgb;

                // --- głębia z ditheringiem w 32 px ---
                int band;
                if (d < 7.0) band = 1;
                else if (d < 10.0) band = th < (10.0 - d) / 3.0 ? 1 : 2;
                else if (d < 22.0) band = 2;
                else if (d < 32.0) band = th < (32.0 - d) / 10.0 ? 2 : 3;
                else if (d < 42.0) band = 3;
                else band = th < max(0.35, (52.0 - d) / 10.0) ? 3 : 4;
                int baseCls = water ? band : 1;
                half3 col = BaseColor(baseCls);

                // --- dno z paralaksą: kamienie i wodorosty przesuwają się wolniej niż kamera,
                // a przy brzegu wiersze lekko falują (załamanie światła w wodzie)
                float2 cam = _WorldSpaceCameraPos.xy * 32.0;
                float2 sb = float2(X, Y) + float2(-cam.x, cam.y) * 0.12 + float2(round(sin(t * 0.25) * 2.0), 0.0);
                if (d < 26.0)
                    sb.x += round(sin(Y * 0.35 + t * 2.4));
                if (water && d > 4.0)
                {
                    float rock = WNoise(sb / 9.0);
                    float weed = WNoise(sb / float2(2.5, 6.0) + 40.0);
                    half3 deeper = baseCls >= 4 ? _ColO.rgb : BaseColor(baseCls + 1);
                    if (rock > 0.62)
                    {
                        col = deeper;
                        if (WNoise((sb - float2(0, 1)) / 9.0) <= 0.62 && d < 22.0)
                            col = BaseColor(max(1, baseCls - 1));      // górna krawędź kamienia łapie światło
                    }
                    else if (weed > 0.74 && th < 0.5)
                        col = deeper;
                }

                // --- ryby ---
                int count = (int)_FishCount;
                [loop] for (int i = 0; i < 16; i++)
                {
                    if (i >= count) break;
                    float4 p = _Fish[i];
                    float4 q = _FishB[i];
                    int kind = (int)round(q.z);
                    float a = 6.2831853 * frac(t / (_FishPeriod * max(0.1, q.w))) + q.x;
                    int fx = (int)round(p.x + p.z * sin(a));
                    int fy = (int)round(p.y + p.w * sin(2.0 * a));
                    bool right = cos(a) > 0.0;
                    int tone = cls != 5 ? FishPixel(kind, x - fx, y - fy, right, t, (int)f, i) : 0;
                    if (tone != 0 && night && _WyspaBiolum > 0.0)
                    {
                        emit = true;   // świecący przypływ: ryby płyną jak turkusowe światełka
                        emitCol = _ColGlow.rgb;
                    }
                    else if (tone == 1)
                        col = DarkerColor(baseCls);
                    else if (tone == 2)
                        col = baseCls <= 1 ? _ColB3.rgb : BaseColor(baseCls - 1);   // meduza: jaśniej od wody
                    else if (tone == 3)
                    {
                        if (night) { emit = true; emitCol = _ColGlow.rgb; }
                        else col = _ColB3.rgb;
                    }
                    if (kind == 0 && q.y > 0.5 && night && x == fx + (right ? 8 : 1) && y == fy + 2)
                        emit = true;
                }

                // --- coś dużego w głębinie ---
                if (_Beast.z > 0.5)
                {
                    float ab = 6.2831853 * frac(t / _BeastPeriod);
                    int ox = (int)round(_Beast.x + 7.0 * cos(ab));
                    int oy = (int)round(_Beast.y + 2.0 * sin(ab));
                    int s = x - (ox - 20);
                    if (s >= 0 && s < 40 && (baseCls == 2 || baseCls == 3 || baseCls == 4) && cls <= 4)
                    {
                        float ts = s / 39.0;
                        float thk = 4.2 * sin(3.14159265 * min(1.0, ts * 1.6)) * (1.0 - ts * 0.6);
                        float yy = oy + 2.5 * sin(s * 0.35 - f * 6.2831853 / 24.0);
                        if (abs(y - round(yy)) <= floor(thk))
                            col = _ColO.rgb;
                    }
                    if (night && fmod(floor(f / 3.0), 24.0) == 0.0)
                    {
                        int ey = (int)round(oy + 2.5 * sin(-f * 6.2831853 / 24.0)) - 1;
                        if (x == ox - 17 && y == ey)
                        {
                            emit = true;
                            emitCol = _ColEye.rgb;
                        }
                    }
                }

                // --- światełka pod wodą: drobinki przy dnie pulsują i dryfują razem z dnem, nocą świecą
                if (water && d > 6.0)
                {
                    float2 lp = float2(X, Y) + float2(-cam.x, cam.y) * 0.12;
                    float2 c0 = floor(lp / 22.0);
                    [unroll] for (int cy = -1; cy <= 1; cy++)
                    [unroll] for (int cx = -1; cx <= 1; cx++)
                    {
                        float2 cell = c0 + float2(cx, cy);
                        float4 h = WyspaHash(cell + 7.3);
                        if (h.x < 0.28)
                        {
                            float2 ctr = cell * 22.0 + h.yz * 22.0
                                       + float2(sin(t * 0.5 + h.w * 6.28), cos(t * 0.4 + h.w * 6.28)) * 2.0;
                            float b = 0.5 + 0.5 * sin(t * (1.2 + h.w) + h.y * 6.28);
                            float r = 1.5 + 2.0 * b;
                            float dl = length(lp - ctr);
                            if (dl <= r * 0.45)
                            {
                                col = lerp(col, _ColGlow.rgb, night ? 1.0 : 0.6);
                                if (night)
                                    emit = true;
                            }
                            else if (dl <= r && th < 0.35 + 0.4 * b)
                                col = lerp(col, _ColGlow.rgb, night ? 0.45 : 0.25);
                        }
                    }
                }

                // --- połyski (32 px): jeden na komórkę 48x20, dryfują w lewo ---
                [unroll] for (int gc = 0; gc < 2; gc++)
                {
                    float2 cell = float2(floor(X / 48.0) + gc, floor(Y / 20.0));
                    float4 h = WyspaHash(cell + 17.0);
                    if (h.x < 0.2)
                    {
                        int gx = (int)(cell.x * 48.0 + floor(h.y * 48.0));
                        int gy = (int)(cell.y * 20.0 + floor(h.z * 20.0));
                        float tt = fmod(f + floor(h.w * 48.0), 48.0);
                        int xx = gx - (int)floor(tt / 2.0);
                        int len = tt < 6.0 ? 3 : 2;
                        if (tt < 12.0 && Y == gy && X >= xx && X < xx + len && d > 9.0)
                            col = baseCls == 1 ? _ColB3.rgb : _ColB2.rgb;
                    }
                }

                // --- fale, które idą do brzegu, rosną i załamują się w pianę ---
                if (water)
                {
                    float sn = WNoise(float2(X, Y) / 28.0 + 3.1);
                    float gap = WNoise(float2(X, Y) / 9.0 + 11.0);
                    [unroll] for (int w = 0; w < 2; w++)
                    {
                        float ph = frac(t / 5.0 + w * 0.5 + sn * 0.35);
                        float dc = 15.0 * (1.0 - ph);                 // grzbiet zbliża się do brzegu
                        float wdt = 0.3 + 0.7 * saturate((8.0 - dc) / 8.0);
                        if (abs(d - dc) < wdt && d >= 1.0)
                        {
                            if (dc > 8.0) { if (gap > 0.5) col = _ColB2.rgb; }
                            else if (dc > 4.0) { if (gap > 0.42) col = gap > 0.75 ? _ColFo.rgb : _ColB3.rgb; }
                            else col = _ColFo.rgb;
                        }
                        // rozbryzg tuż przed rozbiciem
                        if (dc < 4.0 && d < dc && d >= 1.0 && WyspaHash(float2(X, Y) + floor(t * 10.0)).x > 0.82)
                            col = _ColFo.rgb;
                        // cofka: zostawiona piana odpływa i znika
                        if (ph < 0.25 && abs(d - (1.0 + ph * 12.0)) < 0.35 && gap > 0.55 + ph)
                            col = _ColB3.rgb;
                    }
                }

                // --- kręgi na wodzie jak na filmiku: rosną i bledną ---
                if (water && d > 8.0)
                {
                    float2 csz = float2(56.0, 40.0);
                    float2 c0 = floor(float2(X, Y) / csz);
                    [unroll] for (int ry = -1; ry <= 1; ry++)
                    [unroll] for (int rx = -1; rx <= 1; rx++)
                    {
                        float2 cell = c0 + float2(rx, ry);
                        float4 h = WyspaHash(cell + 91.7);
                        if (h.x < 0.35)
                        {
                            float2 ctr = cell * csz + 10.0 + h.yz * (csz - 20.0);
                            float u = frac(t / 4.0 + h.w);
                            float rxl = 3.0 + u * 16.0, ryl = rxl * 0.48;
                            float2 v = float2(X, Y) - ctr;
                            float e = length(v / float2(rxl, ryl));
                            if (abs(e - 1.0) * ryl < 0.6)
                            {
                                float ang = atan2(v.y, v.x);
                                float seg = floor((ang + 3.14159265) / 6.2831853 * 10.0);
                                if (WyspaHash(cell + seg * 1.7).x > 0.2 + 0.6 * u)
                                    col = (v.y < 0.0 && u < 0.6) ? _ColFo.rgb : _ColB3.rgb;
                            }
                        }
                    }
                }

                // --- świecący przypływ (rzadka noc): brzeg i grzbiety fal jarzą się turkusem, plamy światła dryfują ---
                bool glowTide = night && _WyspaBiolum > 0.0;
                if (glowTide && water && d < 18.0)
                {
                    float n = WNoise(float2(X, Y) / 5.0 + float2(t * 0.35, t * 0.2));
                    float spark = WyspaHash(float2(X, Y) + floor(t * 6.0)).x;
                    if (n > 0.74 - 0.06 * _WyspaBiolum * (1.0 - d / 18.0) || (d < 6.0 && spark > 0.94) || col.r == _ColFo.r)
                    {
                        emit = true;
                        emitCol = _ColGlow.rgb;
                    }
                }

                // --- cień klifu i pulsująca piana przy brzegu ---
                bool pulse = fmod(floor(f / 5.0), 2.0) == 0.0;
                if (cls == 5) col = _ColB0.rgb;
                if (cls == 6) col = pulse ? _ColFo.rgb : _ColB3.rgb;
                if (cls == 7) col = pulse ? _ColB2.rgb : _ColFo.rgb;
                if (cls >= 5) emit = glowTide && cls >= 6 && pulse;   // piana świeci w rytm przyboju
                if (cls >= 6 && emit) emitCol = _ColGlow.rgb;

                half alpha = cls > 0 ? input.color.a : 0;
                // przezroczyste okno: zostaje tylko pas wody przy brzegu, krawędź w szachownicę pikseli
                if (_ShoreRange > 0.0 && water)
                {
                    if (d > _ShoreRange || (d > _ShoreRange - 2.0 && ((X + Y) & 1) == 1))
                        alpha = 0;
                }
                half4 lit = WyspaLight(col, alpha, input.uv, input.lightingUV);

                // --- odbicia światła: iskierki tam, gdzie pada światło (komórki 3x1 px w 32 px) ---
                if (!emit && water && d > 3.0 && alpha > 0)
                {
                    half3 light = WyspaLight(half3(1, 1, 1), 1, input.uv, input.lightingUV).rgb;
                    float bl = max(light.r, max(light.g, light.b));
                    float chance = 0.02 * saturate((bl - 0.4) / 0.8);
                    float2 cell = float2(floor(X / 3.0), Y);
                    float4 g = WyspaHash(cell * 1.37 + floor(f / 4.0) * 7.31);
                    int gx = (int)(cell.x * 3.0 + floor(g.y * 2.0));
                    int glen = g.z < 0.3 ? 2 : 1;
                    if (g.x < chance && X >= gx && X < gx + glen)
                        lit.rgb = max(lit.rgb, _ColFo.rgb * light * 1.25);
                }
                return emit ? half4(emitCol, alpha) : lit;
            }
            ENDHLSL
        }
    }
}
