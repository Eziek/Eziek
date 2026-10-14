#ifndef WYSPA_FLOAT_INPUT_INCLUDED
#define WYSPA_FLOAT_INPUT_INCLUDED
// Ten sam układ we wszystkich przebiegach SpriteFloat.shader (wymóg SRP Batchera).
CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    float4 _MainTex_TexelSize;
    float _Waterline;
    float _BobAmount;
    float _BobSpeed;
    float _BobPhase;
    half4 _WaterTint;
    half4 _Foam;
CBUFFER_END
#endif
