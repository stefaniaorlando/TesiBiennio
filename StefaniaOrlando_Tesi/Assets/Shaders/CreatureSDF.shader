Shader "Holobiont/CreatureSDF"
{
    /*
     * Single SDF shader for all three creature types. _Shape selects the
     * silhouette (0 circle / 1 triangle / 2 square — aligned with the
     * CreatureType enum). _FieldRelation selects how the silhouette interacts
     * with the world-space petridish noise field (0 dissolve / 1 deflect /
     * 2 contain — also aligned with CreatureType so the canonical pairing is
     * automatic, but they're separable to allow future special states).
     *
     * Uniform stress channel: edge fray + desaturation, plus a labored
     * heartbeat throb past _HeartbeatStart. NO high-frequency flicker —
     * flicker is reserved for the death event handled outside this shader.
     *
     * Drives:
     *   nutrici  — dissolve band at the silhouette, width grows with efficiency
     *   scudo    — outward deflection bow + SDF prewarp away from noise gradient
     *   hub      — captured noise pocket inside the silhouette, slowed time
     *
     * Globals (from HolobiontShaderGlobals.cs):
     *   _HoloBreathPhase   — gentle global breath modulation (-1..+1)
     *
     * World-space noise sampling shares PetriDishNoise.hlsl with the bg, so
     * creatures sit IN the medium rather than on top of it.
     */
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Identity)]
        _Shape          ("Shape (0 circle 1 triangle 2 square)",        Range(0, 2)) = 0
        _FieldRelation  ("Field Relation (0 dissolve 1 deflect 2 contain)", Range(0, 2)) = 0
        _BaseColor      ("Base color (affinity-tinted)",                Color) = (0.6, 0.85, 0.7, 1)

        [Header(State)]
        _Stress         ("Stress",      Range(0, 1)) = 0
        _Efficiency     ("Efficiency",  Range(0, 1)) = 1

        [Header(Shape)]
        _ShapeRadius    ("Shape radius (in sprite UV)",     Range(0.05, 0.5))   = 0.4
        _EdgeSoftness   ("Edge softness",                   Range(0.001, 0.2))  = 0.04

        [Header(Field Coupling)]
        _DissolveBand       ("Dissolve band (nutrici)",         Range(0,    0.4))   = 0.18
        _DeflectStrength    ("Deflect strength (scudo)",        Range(0,    0.6))   = 0.25
        _DeflectRange       ("Deflect outer range (scudo+hub)", Range(0,    0.4))   = 0.12
        _ContainSaturation  ("Contain saturation (hub)",        Range(0,    2.0))   = 1.4
        _ContainTimeScale   ("Contain time scale (hub)",        Range(0,    1.0))   = 0.3

        [Header(Stress)]
        _FrayAmplitude      ("Edge fray amplitude",     Range(0, 0.3))  = 0.08
        _FrayScale          ("Edge fray noise scale",   Range(0.5, 20)) = 8
        _DesaturationAmount ("Stress desaturation",     Range(0, 1))    = 0.7
        _HeartbeatStart     ("Heartbeat threshold",     Range(0, 1))    = 0.7
        _HeartbeatRate      ("Heartbeat rate (Hz)",     Range(0.1, 5))  = 1.2
        _HeartbeatAmplitude ("Heartbeat amplitude",     Range(0, 0.4))  = 0.18

        [Header(Field)]
        _NoiseScale     ("Noise scale (world)",     Range(0.05, 4))     = 0.6
        _RimStrength    ("Inner rim strength",      Range(0, 1))        = 0.4
        _BreathTint     ("Breath tint amount",      Range(0, 0.3))      = 0.07
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        Pass
        {
            Name "CreatureSDF2D"
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PetriDishNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Shape;
                float  _FieldRelation;
                float  _Stress;
                float  _Efficiency;

                float  _ShapeRadius;
                float  _EdgeSoftness;

                float  _DissolveBand;
                float  _DeflectStrength;
                float  _DeflectRange;
                float  _ContainSaturation;
                float  _ContainTimeScale;

                float  _FrayAmplitude;
                float  _FrayScale;
                float  _DesaturationAmount;
                float  _HeartbeatStart;
                float  _HeartbeatRate;
                float  _HeartbeatAmplitude;

                float  _NoiseScale;
                float  _RimStrength;
                float  _BreathTint;
            CBUFFER_END

            // Globals — set by HolobiontShaderGlobals.
            float _HoloBreathPhase;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uvLocal     : TEXCOORD0; // -0.5..+0.5 (sprite UV centered)
                float2 uvWorld     : TEXCOORD1; // shared with petridish bg
                float4 spriteColor : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                float3 wp       = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uvLocal     = IN.uv - 0.5;
                OUT.uvWorld     = wp.xy;
                OUT.spriteColor = IN.color;
                return OUT;
            }

            // ----- SDFs (centered at origin, return signed distance) -----
            float sdfCircle(float2 p, float r)
            {
                return length(p) - r;
            }

            float sdfSquare(float2 p, float r)
            {
                float2 d = abs(p) - r;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
            }

            // Equilateral triangle pointing +Y, half-size r. Inigo Quilez SDF.
            float sdfTriangle(float2 p, float r)
            {
                const float k = sqrt(3.0);
                p.x = abs(p.x) - r;
                p.y = p.y + r / k;
                if (p.x + k * p.y > 0.0)
                    p = float2(p.x - k * p.y, -k * p.x - p.y) / 2.0;
                p.x -= clamp(p.x, -2.0 * r, 0.0);
                return -length(p) * sign(p.y);
            }

            float sampleSDF(int shape, float2 p, float r)
            {
                if (shape == 0) return sdfCircle(p, r);
                if (shape == 1) return sdfTriangle(p, r);
                return sdfSquare(p, r);
            }

            // Edge fray: noise-perturb the SDF distance. Quiet at low stress.
            float frayDistance(float baseDist, float2 worldUV, float stress)
            {
                float ramp = smoothstep(0.3, 1.0, stress);
                if (ramp <= 0.0) return baseDist;
                float n = (fbm(worldUV * _FrayScale, 2) - 0.5) * 2.0;
                return baseDist + n * _FrayAmplitude * ramp;
            }

            float3 desaturate(float3 c, float amount)
            {
                float gray = dot(c, float3(0.299, 0.587, 0.114));
                return lerp(c, gray.xxx, amount);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                int   shape      = (int)round(_Shape);
                int   field      = (int)round(_FieldRelation);
                float stress     = saturate(_Stress);
                float efficiency = saturate(_Efficiency);
                float t          = _Time.y;

                float fieldT  = (field == 2) ? t * _ContainTimeScale : t;
                float2 noiseUV = IN.uvWorld * _NoiseScale;

                // Deflect (scudo): pre-warp the SDF coordinate outward along the noise gradient.
                float2 sdfP = IN.uvLocal;
                if (field == 1)
                {
                    float n1 = fbm(noiseUV + float2(t * 0.10, 0.0),  2) - 0.5;
                    float n2 = fbm(noiseUV + float2(0.0, t * 0.07),  2) - 0.5;
                    float deflectAmt = _DeflectStrength * efficiency * (1.0 - stress * 0.5);
                    sdfP -= float2(n1, n2) * deflectAmt;
                }

                float dist = sampleSDF(shape, sdfP, _ShapeRadius);
                dist = frayDistance(dist, noiseUV, stress);

                float aa = fwidth(dist) + _EdgeSoftness;
                float insideMask = 1.0 - smoothstep(-aa, aa, dist);

                // ----- Color composition by field relation -----
                float3 col = _BaseColor.rgb;
                float  n   = fbm(noiseUV + float2(fieldT * 0.05, fieldT * 0.03), 3);

                if (field == 0) // dissolve (nutrici)
                {
                    col *= lerp(0.7, 1.25, n);

                    // Edge dissolve band — width grows with efficiency, shrinks with stress.
                    float band = _DissolveBand * lerp(0.3, 1.0, efficiency) * (1.0 - stress * 0.7);
                    if (band > 1e-4 && dist > -band)
                    {
                        float bandT = saturate((dist + band) / band); // 0 inner..1 outer
                        insideMask *= lerp(1.0, n, bandT);
                    }
                }
                else if (field == 1) // deflect (scudo)
                {
                    col *= lerp(0.95, 1.15, n * 0.5 + 0.5);
                    if (dist > 0.0 && dist < _DeflectRange)
                    {
                        float bow = (1.0 - dist / _DeflectRange) * _DeflectStrength * efficiency * (1.0 - stress);
                        col += _BaseColor.rgb * bow * 0.6;
                        insideMask = max(insideMask, bow * 0.5);
                    }
                }
                else // contain (hub)
                {
                    float captured = fbm(noiseUV * 0.85 + float2(fieldT * 0.04, -fieldT * 0.03), 3);
                    col = lerp(_BaseColor.rgb * 0.7, _BaseColor.rgb * _ContainSaturation, captured);

                    // Stress: containment leaks outward through a small band.
                    float leak = smoothstep(0.4, 1.0, stress);
                    float leakRange = _DeflectRange * leak;
                    if (leakRange > 1e-4 && dist > 0.0 && dist < leakRange)
                    {
                        float leakAlpha = (1.0 - dist / leakRange) * leak * 0.6;
                        insideMask = max(insideMask, leakAlpha);
                    }
                }

                // Inner rim — solarpunk light-through-leaf, strongest just inside the edge.
                if (dist < 0.0)
                {
                    float rim = smoothstep(-_ShapeRadius * 0.5, 0.0, dist) * _RimStrength;
                    col += _BaseColor.rgb * rim * 0.4;
                }

                // Stress: continuous desaturation.
                col = desaturate(col, stress * _DesaturationAmount);

                // Stress: labored heartbeat — alpha throb only past the threshold. NO flicker.
                float beatStrength = smoothstep(_HeartbeatStart, 1.0, stress);
                if (beatStrength > 0.0)
                {
                    float beat = sin(t * _HeartbeatRate * 6.2831) * 0.5 + 0.5;
                    insideMask *= 1.0 - beatStrength * _HeartbeatAmplitude * (1.0 - beat);
                }

                // Stress: fragmenting silhouette in the top tier (>=0.9). World-space noise
                // punches alpha gaps INSIDE the shape — visually distinct from continuous
                // edge fray, reads as the silhouette breaking apart.
                float fragRamp = smoothstep(0.9, 1.0, stress);
                if (fragRamp > 0.0)
                {
                    float fragNoise = fbm(noiseUV * _FrayScale * 1.5 + float2(t * 0.3, t * 0.21), 2);
                    float gap       = smoothstep(0.42 - fragRamp * 0.32, 0.62, fragNoise);
                    insideMask *= lerp(1.0, gap, fragRamp);
                }

                // Subtle global breath modulation in luminosity.
                col *= 1.0 + _HoloBreathPhase * _BreathTint;

                float alpha = saturate(insideMask) * _BaseColor.a * IN.spriteColor.a;
                col *= IN.spriteColor.rgb;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
