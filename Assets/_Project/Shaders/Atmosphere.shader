// Atmosfera nad wyspą (GDD §9 p. 120): jeden sprite wielkości obrazu wyspy (tekstura lądu, Visual/Atmosphere), nad lądem
// i obiektami, pod mgłą. Rysuje tylko tam, gdzie widać ląd albo wodę (w przezroczystym oknie nie brudzi pulpitu):
// - cienie chmur: w dzień duże, miękkie (dithering) plamy cienia suną powoli po lądzie i wodzie,
// - poranna mgiełka: o świcie nad wodą niskie, poziome smugi bladej mgły, dryfują i rzedną do 8:00,
// - rosa: o świcie na lądzie pojedyncze piksele migoczą jak krople.
// Siły ustawia WaterAnimator (globalnie): _WyspaSunlight (słońce), _WyspaMorningMist (świt), _WyspaCloudShadow (pogoda).
Shader "Wyspa/Atmosphere"
{
    Properties
    {
        _MainTex("Land (island picture)", 2D) = "white" {}
        _ShadowColor("Cloud shadow", Color) = (0.059, 0.039, 0.063, 1)
        _ShadowAlpha("Cloud shadow strength", Range(0, 1)) = 0.17
        _MistColor("Morning mist", Color) = (0.518, 0.557, 0.608, 1)
        _MistAlpha("Morning mist strength", Range(0, 1)) = 0.4
        _DewColor("Dew", Color) = (0.824, 0.886, 0.902, 1)

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

            #pragma vertex AtmoVertex
            #pragma fragment AtmoFragment

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
            TEXTURE2D(_WyspaWaterData);
            SAMPLER(sampler_WyspaWaterData);
            float4 _WyspaWaterSize;     // rozmiar mapy danych (WaterAnimator)
            float _WyspaShoreRange;     // przezroczyste okno: woda tylko do tylu pikseli od brzegu (0 = wszędzie)
            float _WyspaSunlight;       // 0..1
            float _WyspaCloudShadow;    // 0..1: dzień bez deszczu
            float _WyspaMorningMist;    // 0..1: świt

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_TexelSize;
                half4 _ShadowColor;
                half _ShadowAlpha;
                half4 _MistColor;
                half _MistAlpha;
                half4 _DewColor;
            CBUFFER_END

            Varyings AtmoVertex(Attributes input)
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

            float ANoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = WyspaHash(i).x, b = WyspaHash(i + float2(1, 0)).x;
                float c = WyspaHash(i + float2(0, 1)).x, e = WyspaHash(i + float2(1, 1)).x;
                return lerp(lerp(a, b, f.x), lerp(c, e, f.x), f.y);
            }

            half4 AtmoFragment(Varyings input) : SV_Target
            {
                float2 size = max(_WyspaWaterSize.xy, float2(1, 1));
                float2 px = floor(input.uv * size);                 // piksele obrazu wyspy (16 na kafelek), y w górę
                half landA = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv, 0).a;
                half4 data = SAMPLE_TEXTURE2D_LOD(_WyspaWaterData, sampler_WyspaWaterData, (px + 0.5) / size, 0);
                float cls = round(data.g * 255.0 / 32.0);
                float d = data.r * 255.0 / 4.0;
                bool land = landA > 0.5;
                bool water = !land && cls >= 1.0 && (_WyspaShoreRange <= 0.0 || d <= _WyspaShoreRange);
                if (!land && !water)
                    return half4(0, 0, 0, 0);

                float t = _Time.y;
                float th = WyspaBayer((int)px.x, (int)px.y);

                // poranna mgiełka nad wodą: smugi rozciągnięte w poziomie, wolno dryfują, gęstsze przy brzegu
                if (water && _WyspaMorningMist > 0.01 && cls <= 4.0)
                {
                    float m = ANoise(float2((px.x + t * 3.0) / 26.0, px.y / 3.5) + 5.0) * 0.7
                            + ANoise(float2((px.x - t * 1.5) / 11.0, px.y / 2.0) + 40.0) * 0.3;
                    float near = saturate(1.0 - d / 40.0);
                    float k = (m - 0.5) * 4.0 * _WyspaMorningMist * (0.4 + 0.6 * near);
                    if (k > th)
                    {
                        half4 lit = WyspaLight(_MistColor.rgb, 1, input.uv, input.lightingUV);
                        return half4(lit.rgb, _MistAlpha * saturate(k) * input.color.a);
                    }
                }

                // rosa: o świcie pojedyncze piksele trawy migoczą (krótko, w losowych miejscach)
                if (land && _WyspaMorningMist > 0.05)
                {
                    float4 h = WyspaHash(px * 1.13 + floor(t * 2.0 + WyspaHash(px).y * 2.0) * 3.7);
                    if (h.x < 0.004 * _WyspaMorningMist)
                        return half4(_DewColor.rgb, 0.7 * _WyspaMorningMist * input.color.a);
                }

                // cienie chmur: duże plamy (dwie skale szumu), krawędź w ditheringu; suną z wiatrem w prawo
                float shade = _WyspaSunlight * _WyspaCloudShadow;
                if (shade > 0.01)
                {
                    float2 wp = px + float2(-t * 2.2, t * 0.6);
                    float c = ANoise(wp / 90.0) * 0.7 + ANoise(wp / 34.0 + 13.0) * 0.3;
                    float edge = saturate((c - 0.56) / 0.06);
                    if (edge > th)
                        return half4(_ShadowColor.rgb, _ShadowAlpha * shade * input.color.a);
                }
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
