inline float2 randomVector (float2 UV, float offset)
{
    float2x2 m = float2x2(15.27, 47.63, 99.41, 89.98);
    UV = frac(sin(mul(UV, m)) * 46839.32);
    return float2(sin(UV.y*+offset)*0.5+0.5, cos(UV.x*offset)*0.5+0.5);
}

inline float hash12(float2 p)
{
    float h = dot(p, float2(127.1, 311.7));
    return frac(sin(h) * 43758.5453123);
}

inline float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = hash12(i);
    float b = hash12(i + float2(1.0, 0.0));
    float c = hash12(i + float2(0.0, 1.0));
    float d = hash12(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

inline float fbm4(float2 p)
{
    float v = 0.0;
    float a = 0.5;

    v += a * valueNoise(p);
    p = p * 2.02 + float2(37.1, 61.7);
    a *= 0.5;

    v += a * valueNoise(p);
    p = p * 2.03 + float2(19.3, 11.8);
    a *= 0.5;

    v += a * valueNoise(p);
    p = p * 2.01 + float2(73.2, 49.6);
    a *= 0.5;

    v += a * valueNoise(p);

    return v;
}

inline float2 jitteredCellPoint(int2 cellCoord, float angleOffset)
{
    float2 p = randomVector(cellCoord, angleOffset);
    float2 c = float2(cellCoord);

    float n1 = valueNoise(c * 0.73 + angleOffset * 1.11);
    float n2 = valueNoise(c * 1.21 + 57.0 + angleOffset * 0.87);
    p += (float2(n1, n2) - 0.5) * 0.35;

    return saturate(p);
}

void CustomVoronoi_float(float2 UV, float AngleOffset, float CellDensity, out float DistFromCenter, out float DistFromEdge)
{
    // Warp the Voronoi space with low frequency noise to avoid rigid geometric cells.
    float2 voronoiUV = UV * CellDensity;
    float2 warp = float2(
        fbm4(voronoiUV * 1.35 + AngleOffset * 0.17),
        fbm4(voronoiUV * 1.35 + 19.31 + AngleOffset * 0.13)
    );
    voronoiUV += (warp - 0.5) * 1.10;

    int2 cell = floor(voronoiUV);
    float2 posInCell = frac(voronoiUV);

    DistFromCenter = 8.0f;
    float2 closestOffset;

    for(int y = -1; y <= 1; ++y)
    {
        for(int x = -1; x <= 1; ++x)
        {
            int2 cellToCheck = int2(x, y);
            float2 cellOffset = float2(cellToCheck) - posInCell + jitteredCellPoint(cell + cellToCheck, AngleOffset);

            float distToPoint = dot(cellOffset, cellOffset);

            if(distToPoint < DistFromCenter)
            {
                DistFromCenter = distToPoint;
                closestOffset = cellOffset;
            }
        }
    }

    DistFromEdge = 8.0f;

    for(int y = -1; y <= 1; ++y)
    {
        for(int x = -1; x <= 1; ++x)
        {
            int2 cellToCheck = int2(x, y);
            float2 cellOffset = float2(cellToCheck) - posInCell + jitteredCellPoint(cell + cellToCheck, AngleOffset);

            float2 edgeDir = cellOffset - closestOffset;
            float edgeDirLenSq = dot(edgeDir, edgeDir);
            if(edgeDirLenSq < 1e-6)
            {
                continue;
            }

            float distToEdge = dot(0.5f * (closestOffset + cellOffset), edgeDir * rsqrt(edgeDirLenSq));

            DistFromEdge = min(DistFromEdge, distToEdge);
        }
    }
}
