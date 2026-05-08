Shader "Holobiont/ForceFieldRing"
{
    /*
     * Procedural force-field ring for the holobiont. Fourth member of the
     * shader family alongside CreatureSDF and SymbiosisTendrils — same
     * world-space petridish noise (PetriDishNoise.hlsl), same field-relation
     * vocabulary mapped onto the three force-field states:
     *
     *   _State 0 — Idle.    Wavy ring, breath-modulated thickness, no extras.
     *   _State 1 — Capture. Contain relation: noise inside the ring freezes
     *                       (slowed time scale) and saturates. Holobiont
     *                       holds its medium, like a giant hub.
     *   _State 2 — Shed.    Deflect relation: radial outflow shimmer pushes
     *                       outward through the silhouette. Expelling.
     *
     * Renderer is expected to be a SpriteRenderer scaled to currentRadius * 2
     * on a 1×1 white square sprite (same convention as the creature shader).
     * The view component (HolobiontView) drives _State, _CurrentRadius, and
     * the per-instance _BaseColor.a (for breath-driven master alpha) via MPB.
     *
     * Globals (from HolobiontShaderGlobals.cs):
     *   _HoloBreathPhase  — breath thickness + master color modulation.
     *
     * _CurrentRadius is set per-frame so the wave amplitude (authored in world
     * units) divides by the live diameter and the wave's perceived size stays
     * world-constant as the ring breathes.
     */
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Identity)]
        _State          ("State (0 idle 1 capture 2 shed)",     Range(0, 2)) = 0
        _BaseColor      ("Base color (idle)",                   Color) = (0.65, 0.88, 0.78, 0.55)
        _CaptureColor   ("Capture tint",                        Color) = (1.00, 0.85, 0.40, 0.85)
        _ShedColor      ("Shed tint",                           Color) = (0.45, 0.75, 1.00, 0.85)

        [Header(Geometry)]
        _Thickness                  ("Ring thickness (UV)",     Range(0.003, 0.2))   = 0.035
        _ThicknessBreathAmplitude   ("Breath thickness range",  Range(0, 0.1))       = 0.02
        _CurrentRadius              ("Current world radius",    Float)               = 1
        _EdgeSoftness               ("Edge softness",           Range(0.001, 0.1))   = 0.012

        [Header(Wave)]
        _WaveAmplitude  ("Wave amplitude (world)",  Range(0, 0.5))     = 0.06
        _NoiseScale     ("Noise scale (world)",     Range(0.05, 4))    = 0.6
        _WaveTimeScale  ("Wave time scale",         Range(0, 1))       = 0.18

        [Header(Capture)]
        _CaptureSaturation      ("Captured saturation",         Range(0, 2))    = 1.4
        _CaptureTimeScale       ("Captured time scale (slow)",  Range(0, 1))    = 0.20
        _CaptureInteriorAmount  ("Captured interior amount",    Range(0, 1))    = 0.55

        [Header(Shed)]
        _ShedOutflowStrength    ("Outflow shimmer strength",    Range(0, 1))    = 0.6
        _ShedOutflowSpeed       ("Outflow shimmer speed",       Range(0, 4))    = 1.5
        _ShedRingFrequency      ("Outflow ring frequency",      Range(2, 60))   = 24

        [Header(Channel Tint)]
        _ChannelTintAmount      ("Channel tint amount",         Range(0, 0.5)) = 0.15
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
            Name "ForceFieldRing2D"
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
                float4 _CaptureColor;
                float4 _ShedColor;
                float  _State;
                float  _Thickness;
                float  _ThicknessBreathAmplitude;
                float  _CurrentRadius;
                float  _EdgeSoftness;
                float  _WaveAmplitude;
                float  _NoiseScale;
                float  _WaveTimeScale;
                float  _CaptureSaturation;
                float  _CaptureTimeScale;
                float  _CaptureInteriorAmount;
                float  _ShedOutflowStrength;
                float  _ShedOutflowSpeed;
                float  _ShedRingFrequency;
                float  _ChannelTintAmount;
            CBUFFER_END

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
                float2 uvLocal     : TEXCOORD0;
                float2 uvWorld     : TEXCOORD1;
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

            half4 frag(Varyings IN) : SV_Target
            {
                int   state   = (int)round(_State);
                float t       = _Time.y;
                float breathT = _HoloBreathPhase * 0.5 + 0.5;

                // Thickness in sprite UV. Breath modulation: thicker during exhale,
                // thinner during inhale. Always positive.
                float thicknessUV = max(0.001,
                    _Thickness + _ThicknessBreathAmplitude * (breathT - 0.5));

                // Wave amplitude authored in world units → divide by the live world
                // diameter so the perceived wave stays world-constant as the ring breathes.
                float invDiameter = 1.0 / max(0.0001, 2.0 * _CurrentRadius);
                float waveAmpUV   = _WaveAmplitude * invDiameter;

                // Capture freezes the wave; idle/shed move at normal pace.
                float waveT = (state == 1) ? t * _CaptureTimeScale : t * _WaveTimeScale;
                float2 noiseUV = IN.uvWorld * _NoiseScale + float2(waveT, waveT * 0.7);

                float n = fbm(noiseUV, 2) - 0.5;

                // Pull the effective ring radius slightly inward and add the wave so the
                // ring breathes organically rather than sitting flat at the sprite edge.
                float baseR = 0.5;
                float r_eff = baseR - thicknessUV - waveAmpUV * n;

                float distFromCenter = length(IN.uvLocal);
                float distOuter = distFromCenter - r_eff;          // + outside the wavy radius
                float distRing  = abs(distOuter) - thicknessUV;    // - inside the band

                float aa       = fwidth(distRing) + _EdgeSoftness;
                float ringMask = 1.0 - smoothstep(-aa, aa, distRing);
                float interiorMask = 1.0 - smoothstep(-aa, aa, distOuter); // 1 inside the disk

                // ---- Color composition by state ----
                float3 col      = _BaseColor.rgb;
                float  alphaMul = _BaseColor.a;

                if (state == 1) // CAPTURE — contain relation
                {
                    // Captured pocket: saturated, slowed petridish noise inside the disk.
                    float captured = fbm(IN.uvWorld * _NoiseScale * 0.85
                                       + float2(waveT * 0.3, -waveT * 0.25), 3);
                    float3 inside  = lerp(_BaseColor.rgb * 0.6,
                                          _CaptureColor.rgb * _CaptureSaturation,
                                          captured);

                    col = lerp(_BaseColor.rgb, _CaptureColor.rgb, 0.7);

                    float interior = interiorMask * _CaptureInteriorAmount;
                    col = lerp(col, inside, interior);
                    ringMask = max(ringMask, interior * 0.6);
                }
                else if (state == 2) // SHED — deflect / outflow
                {
                    // Radial outflow shimmer: rings of brightness move outward from origin.
                    float radialPhase = (distFromCenter - t * _ShedOutflowSpeed * 0.1) * _ShedRingFrequency;
                    float pulse = sin(radialPhase) * 0.5 + 0.5;
                    pulse = pow(pulse, 3.0);
                    float outflow = pulse * _ShedOutflowStrength
                                  * smoothstep(-0.05, 0.0, distOuter); // outside the silhouette

                    col = lerp(_BaseColor.rgb, _ShedColor.rgb, 0.7);
                    col += _ShedColor.rgb * outflow;
                    ringMask = max(ringMask, outflow * 0.5);
                }
                // state == 0 (idle) → just the wavy ring, no extras.

                // Subtle channel tint sampled from the petridish — keeps the ring chromatically
                // alive and visually bonded to the bg.
                if (_ChannelTintAmount > 1e-4)
                {
                    float channelTint = fbm(IN.uvWorld * _NoiseScale * 1.4
                                          + float2(waveT * 0.5, 0), 2);
                    col *= lerp(1.0 - _ChannelTintAmount, 1.0 + _ChannelTintAmount, channelTint);
                }

                float alpha = ringMask * alphaMul * IN.spriteColor.a;
                col *= IN.spriteColor.rgb;
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
