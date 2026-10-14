// Wersja bez oświetlenia (piksele emisyjne *_glow): kołysze się w takt rośliny, na której leży.
Shader "Wyspa/SpriteSwayUnlit"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        _SwayAmount("Sway amount (px at top)", Float) = 1
        _SwaySpeed("Sway speed (cycles per second)", Float) = 0.42
        _SwayPhase("Sway phase (set by SpriteFx)", Float) = 0
        [HideInInspector] _SwayX("World X in px (set by SpriteFx)", Float) = 0
        _OutlineColor("Outline (set by SpriteFx)", Color) = (0,0,0,0)
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "WyspaSwayInput.hlsl"
            #include "WyspaSway.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                input.uv = WyspaSwayUV(input.uv);
                half inside = WyspaAlpha(input.uv) > 0.0 ? 1 : 0;
                return CommonUnlitFragment(input, input.color) * inside;
            }
            ENDHLSL
        }
    }
}
