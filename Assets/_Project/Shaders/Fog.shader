// Mgła (GDD §4.6, §13): tylko nad morzem, kłębiaste chmury w pixel arcie (prośba gracza: jak cumulusy z referencji,
// w brudnej palecie dark fantasy). Rozbudowa wyspy odsuwa ją sama (mapa odległości liczona od nowego brzegu).
// Port tools/pixel-proto/fog_clouds_preview.py (podgląd docs/img/mgla_chmury.png), liczona w siatce 32 px jak woda.
// Sprite = mapa danych wody malowana przez Island (R = odległość x4, G > 0 = woda). Gęstość ustawia FogController.
Shader "Wyspa/Fog"
{
    Properties
    {
        _MainTex("Water data", 2D) = "black" {}
        _Tone0("Tone 0 (darkest)", Color) = (0.106, 0.125, 0.161, 1)
        _Tone1("Tone 1", Color) = (0.165, 0.192, 0.235, 1)
        _Tone2("Tone 2", Color) = (0.247, 0.278, 0.318, 1)
        _Tone3("Tone 3", Color) = (0.361, 0.376, 0.388, 1)
        _Tone4("Tone 4", Color) = (0.514, 0.502, 0.463, 1)
        _Tone5("Tone 5 (highlight)", Color) = (0.663, 0.627, 0.533, 1)
        _Density("Density (set by FogController)", Range(0, 2)) = 1
        _FogFrom("Fog starts (px from shore)", Float) = 22
        _FogSpan("Fog thickens over (px)", Float) = 40
        _FogPeriod("Drift loop (s)", Float) = 30
        _FogOuter("Band mode: water band edge (px, 0 = off)", Float) = 0

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

            #pragma vertex FogVertex
            #pragma fragment FogFragment

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_TexelSize;
                half4 _Tone0, _Tone1, _Tone2, _Tone3, _Tone4, _Tone5;
                float _Density;
                float _FogFrom;
                float _FogSpan;
                float _FogPeriod;
                float _FogOuter;
            CBUFFER_END

            Varyings FogVertex(Attributes input)
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

            // Chmura = bryła z guzków (sylwetka kalafiora): rdzeń i FOG_NB guzków na obwodzie. Chmura niżej na ekranie stoi
            // bliżej i zasłania wyższe. Cieniowanie na dwóch poziomach: bryła jak kula (jasna góra-lewo, ciemny dół-prawo),
            // na krawędzi sylwetki od światła jasny rąbek, od cienia ciemny, w środku jasne łuki guzków. 6 tonów, bez obrysu,
            // granice tonów poszarpane szumem w parach pikseli. Liczone w pikselach mgły (2 na piksel danych), y w dół.
            #define FOG_SCALE 2.0     // piksele mgły na piksel danych wody (siatka 32 px)
            #define FOG_CELL 30.0     // komórka siatki chmur (piksele mgły)
            #define FOG_NB 6          // guzki na obwodzie bryły
            #define FOG_RIM 2.5       // szerokość rąbka
            #define FOG_CAP 2.0       // łuki guzków w środku bryły
            #define FOG_BAND 0.6      // przezroczyste okno: mniejsze, gęstsze chmury (mieszczą się w FOG_REACH)
            // przezroczyste okno: mgła kończy się najdalej tyle pikseli danych za pasem wody (zawsze w kadrze)
            #define FOG_REACH 18.0
            // tony: środek, rozpiętość bryły, rąbek, łuki, głębia (ciemniej dalej od wyspy), szum, podstawa
            #define TONE_MID 2.9
            #define TONE_SPAN 2.6
            #define TONE_RIM 1.1
            #define TONE_CAP 0.6
            #define TONE_DEPTH 1.5
            #define TONE_JIT 0.35
            #define TONE_BASE 1.2

            static const float3 FOG_L = float3(-0.53233, -0.72590, 0.43554);   // światło z lewej-góry (y w dół), lekko od widza
            static const float2 FOG_L2 = float2(-0.59137, -0.80641);           // to samo na płaszczyźnie

            struct FogCloud
            {
                float2 c;
                float rc;
                float3 b[FOG_NB + 1];   // guzki: środek xy, promień z; ostatni = rdzeń
            };

            // odległość od brzegu (piksele danych) w punkcie danych, bez interpolacji
            float FogDist(float2 pd, float2 size)
            {
                half4 data = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, (floor(pd) + 0.5) / size, 0);
                return data.r * 255.0 / 4.0;
            }

            // to samo, interpolowane (gładka granica podstawy i limitu zasięgu w siatce 32 px)
            float FogDistSmooth(float2 pd, float2 size)
            {
                float2 tp = pd - 0.5;
                float2 i0 = floor(tp), fr = tp - i0;
                float a = FogDist(i0 + 0.5, size), b = FogDist(i0 + float2(1.5, 0.5), size);
                float c = FogDist(i0 + float2(0.5, 1.5), size), e = FogDist(i0 + 1.5, size);
                return lerp(lerp(a, b, fr.x), lerp(c, e, fr.x), fr.y);
            }

            // punkt mgły (y w dół) -> punkt danych (y w górę)
            float2 FogToData(float2 p, float2 fsize)
            {
                return float2(p.x, fsize.y - p.y) / FOG_SCALE;
            }

            void FogCloudCenter(float2 cell, float seed, float scale, float T, out float2 c, out float rc)
            {
                float4 h = WyspaHash(float2(cell.x + 17.0 + seed, cell.y + 5.0));
                c = float2((cell.x + 0.25 + 0.5 * h.x) * FOG_CELL * scale + round(sin(T + h.z * 6.2831853) * 2.0),
                           (cell.y + 0.25 + 0.5 * h.y) * FOG_CELL * scale);
                rc = (14.0 + 10.0 * h.z) * scale;
            }

            void FogCloudBumps(inout FogCloud o, float2 cell, float seed, float T)
            {
                [unroll] for (int b = 0; b < FOG_NB; b++)
                {
                    float4 g = WyspaHash(float2(cell.x * 7.0 + b * 13.0 + 1.0 + seed, cell.y * 3.0 + b * 5.0 + 2.0));
                    float ang = (b + 0.6 * g.x) / FOG_NB * 6.2831853;
                    float rr = o.rc * (0.45 + 0.35 * g.y);
                    // chmury szersze niż wyższe; guzki lekko „oddychają” (całkowita wielokrotność T: pętla bez skoku)
                    o.b[b] = float3(o.c + float2(cos(ang) * rr, sin(ang) * rr * 0.7),
                                    o.rc * (0.42 + 0.25 * g.z) + 0.6 * sin(T * 2.0 + g.w * 6.2831853));
                }
                o.b[FOG_NB] = float3(o.c, o.rc * 0.72);
            }

            bool FogInCloud(FogCloud o, float2 p)
            {
                [unroll] for (int b = 0; b <= FOG_NB; b++)
                {
                    float2 v = p - o.b[b].xy;
                    if (dot(v, v) < o.b[b].z * o.b[b].z)
                        return true;
                }
                return false;
            }

            // jasne łuki guzków w środku bryły: piksel w guzku, a punkt przesunięty ku światłu już poza nim
            bool FogInnerCap(FogCloud o, float2 p)
            {
                float2 s = p + FOG_L2 * FOG_CAP;
                [unroll] for (int b = 0; b < FOG_NB; b++)
                {
                    float2 v = p - o.b[b].xy, w = s - o.b[b].xy;
                    float r2 = o.b[b].z * o.b[b].z;
                    if (dot(v, v) < r2 && dot(w, w) >= r2)
                        return true;
                }
                return false;
            }

            half4 FogFragment(Varyings input) : SV_Target
            {
                float2 size = _MainTex_TexelSize.zw;
                float2 fsize = size * FOG_SCALE;
                float2 q = floor(input.uv * fsize);
                float2 p = float2(q.x + 0.5, fsize.y - q.y - 0.5);   // środek piksela mgły, y w dół
                float2 pd = FogToData(p, fsize);
                half4 data = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, (floor(pd) + 0.5) / size, 0);
                bool water = data.g > 0.06;
                float d = FogDistSmooth(pd, size);    // odległość od brzegu w pikselach danych (do ok. 64)
                float T = 6.2831853 * frac(_Time.y / _FogPeriod);   // płynny dryf chmur
                bool band = _FogOuter > 0.0;
                // gęstsza mgła (noc, zgaszone lampy) podchodzi bliżej brzegu; w przezroczystym oknie zaczyna się przy wodzie
                float edge = band ? _FogOuter : _FogFrom / max(0.3, _Density);
                float lo = band ? edge + 1.0 : edge + 2.0;
                float hi = band ? edge + 8.0 : 1e6;
                float scale = band ? FOG_BAND : 1.0;
                float cellSize = FOG_CELL * scale;
                float2 g0 = floor(p / cellSize);

                float bestY = -1e6, big = 0, rim = 0, depth = 0;
                bool has = false;
                [loop] for (int i = -1; i <= 1; i++)
                [loop] for (int j = -1; j <= 1; j++)
                [unroll] for (int k = 0; k <= 1; k++)   // dwie chmury na komórkę (gęsto, bez dziur)
                {
                    float2 cell = g0 + float2(i, j) + 0.5 * k;
                    float seed = 41.0 * k;
                    FogCloud o = (FogCloud)0;
                    FogCloudCenter(cell, seed, scale, T, o.c, o.rc);
                    float2 v = p - o.c;
                    float reach = o.rc * 1.5 + 3.5;
                    if (o.c.y <= bestY || dot(v, v) > reach * reach)
                        continue;
                    float dc = FogDist(FogToData(o.c, fsize), size);
                    if (dc < lo || dc > hi)
                        continue;
                    FogCloudBumps(o, cell, seed, T);
                    if (!FogInCloud(o, p))
                        continue;
                    // bryła jak spłaszczona kula
                    float2 qq = v / (o.rc * float2(1.25, 0.95));
                    float q2 = min(dot(qq, qq), 1.0);
                    big = dot(float3(qq, sqrt(1.0 - q2)), FOG_L);
                    bool litEdge = !FogInCloud(o, p + FOG_L2 * FOG_RIM);
                    bool darkEdge = !FogInCloud(o, p - FOG_L2 * FOG_RIM);
                    rim = litEdge ? 1.0 : (darkEdge ? -1.0 : (FogInnerCap(o, p) ? TONE_CAP : 0.0));
                    depth = saturate((dc - edge) / 30.0);
                    bestY = o.c.y;
                    has = true;
                }
                bool base = !band && d > edge + 10.0;   // zwykłe okno: dalej od wyspy morze całe w mgle

                float jit = WyspaHash(float2(floor(p.x / 2.0) + 3.0, q.y + 11.0)).x - 0.5;
                float t = has ? TONE_MID + big * TONE_SPAN + rim * TONE_RIM - depth * TONE_DEPTH + jit * TONE_JIT
                              : TONE_BASE - saturate((d - edge) / 30.0) + jit * TONE_JIT;
                int idx = (int)clamp(floor(t), 0.0, 5.0);
                half3 col = idx == 0 ? _Tone0.rgb : idx == 1 ? _Tone1.rgb : idx == 2 ? _Tone2.rgb
                          : idx == 3 ? _Tone3.rgb : idx == 4 ? _Tone4.rgb : _Tone5.rgb;
                half a = (has || base) ? 0.95 : 0.0;
                if (band && d >= edge + FOG_REACH) a = 0;   // twardy limit zasięgu: chmury kończą się przed krawędzią okna
                if (!water) a = 0;

                // światło liczymy zawsze z alfą 1 (CombinedShapeLightShared odrzuca alfę 0), alfę ustawiamy na końcu
                half4 lit = WyspaLight(col, 1, input.uv, input.lightingUV);
                lit.rgb = max(lit.rgb, col * 0.45);   // w nocy mgła zostaje bladą poświatą, nie znika w ciemności
                return half4(lit.rgb, a * input.color.a);
            }
            ENDHLSL
        }
    }
}
