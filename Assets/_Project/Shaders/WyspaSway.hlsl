#ifndef WYSPA_SWAY_INCLUDED
#define WYSPA_SWAY_INCLUDED
// Kołysanie sprite'ów i kontur (GDD §5.5). Wymaga wcześniej: CBUFFER z polami poniżej oraz TEXTURE2D(_MainTex).
// Przesuwamy całe wiersze pikseli o pełne piksele: 0 u podstawy, najwięcej u góry (jak w prototypie, anim.py, sway_fn).
// Czas płynny (prośba gracza): wiersze przeskakują dokładnie wtedy, gdy wypada im piksel, a nie co 0.1 s.

float WyspaStepTime(float fps)
{
    return floor(_Time.y * fps) / fps;
}

// Podmuch wiatru (globalnie, ustawia IslandFx razem ze smugami): czoło podmuchu przechodzi przez wyspę z lewej na prawą,
// więc rośliny pochylają się po kolei, falą, a nie wszystkie naraz (prośba gracza: ładniejszy wiatr).
float _WyspaWindFront;   // piksele świata: gdzie jest teraz czoło podmuchu
float _WyspaWindPower;   // 0..1 siła bieżącego podmuchu (0 = bez wiatru)
#define WYSPA_WIND_WIDTH 40.0

// Pochylenie w miejscu x: zero przed czołem, szybkie pochylenie, gdy czoło przechodzi, potem sprężysty powrót
// z małym odbiciem w drugą stronę (tłumione drganie: exp * sin, maksimum ok. 1 przy u = 0.36).
float WyspaGust(float x)
{
    float u = (_WyspaWindFront - x) / WYSPA_WIND_WIDTH;
    if (u <= 0.0 || _WyspaWindPower <= 0.0)
        return 0;
    return _WyspaWindPower * 2.0 * exp(-1.6 * u) * sin(3.0 * u);
}

float2 WyspaSwayUV(float2 uv)
{
    float h = floor(uv.y * _MainTex_TexelSize.w) * _MainTex_TexelSize.y;   // 0 = dolny wiersz tekstury
    float bend = h * sqrt(h);   // sztywna podstawa, giętki czubek (łodyga się zgina, a nie przechyla jak deska)
    float t = _Time.y;
    float s = sin(6.2831853 * _SwaySpeed * t + _SwayPhase);
    float g = WyspaGust(_SwayX);
    float flutter = abs(g) * 0.4 * sin(t * 11.0 + _SwayPhase * 3.0);   // drżenie liści, póki wieje
    float shift = round(_SwayAmount * (s * (1.0 + 0.5 * abs(g)) + 2.2 * g + flutter) * bend);
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
