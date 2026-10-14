// Woda (GDD §4.5) liczona w shaderze z mapy danych (Ground/water_data.png, opis w tools/pixel-proto/export_unity_fx.py):
// głębia, ryby pod wodą (ósemki), coś dużego w głębinie, połyski, grzbiety fal, przybój, pulsująca piana.
// Port prototypu tools/pixel-proto/water.py (10 kl./s). Ryby i potwora ustawia WaterAnimator (tablice z C#).
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
            CBUFFER_END

            // Tablice ustawia WaterAnimator (SetVectorArray). Współrzędne w pikselach kadru, y w dół (jak w Pythonie).
            float4 _Fish[16];       // cx, cy, rx, ry
            float4 _FishB[16];      // faza (rad), świeci (0/1)
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

            half4 WaterFragment(Varyings input) : SV_Target
            {
                float2 size = _DataTex_TexelSize.zw;
                float2 pix = floor(input.uv * size);
                half4 data = SAMPLE_TEXTURE2D_LOD(_DataTex, sampler_DataTex, (pix + 0.5) * _DataTex_TexelSize.xy, 0);
                int cls = (int)round(data.g * 255.0 / 32.0);
                float d = data.r * 255.0 / 4.0;
                int x = (int)pix.x;
                int y = (int)(size.y - 1.0 - pix.y);
                float f = WyspaFrame();
                float t = f / 10.0;
                int baseCls = cls >= 1 && cls <= 4 ? cls : 1;
                half3 col = BaseColor(baseCls);
                bool emit = false;
                half3 emitCol = _ColGlow.rgb;
                bool night = _Night > 0.5;

                // --- ryby ---
                int count = (int)_FishCount;
                [loop] for (int i = 0; i < 16; i++)
                {
                    if (i >= count) break;
                    float4 p = _Fish[i];
                    float4 q = _FishB[i];
                    float a = 6.2831853 * frac(t / _FishPeriod) + q.x;
                    int fx = (int)round(p.x + p.z * sin(a));
                    int fy = (int)round(p.y + p.w * sin(2.0 * a));
                    bool right = cos(a) > 0.0;
                    int lx = x - fx;
                    int ly = y - fy;
                    if (lx >= 0 && lx < 10 && ly >= 0 && ly < 4 && cls != 5)
                    {
                        int k = right ? lx : 9 - lx;
                        int shape = (((int)f + i) % 4) < 2 ? 0 : 1;
                        if (((FishRow(shape, ly) >> (uint)k) & 1u) != 0u)
                            col = DarkerColor(baseCls);
                    }
                    if (q.y > 0.5 && night && x == fx + (right ? 8 : 1) && y == fy + 2)
                        emit = true;
                }

                // --- coś dużego w głębinie: długie, falujące cielsko ---
                if (_Beast.z > 0.5)
                {
                    float ab = 6.2831853 * frac(t / _BeastPeriod);
                    int ox = (int)round(_Beast.x + 7.0 * cos(ab));
                    int oy = (int)round(_Beast.y + 2.0 * sin(ab));
                    int s = x - (ox - 20);
                    if (s >= 0 && s < 40 && (baseCls == 2 || baseCls == 3 || baseCls == 4) && cls <= 4)
                    {
                        float ts = s / 39.0;
                        float th = 4.2 * sin(3.14159265 * min(1.0, ts * 1.6)) * (1.0 - ts * 0.6);
                        float yy = oy + 2.5 * sin(s * 0.35 - f * 6.2831853 / 24.0);
                        if (abs(y - round(yy)) <= floor(th))
                            col = _ColO.rgb;
                    }
                    // oko błyska tylko w nocy, rzadko
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

                // --- połyski: jeden na komórkę 24x10 (część komórek), dryfują w lewo ---
                [unroll] for (int gc = 0; gc < 2; gc++)
                {
                    float2 cell = float2(floor(x / 24.0) + gc, floor(y / 10.0));
                    float4 h = WyspaHash(cell + 17.0);
                    if (h.x < 0.2)
                    {
                        int gx = (int)(cell.x * 24.0 + floor(h.y * 24.0));
                        int gy = (int)(cell.y * 10.0 + floor(h.z * 10.0));
                        float tt = fmod(f + floor(h.w * 48.0), 48.0);
                        int xx = gx - (int)floor(tt / 3.0);
                        int len = tt < 6.0 ? 2 : 1;
                        if (tt < 10.0 && y == gy && x >= xx && x < xx + len && d > 9.0)
                            col = baseCls == 1 ? _ColB3.rgb : _ColB2.rgb;
                    }
                }

                // --- grzbiety fal: narasta, szczyt, łamie się w pianę ---
                [unroll] for (int cc = 0; cc < 2; cc++)
                {
                    float2 cell = float2(floor(x / 40.0) + cc, floor(y / 20.0));
                    float4 h = WyspaHash(cell + 51.7);
                    if (h.x < 0.35)
                    {
                        int x0 = (int)(cell.x * 40.0 + floor(h.y * 40.0));
                        int y0 = (int)(cell.y * 20.0 + floor(h.z * 19.0));
                        float tt = fmod(f + floor(h.w * 48.0), 48.0);
                        int ci = x - (x0 - (int)floor(tt / 3.0));
                        int cj = y - y0;
                        if (tt >= 6.0 && tt < 42.0 && ci >= 0 && ci < 8 && cj >= 0 && cj < 2 && d > 9.0)
                        {
                            uint mask = 0u;
                            half3 cc2 = _ColB2.rgb;
                            if (tt < 16.0)      { mask = cj == 1 ? 0x3Cu : 0u;  cc2 = _ColB2.rgb; }
                            else if (tt < 30.0) { mask = cj == 0 ? 0x3Cu : 0x42u; cc2 = cj == 0 ? _ColB3.rgb : _ColB2.rgb; }
                            else                { mask = cj == 0 ? 0x4Au : 0x3Cu; cc2 = cj == 0 ? _ColFo.rgb : _ColB3.rgb; }
                            if (((mask >> (uint)ci) & 1u) != 0u)
                                col = cc2;
                        }
                    }
                }

                // --- przybój: dwie fale napływają do brzegu ---
                [unroll] for (int w = 0; w < 2; w++)
                {
                    float k = 9.0 - fmod(f + w * 24.0, 24.0) / 24.0 * 7.0;
                    if (d >= k && d < k + 1.0 && ((x + y) & 1) == 0)
                        col = _ColB3.rgb;
                }

                // --- cień klifu i pulsująca piana ---
                bool pulse = fmod(floor(f / 5.0), 2.0) == 0.0;
                if (cls == 5) col = _ColB0.rgb;
                if (cls == 6) col = pulse ? _ColFo.rgb : _ColB3.rgb;
                if (cls == 7) col = pulse ? _ColB2.rgb : _ColFo.rgb;
                if (cls >= 5) emit = false;

                half alpha = cls > 0 ? input.color.a : 0;
                half4 lit = WyspaLight(col, alpha, input.uv, input.lightingUV);
                return emit ? half4(emitCol, alpha) : lit;
            }
            ENDHLSL
        }
    }
}
