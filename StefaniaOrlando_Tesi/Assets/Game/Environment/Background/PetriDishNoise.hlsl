#ifndef PETRI_DISH_NOISE_INCLUDED
#define PETRI_DISH_NOISE_INCLUDED

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p)
{
    float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float fbm(float2 p, int octaves)
{
    float v = 0.0;
    float a = 0.5;
    float2 shift = float2(100.0, 100.0);
    for (int i = 0; i < octaves; i++)
    {
        v += a * valueNoise(p);
        p = p * 2.0 + shift;
        a *= 0.5;
    }
    return v;
}

// Voronoi with point jitter animated by time.
// Returns: x = F1 (nearest dist), y = F2 (second-nearest dist),
//          z = nearest-cell hash (stable per-cell ID in [0,1]).
float3 voronoi(float2 uv, float jitter, float t)
{
    float2 ip = floor(uv);
    float2 fp = frac(uv);

    float F1 = 8.0;
    float F2 = 8.0;
    float2 nearestCell = ip;

    [unroll]
    for (int j = -1; j <= 1; j++)
    {
        [unroll]
        for (int i = -1; i <= 1; i++)
        {
            float2 cell = float2(i, j);
            float2 h = hash22(ip + cell);
            // slow per-cell drift — sin oscillates each point inside its cell
            float2 offset = 0.5 + 0.5 * sin(t + 6.2831 * h);
            offset = lerp(float2(0.5, 0.5), offset, jitter);
            float2 r = cell + offset - fp;
            float d = dot(r, r);
            if (d < F1)
            {
                F2 = F1;
                F1 = d;
                nearestCell = ip + cell;
            }
            else if (d < F2)
            {
                F2 = d;
            }
        }
    }

    return float3(sqrt(F1), sqrt(F2), hash21(nearestCell));
}

#endif
