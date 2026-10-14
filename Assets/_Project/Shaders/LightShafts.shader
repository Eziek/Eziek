// Światło „wolumetryczne” w pikselach (prośba gracza, Etap 6c+): ukośne snopy słońca w dzień, blade promienie księżyca w nocy
// i aureole wokół zapalonych lamp, jakby świeciły w mgle. Dithering Bayera, mieszanie addytywne, bez oświetlenia 2D.
// Sprite = mapa danych wody (jak mgła): R = odległość od brzegu x4, G > 0 = woda. Lampy i kolory ustawia LightShafts.cs.
Shader "Wyspa/LightShafts"
{
    Properties
    {
        _MainTex("Water data", 2D) = "black" {}
        _ShaftColor("Shaft color (set by LightShafts)", Color) = (0.12, 0.10, 0.06, 1)
        _ShaftStrength("Shaft strength 0..1", Range(0, 1)) = 0.5
        _ShaftAngle("Shaft angle (rad, 0 = down)", Float) = 0.5
        _MaskRange("Transparent window: only up to (px from shore, 0 = everywhere)", Float) = 0
        _HaloStrength("Lamp halo strength 0..1", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        // addytywnie; alfa wyjścia 0, więc w przezroczystym oknie światło dodaje się też do pulpitu (alfa premnożona)
        Blend One One
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex ShaftVertex
            #pragma fragment ShaftFragment

            #define WYSPA_NO_LIGHT   // bez oświetlenia 2D (shader nieoświetlony)
            #include "WyspaCommon.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                half4 _ShaftColor;
                float _ShaftStrength;
                float _ShaftAngle;
                float _MaskRange;
                float _HaloStrength;
            CBUFFER_END

            // Lampy ustawia LightShafts (SetVectorArray). Pozycja w pikselach mapy danych (y w dół), promień w pikselach.
            float4 _Lamps[16];        // x, y, promień, siła 0..1
            float4 _LampColors[16];   // kolor światła lampy
            float _LampCount;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings ShaftVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                return o;
            }

            half4 ShaftFragment(Varyings input) : SV_Target
            {
                float2 size = _MainTex_TexelSize.zw;
                float2 pix = floor(input.uv * size);
                half4 data = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, (pix + 0.5) * _MainTex_TexelSize.xy, 0);
                bool water = data.g > 0.06;
                float d = data.r * 255.0 / 4.0;
                int x = (int)pix.x;
                int y = (int)(size.y - 1.0 - pix.y);
                // przezroczyste okno: nic poza pasem wody (nad pulpitem)
                if (_MaskRange > 0.0 && water && d > _MaskRange)
                    return half4(0, 0, 0, 0);

                float T = WyspaFrame() / 10.0;
                float th = WyspaBayer(x, y) + 1.0 / 32.0;   // próg > 0: przy zerowej sile żaden piksel się nie zapala
                half3 col = 0;

                // --- snopy: wąskie ukośne pasma, powoli dryfują i oddychają ---
                float2 dir = float2(sin(_ShaftAngle), cos(_ShaftAngle));
                float across = x * dir.y - y * dir.x;
                float along = x * dir.x + y * dir.y;
                float b1 = 0.5 + 0.5 * sin(across / 9.0 + T * 0.12);
                float b2 = 0.5 + 0.5 * sin(across / 23.0 - T * 0.05 + 1.7);
                float beam = pow(saturate(b1 * b2), 3.0);
                float breathe = 0.65 + 0.35 * sin(along / 50.0 + T * 0.2);
                float shaft = beam * breathe * _ShaftStrength;
                if (th < shaft * 0.8)
                    col += _ShaftColor.rgb;

                // --- aureole lamp: dithered poświata w mgle, mocniejsza bliżej lampy ---
                int count = (int)_LampCount;
                [loop] for (int i = 0; i < 16; i++)
                {
                    if (i >= count) break;
                    float4 l = _Lamps[i];
                    float dist = length(float2(x, y) - l.xy);
                    float h = saturate(1.0 - dist / max(1.0, l.z));
                    h = h * h * l.w * _HaloStrength;
                    if (th < h * 0.7)
                        col += _LampColors[i].rgb * 0.18;
                }
                return half4(col, 0);
            }
            ENDHLSL
        }
    }
}
