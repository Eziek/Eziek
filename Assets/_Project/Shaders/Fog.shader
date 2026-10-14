// Mgła (GDD §4.6, §13): tylko nad morzem, gęstnieje z odległością od lądu, pasma dryfują, dithering Bayera.
// Rozbudowa wyspy odsuwa ją sama (mapa odległości liczona od nowego brzegu). Port tools/pixel-proto/tiles_proto.py (render).
// Sprite = mapa danych wody malowana przez Island (R = odległość x4, G > 0 = woda). Gęstość ustawia FogController.
Shader "Wyspa/Fog"
{
    Properties
    {
        _MainTex("Water data", 2D) = "black" {}
        _ColB3("b3", Color) = (0.337, 0.412, 0.478, 1)
        _ColFo("fo", Color) = (0.518, 0.557, 0.608, 1)
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
                half4 _ColB3, _ColFo;
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

            half4 FogFragment(Varyings input) : SV_Target
            {
                float2 size = _MainTex_TexelSize.zw;
                float2 pix = floor(input.uv * size);
                half4 data = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, (pix + 0.5) * _MainTex_TexelSize.xy, 0);
                bool water = data.g > 0.06;
                int x = (int)pix.x;
                int y = (int)(size.y - 1.0 - pix.y);
                float d = data.r * 255.0 / 4.0;      // odległość od brzegu w pikselach (do ok. 64)
                float T = 6.2831853 * frac(WyspaFrame() / 10.0 / _FogPeriod);
                // gęstsza mgła (noc, zgaszone lampy) zaczyna się bliżej brzegu
                float from = _FogFrom / max(0.3, _Density);
                float v = (d - from) / max(1.0, _FogSpan)
                        + 0.18 * sin(x / 9.0 + T) * sin(y / 7.0)
                        + 0.08 * sin(6.2831853 * (x / 45.0 + y / 90.0) - 2.0 * T);
                float th = WyspaBayer(x, y);

                half3 col = _ColB3.rgb;
                half a = 0;
                if (v > 0.85) { col = _ColFo.rgb; a = 1; }
                else if (v > 0.55) { col = th < (v - 0.55) / 0.3 ? _ColFo.rgb : _ColB3.rgb; a = 1; }
                else if (v > 0.25 && th < (v - 0.25) / 0.3) { col = _ColB3.rgb; a = 1; }
                // przezroczyste okno (Etap 6a): mgła tylko nad pasem wody (nie nad pulpitem). Od _FogFrom gęstnieje
                // ku krawędzi pasa (_FogOuter = _ShoreRange wody), dryfuje; krawędź w tę samą szachownicę co woda.
                if (_FogOuter > 0.0)
                {
                    float drift = 0.25 * sin(x / 9.0 + T) * sin(y / 7.0) + 0.15 * sin(6.2831853 * (x / 45.0 + y / 90.0) - 2.0 * T);
                    float c = saturate((d - _FogFrom) / max(1.0, _FogOuter - _FogFrom) + drift) * 0.6 * clamp(_Density, 0.7, 1.3);
                    a = th < c ? 1 : 0;
                    col = th < c * 0.35 ? _ColFo.rgb : _ColB3.rgb;
                    if (d > _FogOuter || (d > _FogOuter - 2.0 && ((x + y) & 1) == 1))
                        a = 0;
                }
                if (!water) a = 0;

                // światło liczymy zawsze z alfą 1 (CombinedShapeLightShared odrzuca alfę 0), alfę ustawiamy na końcu
                half4 lit = WyspaLight(col, 1, input.uv, input.lightingUV);
                return half4(lit.rgb, a * input.color.a);
            }
            ENDHLSL
        }
    }
}
