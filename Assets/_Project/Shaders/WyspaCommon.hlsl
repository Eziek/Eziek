#ifndef WYSPA_COMMON_INCLUDED
#define WYSPA_COMMON_INCLUDED
// Wspólne funkcje shaderów wyspy: czas skokowy, hash, dithering Bayera, oświetlenie 2D dla koloru liczonego w shaderze.

static const float WYSPA_BAYER[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

// Klatka animacji: 10 kl./s jak w prototypie (anim.py)
float WyspaFrame()
{
    return floor(_Time.y * 10.0);
}

float WyspaBayer(int x, int y)
{
    return WYSPA_BAYER[(y & 3) * 4 + (x & 3)] / 16.0;
}

// hash 2D -> 4 liczby 0..1 (Dave Hoskins, "Hash without Sine")
float4 WyspaHash(float2 p)
{
    float4 p4 = frac(float4(p.xyxy) * float4(0.1031, 0.1030, 0.0973, 0.1099));
    p4 += dot(p4, p4.wzxy + 33.33);
    return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

// Kolor policzony w shaderze oświetlony światłami 2D (jak CommonLitFragment, bez tekstur maski i normalnych)
half4 WyspaLight(half3 albedo, half alpha, float2 uv, half2 lightingUV)
{
    SurfaceData2D surfaceData;
    InputData2D inputData;
    InitializeSurfaceData(albedo, alpha, half4(1, 1, 1, 1), surfaceData);
    InitializeInputData(uv, lightingUV, inputData);
    return CombinedShapeLightShared(surfaceData, inputData);
}

#endif
