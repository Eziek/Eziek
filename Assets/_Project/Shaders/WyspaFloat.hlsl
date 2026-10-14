#ifndef WYSPA_FLOAT_INCLUDED
#define WYSPA_FLOAT_INCLUDED
// Kolor sprite'a na wodzie (SpriteFloat.shader): część pod linią wody, piana przy burtach.
// Wymaga _MainTex/sampler_MainTex (Core2D / 2DCommon) i pól z WYSPA_FLOAT_CBUFFER.

float WyspaFloatHash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

half4 WyspaFloatColor(float2 uv, half4 tint)
{
    float2 size = _MainTex_TexelSize.zw;
    float2 t = floor(uv * size);                                     // texel, y od dołu
    // próbki bez gradientów w rozgałęzieniach (FXC): piksel i sąsiedzi w poziomie do piany przy burtach
    half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * tint;
    half aL = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(_MainTex_TexelSize.x, 0)).a;
    half aR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(_MainTex_TexelSize.x, 0)).a;

    // linia wody kołysze się o cały piksel (pixel art, bez rozmycia)
    float wl = _Waterline + round(sin(_Time.y * _BobSpeed * 6.2831853 + _BobPhase) * _BobAmount);
    float depth = wl - t.y;                                          // > 0 = pod wodą (1 = tuż pod linią)
    float flicker = WyspaFloatHash(float2(t.x, floor(_Time.y * 5.0 + _BobPhase * 3.0)));
    half4 foam = half4(_Foam.rgb, tint.a);

    if (depth > 0.0)
    {
        // pod wodą: kolor wody, półprzezroczyste, głębiej niewidoczne (kadłub zanurzony)
        c.rgb = lerp(c.rgb, _WaterTint.rgb, saturate(0.5 + depth * 0.15));
        c.a *= depth <= 2.0 ? 0.7 : 0.0;
        if (depth <= 1.0 && c.a > 0.0 && flicker > 0.55)
            c = foam;                                                // piana na linii wody
    }
    // piana przy burtach: przezroczysty piksel na linii wody obok kadłuba
    if (c.a <= 0.0 && (aL > 0.0 || aR > 0.0) && depth > -1.0 && depth <= 1.0 && flicker > 0.35)
        c = foam;
    return c;
}
#endif
