#ifndef WYSPA_SWAY_INPUT_INCLUDED
#define WYSPA_SWAY_INPUT_INCLUDED
// Ten sam układ we wszystkich przebiegach (wymóg SRP Batchera).
CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    float4 _MainTex_TexelSize;
    float _SwayAmount;
    float _SwaySpeed;
    float _SwayPhase;
    float _SwayX;          // położenie sprite'a na świecie (piksele) - podmuch dociera do niego w swoim czasie
    float _SwayY;          // podstawa sprite'a na świecie (piksele, y w górę) - kursor odgina rośliny przy sobie
    half4 _OutlineColor;
CBUFFER_END
#endif
