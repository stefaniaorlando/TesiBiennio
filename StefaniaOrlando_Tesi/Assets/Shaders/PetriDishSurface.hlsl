#ifndef PETRI_DISH_SURFACE_INCLUDED
#define PETRI_DISH_SURFACE_INCLUDED

#include "PetriDishNoise.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _ChannelColor;
    float4 _AccentColor;
    float4 _DeepColor;
    float  _AccentAmount;

    float  _CellScale;
    float  _CellJitter;
    float  _CellAnimSpeed;

    float  _WarpStrength;
    float  _WarpScale;
    float  _WarpSpeed;
    float  _FlowTurbScale;
    float  _FlowTurbAmount;

    float  _ChannelWidth;
    float  _ChannelSoftness;
    float  _ChannelIntensity;
    float  _JunctionBoost;

    float  _PulseSpeed;
    float  _PulseFreq;
    float  _PulseStrength;

    float  _GrainScale;
    float  _GrainIntensity;

    float  _GlobalIntensity;
    float  _TimeScale;
CBUFFER_END

// Computes the procedural petri-dish surface color at a world-space XY point.
// Both the unlit and lit shaders call this so the visual signature stays in one place.
half3 ComputePetriDishColor(float2 uvWorld, float t)
{
    // 1. Warp the sampling UV. Two layers, mirroring FlowField.GetFlowAtPosition:
    //    - base layer: spatial freq _WarpScale, time freq _WarpSpeed, strength _WarpStrength
    //    - turbulence: spatial freq _FlowTurbScale, ~2x time freq, weight _FlowTurbAmount
    //    With sync OFF, _FlowTurbAmount = 0 collapses this to the original single layer.
    float2 baseUV = uvWorld * _WarpScale + float2(t * _WarpSpeed, t * _WarpSpeed * 0.7);
    float2 warp   = float2(
        fbm(baseUV,                       3),
        fbm(baseUV + float2(31.7, 19.3), 3)
    ) - 0.5;

    if (_FlowTurbAmount > 0.0)
    {
        float2 turbUV = uvWorld * _FlowTurbScale + float2(t * _WarpSpeed * 2.0, t * _WarpSpeed * 1.4);
        float2 turb   = float2(
            fbm(turbUV,                       2),
            fbm(turbUV + float2(47.2, 23.7), 2)
        ) - 0.5;
        warp += turb * _FlowTurbAmount;
    }

    float2 cellUV = uvWorld * _CellScale + warp * _WarpStrength;

    // 2. Voronoi: F1, F2, cell ID.
    float3 v   = voronoi(cellUV, _CellJitter, t * _CellAnimSpeed * 6.2831);
    float  F1  = v.x;
    float  F2  = v.y;
    float  cid = v.z;

    // 3. Channel mask = thin band where F2 - F1 is near zero.
    float edge       = F2 - F1;
    float channel    = 1.0 - smoothstep(_ChannelWidth, _ChannelWidth + _ChannelSoftness, edge);

    // 4. Junction boost — F1 is also small at 3-cell meeting points, doubles the effect there.
    float junction   = (1.0 - smoothstep(_ChannelWidth * 0.5, _ChannelWidth * 1.5, edge))
                     * (1.0 - smoothstep(_ChannelWidth * 1.5, _ChannelWidth * 3.0, F1));
    channel         += junction * _JunctionBoost;

    // 5. Pulse rolling along channels.
    float pulse      = 0.5 + 0.5 * sin(t * _PulseSpeed - F1 * _PulseFreq + cid * 6.2831);
    float pulsedCh   = channel * lerp(1.0 - _PulseStrength, 1.0 + _PulseStrength, pulse);

    // 6. Cell fill — bright at center (low F1), dimmer toward edges.
    float fill       = 1.0 - saturate(F1 / 0.7);
    fill            *= lerp(0.7, 1.1, cid); // per-cell brightness variation

    // 7. Accent pockets — only some cells get the warm tint.
    float accentMask = step(1.0 - _AccentAmount, cid);

    // 8. Particulate grain.
    float grain      = (fbm(uvWorld * _GrainScale, 2) - 0.5) * 2.0 * _GrainIntensity;

    // ---- Compose color ----
    half3 baseCol    = lerp(_DeepColor.rgb, _BaseColor.rgb, fill);
    baseCol          = lerp(baseCol, _AccentColor.rgb, accentMask * fill * 0.6);
    half3 col        = baseCol + _ChannelColor.rgb * pulsedCh * _ChannelIntensity;
    col             += grain;
    col             *= _GlobalIntensity;
    return col;
}

// Channel-mask "height field" used by LitBumped's NormalsRendering pass.
// Mirrors steps 1-4 of ComputePetriDishColor (warp + voronoi + channel + junction boost).
// Channels and 3-cell junctions are peaks; cell interiors are zero. ddx/ddy of this
// gives a tangent-space normal that makes membranes catch 2D lights as raised veins.
float ComputePetriDishHeight(float2 uvWorld, float t)
{
    float2 baseUV = uvWorld * _WarpScale + float2(t * _WarpSpeed, t * _WarpSpeed * 0.7);
    float2 warp   = float2(
        fbm(baseUV,                       3),
        fbm(baseUV + float2(31.7, 19.3), 3)
    ) - 0.5;

    if (_FlowTurbAmount > 0.0)
    {
        float2 turbUV = uvWorld * _FlowTurbScale + float2(t * _WarpSpeed * 2.0, t * _WarpSpeed * 1.4);
        float2 turb   = float2(
            fbm(turbUV,                       2),
            fbm(turbUV + float2(47.2, 23.7), 2)
        ) - 0.5;
        warp += turb * _FlowTurbAmount;
    }

    float2 cellUV = uvWorld * _CellScale + warp * _WarpStrength;

    float3 v  = voronoi(cellUV, _CellJitter, t * _CellAnimSpeed * 6.2831);
    float  F1 = v.x;
    float  F2 = v.y;

    float edge     = F2 - F1;
    float channel  = 1.0 - smoothstep(_ChannelWidth, _ChannelWidth + _ChannelSoftness, edge);
    float junction = (1.0 - smoothstep(_ChannelWidth * 0.5, _ChannelWidth * 1.5, edge))
                   * (1.0 - smoothstep(_ChannelWidth * 1.5, _ChannelWidth * 3.0, F1));
    channel       += junction * _JunctionBoost;

    return channel;
}

#endif
