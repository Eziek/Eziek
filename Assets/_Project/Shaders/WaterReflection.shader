// Odbicie w wodzie (GDD §9 p. 114): odwrócona kopia sprite'a (WaterReflections), widoczna tylko nad wodą.
// Wiersze pikseli falują o piksel w bok, kolor przechodzi w barwę wody, a odbicie słabnie z odległością od brzegu
// (u podstawy obiektu wyraźne, czubek ledwo widać). Maska wody: mapa danych wody (globalnie _WyspaWaterData, ustawia
// WaterAnimator): klasa piksela > 0 = woda. _Unlit = 1 dla pikseli emisyjnych (odbicie lampy świeci nocą).
Shader "Wyspa/WaterReflection"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        _Tint("Water tint", Color) = (0.122, 0.188, 0.267, 1)
        _TintAmount("Tint amount", Range(0, 1)) = 0.45
        _Strength("Strength (set by WaterReflections)", Range(0, 1)) = 0.5
        _Unlit("Unlit (glow)", Float) = 0

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

            #pragma vertex ReflVertex
            #pragma fragment ReflFragment

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
                float2 positionWS  : TEXCOORD5;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
            #include "WyspaCommon.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_WyspaWaterData);
            SAMPLER(sampler_WyspaWaterData);
            float4 _WyspaWaterSize;   // szerokość, wysokość mapy danych (globalnie, WaterAnimator)

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_TexelSize;
                half4 _Tint;
                half _TintAmount;
                half _Strength;
                half _Unlit;
            CBUFFER_END

            Varyings ReflVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.positionWS = TransformObjectToWorld(input.positionOS).xy;
                o.uv = input.uv;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 ReflFragment(Varyings input) : SV_Target
            {
                // fala: każdy wiersz pikseli przesunięty o -1..1 piksel, wolno płynie
                float2 uv = input.uv;
                float row = floor(uv.y * _MainTex_TexelSize.w);
                float shift = round(sin(row * 0.9 + _Time.y * 2.6) * 0.7);
                uv.x += shift * _MainTex_TexelSize.x;
                half4 c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0);
                if (uv.x < 0.0 || uv.x > 1.0)
                    c.a = 0;

                // maska wody: piksel mapy danych pod tym miejscem świata (16 px na jednostkę, środek tekstury = 0,0)
                float2 size = max(_WyspaWaterSize.xy, float2(1, 1));
                float2 px = floor(input.positionWS * 16.0 + size * 0.5);
                half4 data = SAMPLE_TEXTURE2D_LOD(_WyspaWaterData, sampler_WyspaWaterData, (px + 0.5) / size, 0);
                half water = data.g > 0.06 ? 1 : 0;

                // u podstawy obiektu wyraźne, ku czubkowi słabnie; pasy co 2 wiersze lekko ciemniejsze (zmarszczki)
                half fade = saturate(1.0 - input.uv.y * 0.9);
                half ripple = fmod(row, 4.0) < 1.0 ? 0.75 : 1.0;
                half a = c.a * water * _Strength * fade * ripple * input.color.a;
                half3 col = lerp(c.rgb * input.color.rgb, _Tint.rgb, _TintAmount);
                if (a <= 0.003)
                    return half4(0, 0, 0, 0);
                if (_Unlit > 0.5)
                    return half4(c.rgb * input.color.rgb, a);
                // światło liczymy z alfą 1 (CombinedShapeLightShared odrzuca alfę 0), alfa na końcu
                half4 lit = WyspaLight(col, 1, input.uv, input.lightingUV);
                return half4(lit.rgb, a);
            }
            ENDHLSL
        }
    }
}
