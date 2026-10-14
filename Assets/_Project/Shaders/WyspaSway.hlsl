#ifndef WYSPA_SWAY_INCLUDED
#define WYSPA_SWAY_INCLUDED
// Kołysanie sprite'ów i kontur (GDD §5.5). Wymaga wcześniej: CBUFFER z polami poniżej oraz TEXTURE2D(_MainTex).
// Przesuwamy całe wiersze pikseli o pełne piksele: 0 u podstawy, najwięcej u góry, skokowo 10 kl./s,
// tak jak w prototypie (tools/pixel-proto/anim.py, sway_fn).

float WyspaStepTime(float fps)
{
    return floor(_Time.y * fps) / fps;
}

float2 WyspaSwayUV(float2 uv)
{
    float h = floor(uv.y * _MainTex_TexelSize.w) * _MainTex_TexelSize.y;   // 0 = dolny wiersz tekstury
    float t = WyspaStepTime(10.0);
    float s = sin(6.2831853 * _SwaySpeed * t + _SwayPhase);
    float shift = round(_SwayAmount * s * h);
    uv.x -= shift * _MainTex_TexelSize.x;
    return uv;
}

half WyspaAlpha(float2 uv)
{
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return 0;
    return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
}

// 1 = pusty piksel, który sąsiaduje (4 kierunki) z pikselem sprite'a
half WyspaOutline(float2 uv)
{
    if (_OutlineColor.a <= 0.0 || WyspaAlpha(uv) > 0.5)
        return 0;
    float2 tx = _MainTex_TexelSize.xy;
    half n = max(max(WyspaAlpha(uv + float2(tx.x, 0)), WyspaAlpha(uv - float2(tx.x, 0))),
                 max(WyspaAlpha(uv + float2(0, tx.y)), WyspaAlpha(uv - float2(0, tx.y))));
    return n > 0.5 ? 1 : 0;
}

#endif
