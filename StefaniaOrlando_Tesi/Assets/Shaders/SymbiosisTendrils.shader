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
     *               with the A→B direction (so local +Y is the world-space
     *               perpendicular); localScale = (lengthAB, width, 1).
     *   color.r   — pair stress in [0,1] (avg of the two creatures).
     *
     * Inside the shader:
     *   uv.x            — tAlong (0 at A, 1 at B), used for endpoint taper.
     *   uv.y → side     — remapped from [0,1] to [-1,+1] for cross-section falloff.
     *   UNITY_MATRIX_M  — world-space perpendicular is read off the model
     *                     matrix's +Y column (normalized), so the noise
     *                     displacement direction matches whatever the CPU set.
     *
     * Stress reading: thinner soft falloff (alpha drops) plus, past
     * _StressBreakStart, world-space noise punches gaps in the ribbon.
     * No flicker.
     *
     * Globals (from HolobiontShaderGlobals.cs):
     *   _HoloBreathPhase — alpha modulation; the network breathes with the player.
     */
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}

        _BaseColor          ("Base color (healthy)",       Color) = (0.85, 0.95, 0.7, 1)
        _FrayedColor        ("Frayed color (stressed)",    Color) = (0.55, 0.45, 0.35, 1)

        _CenterAlpha        ("Center alpha (max)",         Range(0, 1))   = 0.65
        _SoftPower          ("Cross-section softness",     Range(0.5, 6)) = 2

        _BreathMin          ("Breath min mult",            Range(0, 1))   = 0.55
        _BreathMax          ("Breath max mult",            Range(0, 1))   = 1.0

        _NoiseScale         ("World noise scale",          Range(0.05, 4)) = 0.6
        _WaveAmplitude      ("Wave amplitude",             Range(0, 0.4)) = 0.05
        _WaveTimeScale      ("Wave time scale",            Range(0, 1))   = 0.18

        _StressFray         ("Stress alpha drop",          Range(0, 1))   = 0.7
        _StressBreakStart   ("Stress break threshold",     Range(0, 1))   = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
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
            CBUFFER_END

            float _HoloBreathPhase;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0; // sprite UVs (0..1, 0..1)
                float4 color      : COLOR;     // SpriteRenderer.color — r = pairStress
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 uvWorld     : TEXCOORD1;
                float4 color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float t      = _Time.y * _WaveTimeScale;
                float tAlong = IN.uv.x;
                float taper  = sin(tAlong * 3.14159265);

                float3 wp = TransformObjectToWorld(IN.positionOS.xyz);

                // World-space perpendicular comes from the model matrix's +Y column.
                // CPU rotates the sprite so local +X is along the ribbon (A→B), so
                // local +Y is the perpendicular. Non-uniform scale (length × width)
                // means we have to normalize before using it as a direction.
                float2 perpWorld = mul((float3x3)UNITY_MATRIX_M, float3(0, 1, 0)).xy;
                float  perpLen   = length(perpWorld);
                perpWorld = (perpLen > 1e-6) ? perpWorld / perpLen : float2(0, 1);

                float n    = fbm(wp.xy * _NoiseScale + float2(t, t * 0.7), 2) - 0.5;
                float wave = n * _WaveAmplitude * taper;
                wp.xy += perpWorld * wave;

                OUT.positionHCS = TransformWorldToHClip(wp);
                OUT.uv          = IN.uv;
                OUT.uvWorld     = wp.xy;
                OUT.color       = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Sprite UVs go [0,1] across the quad. Remap uv.y to [-1,+1] for
                // the symmetric cross-section falloff used downstream.
                float side       = IN.uv.y * 2.0 - 1.0;
                float tAlong     = IN.uv.x;
                float pairStress = saturate(IN.color.r);

                // Soft cross-section falloff.
                float crossSec = pow(saturate(1.0 - abs(side)), _SoftPower);

                // Endpoint taper.
                float endpointTaper = saturate(sin(tAlong * 3.14159265));
                endpointTaper = pow(endpointTaper, 0.5);

                // Stress: alpha drop, then gap-punching past the break threshold.
                float stressMask = saturate(1.0 - pairStress * _StressFray);
                float breakRamp  = smoothstep(_StressBreakStart, 1.0, pairStress);
                if (breakRamp > 1e-4)
                {
                    float breakNoise = fbm(IN.uvWorld * _NoiseScale * 4.0, 2);
                    float lo = breakRamp * 0.55;
                    float hi = max(breakRamp * 0.85, lo + 1e-4);
                    stressMask *= smoothstep(lo, hi, breakNoise);
                }

                // Breath modulation.
                float breathT    = saturate(_HoloBreathPhase * 0.5 + 0.5);
                float breathMult = lerp(_BreathMin, _BreathMax, breathT);

                float3 col   = lerp(_BaseColor.rgb, _FrayedColor.rgb, pairStress);
                float  alpha = _CenterAlpha * crossSec * endpointTaper * stressMask * breathMult * _BaseColor.a;
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
