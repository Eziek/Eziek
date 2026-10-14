// Mgła (GDD §4.6, §13): tylko nad morzem, w stylu „chibi” (bulwiaste kłęby z obrysem i płaskimi kolorami, prośba gracza).
// Rozbudowa wyspy odsuwa ją sama (mapa odległości liczona od nowego brzegu). Port tools/pixel-proto/tiles_proto.py (render).
// Sprite = mapa danych wody malowana przez Island (R = odległość x4, G > 0 = woda). Gęstość ustawia FogController.
Shader "Wyspa/Fog"
{
    Properties
    {
        _MainTex("Water data", 2D) = "black" {}
        _ColB3("b3", Color) = (0.337, 0.412, 0.478, 1)
        _ColFo("fo", Color) = (0.518, 0.557, 0.608, 1)
        _ColLight("Light rim", Color) = (0.769, 0.792, 0.831, 1)
        _ColLine("Outline", Color) = (0.094, 0.110, 0.157, 1)
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
                half4 _ColB3, _ColFo, _ColLight, _ColLine;
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

            // Mgła „chibi” (prośba gracza): bulwiaste kłęby jak z kreskówki - koła rozsiane po komórkach siatki, płaskie kolory
            // (jasna góra, podstawa, cień od dołu) i ciemny obrys, ostre piksele. Kształt to pole odległości (SDF):
            // < 0 wewnątrz mgły. Pasmo bazowe + koła, których środki leżą w zadanym przedziale odległości od brzegu.
            #define FOG_CELL 11.0

            float FogDist(float2 p, float2 size)
            {
                half4 data = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, (floor(p) + 0.5) / size, 0);
                return data.r * 255.0 / 4.0;
            }

            // przezroczyste okno: mgła kończy się najdalej tyle pikseli za pasem wody (zawsze w kadrze, bez ucięcia oknem)
            #define FOG_REACH 18.0

            // band = przezroczyste okno (wał przy wodzie), inaczej mgła zakrywa morze dalej od wyspy
            float FogSdf(float2 p, float2 size, bool band, float edge, float T)
            {
                float d = FogDist(p, size);
                float s = band ? d - (edge + 5.0) : (edge + 14.0) - d;
                float lo = band ? edge + 2.0 : edge + 4.0;
                float hi = band ? edge + 13.0 : edge + 14.0;
                float2 g = floor(p / FOG_CELL);
                [unroll] for (int i = -1; i <= 1; i++)
                [unroll] for (int j = -1; j <= 1; j++)
                {
                    float2 cell = g + float2(i, j);
                    float4 h = WyspaHash(cell + 17.0);
                    float2 c = (cell + 0.2 + 0.6 * h.xy) * FOG_CELL;
                    c.x += round(sin(T + h.z * 6.2831853) * 1.5);              // kłęby dryfują o piksel, dwa
                    float r = 4.5 + 4.0 * h.z + 0.7 * sin(T * 1.7 + h.w * 6.2831853);   // i lekko „oddychają”
                    float dc = FogDist(c, size);
                    if (dc >= lo && dc <= hi)
                        s = min(s, length(p - c) - r);
                }
                if (band)
                    s = max(s, d - (edge + FOG_REACH));   // twardy limit zasięgu: obrys zamyka się przed krawędzią okna
                return s;
            }

            half4 FogFragment(Varyings input) : SV_Target
            {
                float2 size = _MainTex_TexelSize.zw;
                float2 pix = floor(input.uv * size);
                half4 data = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, (pix + 0.5) * _MainTex_TexelSize.xy, 0);
                bool water = data.g > 0.06;
                float d = data.r * 255.0 / 4.0;      // odległość od brzegu w pikselach (do ok. 64)
                float T = 6.2831853 * frac(_Time.y / _FogPeriod);   // płynny dryf kłębów
                bool band = _FogOuter > 0.0;
                // gęstsza mgła (noc, zgaszone lampy) podchodzi bliżej brzegu; w przezroczystym oknie zaczyna się przy wodzie
                float edge = band ? _FogOuter : _FogFrom / max(0.3, _Density);

                float s = FogSdf(pix, size, band, edge, T);
                float sShadow = FogSdf(pix + float2(2.0, -2.0), size, band, edge, T);   // przy dolno-prawej krawędzi: cień
                float sLight = FogSdf(pix + float2(-1.0, 2.0), size, band, edge, T);    // przy górnej-lewej: jasny brzeg
                bool inside = s < 0.0;
                bool outline = s >= 0.0 && s < 1.2;   // obrys 1 px (kolor ciemniejszy, prośba gracza)
                if (band)
                {
                    inside = inside && d > _FogOuter - 3.0;     // zachodzi 3 px na pas wody, bez przerwy i bez obrysu od wody
                    outline = outline && d > _FogOuter - 1.0;
                }

                half3 col = _ColFo.rgb;
                half a = 0;
                if (inside)
                {
                    a = 0.94;
                    if (sShadow >= 0.0) col = _ColB3.rgb;
                    else if (sLight >= 0.0) col = _ColLight.rgb;
                }
                else if (outline)
                {
                    a = 0.94;
                    col = _ColLine.rgb;
                }
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
