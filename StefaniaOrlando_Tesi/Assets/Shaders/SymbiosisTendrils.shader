Shader "Holobiont/SymbiosisTendrils"
{
    /*
     * Soft, swaying tendrils between adjacent bonded creatures — the visible
     * symbiosis network. One SpriteRenderer per pair (pooled by
     * HolobiontTendrilNetwork) on a 1×1 white square sprite — same renderer
     * primitive as creatures and the bg, fully URP-2D native.
     *
     * Per-instance contract (driven by the CPU pool):
     *   transform — position = midpoint of A and B; rotation aligns local +X
     *               with the A→B direction (so the sprite's UV.x runs
     *               along the ribbon, UV.y across); localScale =
     *               (lengthAB, width, 1).
     *   color.r   — pair stress in [0,1] (avg of the two creatures).
     *
     * Fragment-only stylization (vert is the simple CreatureSDF pattern —
     * URP 2D batching can collapse UNITY_MATRIX_M to identity, so we don't
     * rely on per-vertex world-space math):
     *   uv.x  → tAlong  (0 at A, 1 at B); endpoint taper.
     *   uv.y  → side    (remapped to -1..+1); soft cross-section falloff.
     *   wobble  — UV-space side shift driven by world-noise + time.
     *   stress  — color shift toward the frayed tint, alpha attenuation, and
     *             past _StressBreakStart, world-noise gap-punching whose
     *             max depth is bounded by _StressBreakStrength so the
     *             ribbon never disappears entirely.
     *
     * Globals (from HolobiontShaderGlobals.cs):
     *   _HoloBreathPhase — alpha modulation; the network breathes with the player.
     */
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}

        _BaseColor          ("Base color (healthy)",       Color) = (0.85, 0.95, 0.7, 1)
        _FrayedColor        ("Frayed color (stressed)",    Color) = (0.55, 0.45, 0.35, 1)

        _CenterAlpha        ("Center alpha (max)",         Range(0, 1))   = 0.85
        _SoftPower          ("Cross-section softness",     Range(0.5, 6)) = 2

        _BreathMin          ("Breath min mult",            Range(0, 1))   = 0.6
        _BreathMax          ("Breath max mult",            Range(0, 1))   = 1.0

        _NoiseScale         ("World noise scale",          Range(0.05, 4)) = 0.6
        _WaveAmplitude      ("Wave amplitude (uv.y)",      Range(0, 0.4)) = 0.08
        _WaveTimeScale      ("Wave time scale",            Range(0, 1))   = 0.18

        _StressFray         ("Stress alpha drop",          Range(0, 1))   = 0.5
        _StressBreakStart   ("Stress break threshold",     Range(0, 1))   = 0.6
        _StressBreakStrength("Stress break strength",      Range(0, 1))   = 0.4
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
            Name "SymbiosisTendrils2D"
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
                float4 _FrayedColor;
                float  _CenterAlpha;
                float  _SoftPower;
                float  _BreathMin;
                float  _BreathMax;
                float  _NoiseScale;
                float  _WaveAmplitude;
                float  _WaveTimeScale;
                float  _StressFray;
                float  _StressBreakStart;
                float  _StressBreakStrength;
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
                float2 uv          : TEXCOORD0; // 0..1 across the sprite
                float2 uvWorld     : TEXCOORD1; // for world-coord noise
                float4 spriteColor : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                float3 wp       = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.uvWorld     = wp.xy;
                OUT.spriteColor = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t          = _Time.y * _WaveTimeScale;
                float tAlong     = IN.uv.x;
                float pairStress = saturate(IN.spriteColor.r);

                // Wobble: shift the cross-section coordinate by a world-noise
                // sample, tapered to zero at the endpoints.
                float taper      = sin(tAlong * 3.14159265);
                float n          = fbm(IN.uvWorld * _NoiseScale + float2(t, t * 0.7), 2) - 0.5;
                float side       = (IN.uv.y * 2.0 - 1.0) - n * _WaveAmplitude * 2.0 * taper;

                // Soft cross-section falloff.
                float crossSec = pow(saturate(1.0 - abs(side)), _SoftPower);

                // Endpoint taper.
                float endpointTaper = pow(saturate(taper), 0.5);

                // Stress: gentle alpha attenuation, plus optional world-noise
                // gap-punching past _StressBreakStart. The break depth is
                // capped by _StressBreakStrength — at strength 0 the ribbon
                // looks unbroken; at strength 1 you get the dramatic original
                // fragmenting silhouette.
                float stressAtten = 1.0 - pairStress * _StressFray;
                float breakRamp   = smoothstep(_StressBreakStart, 1.0, pairStress);
                if (breakRamp > 1e-4 && _StressBreakStrength > 1e-4)
                {
                    float breakNoise = fbm(IN.uvWorld * _NoiseScale * 4.0, 2);
                    float gap        = smoothstep(0.55, 0.85, breakNoise);
                    float minMult    = 1.0 - _StressBreakStrength * breakRamp;
                    stressAtten     *= lerp(minMult, 1.0, gap);
                }

                // Breath modulation.
                float breathT    = saturate(_HoloBreathPhase * 0.5 + 0.5);
                float breathMult = lerp(_BreathMin, _BreathMax, breathT);

                float3 col   = lerp(_BaseColor.rgb, _FrayedColor.rgb, pairStress);
                float  alpha = _CenterAlpha * crossSec * endpointTaper * stressAtten * breathMult * _BaseColor.a;
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
