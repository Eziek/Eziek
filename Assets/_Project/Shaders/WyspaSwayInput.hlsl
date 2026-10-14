#ifndef WYSPA_SWAY_INPUT_INCLUDED
#define WYSPA_SWAY_INPUT_INCLUDED
// Ten sam układ we wszystkich przebiegach (wymóg SRP Batchera).
CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    float4 _MainTex_TexelSize;
    float _SwayAmount;
    float _SwaySpeed;
    float _SwayPhase;
    half4 _OutlineColor;
CBUFFER_END
#endif
