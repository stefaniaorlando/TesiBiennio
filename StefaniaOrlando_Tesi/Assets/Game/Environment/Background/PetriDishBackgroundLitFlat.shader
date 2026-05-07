Shader "Holobiont/PetriDishBackgroundLitFlat"
{
    Properties
    {
        // Required by SpriteRenderer — never sampled.
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Palette)]
        _BaseColor      ("Base (cell fill)",     Color) = (0.32, 0.46, 0.42, 1)
        _ChannelColor   ("Channels (membranes)", Color) = (0.55, 0.72, 0.70, 1)
        _AccentColor    ("Accent (warm pockets)",Color) = (0.75, 0.55, 0.32, 1)
        _DeepColor      ("Deep (cell centers)",  Color) = (0.18, 0.28, 0.30, 1)
        _AccentAmount   ("Accent amount",        Range(0, 1)) = 0.08

        [Header(Voronoi Cells)]
        _CellScale      ("Cell scale (world)",   Range(0.05, 5)) = 0.6
        _CellJitter     ("Cell jitter (organic)",Range(0, 1)) = 0.85
        _CellAnimSpeed  ("Cell drift speed",     Range(0, 1)) = 0.08

        [Header(UV Warp)]
        _WarpStrength   ("Warp strength",        Range(0, 2)) = 0.55
        _WarpScale      ("Warp scale",           Range(0.05, 5)) = 0.35
        _WarpSpeed      ("Warp scroll speed",    Range(0, 0.5)) = 0.02
        _FlowTurbScale  ("Flow turbulence scale (sync)",  Range(0, 5)) = 0
        _FlowTurbAmount ("Flow turbulence amount (sync)", Range(0, 5)) = 0

        [Header(Channels)]
        _ChannelWidth   ("Channel width",        Range(0.001, 0.2)) = 0.04
        _ChannelSoftness("Channel softness",     Range(0.001, 0.2)) = 0.06
        _ChannelIntensity("Channel intensity",   Range(0, 2)) = 0.25
        _JunctionBoost  ("Junction boost",       Range(0, 3)) = 0.4

        [Header(Pulse)]
        _PulseSpeed     ("Pulse speed",          Range(0, 4)) = 0.6
        _PulseFreq      ("Pulse frequency",      Range(0, 20)) = 4.0
        _PulseStrength  ("Pulse strength",       Range(0, 1)) = 0.25

        [Header(Particulate)]
        _GrainScale     ("Grain scale",          Range(1, 200)) = 60
        _GrainIntensity ("Grain intensity",      Range(0, 0.5)) = 0.06

        [Header(Composition)]
        _GlobalIntensity("Global intensity",     Range(0, 2)) = 0.85
        _TimeScale      ("Time scale (master)",  Range(0, 2)) = 1.0
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
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "PetriDishBackgroundLitFlat2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            // Four 2D-light blend-style slots (configured on the URP 2D Renderer asset).
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PetriDishSurface.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            // Declare the per-light textures + blend params for each enabled blend style.
            // Must come BEFORE CombinedShapeLightShared.hlsl, which references these globals.
            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uvWorld     : TEXCOORD0; // world XY for procedural noise
                half2  lightingUV  : TEXCOORD1; // screen UV for 2D light texture sampling
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                float3 wp       = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uvWorld     = wp.xy;
                OUT.lightingUV  = half2(ComputeScreenPos(OUT.positionHCS).xy);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t   = _Time.y * _TimeScale;
                half3 col = ComputePetriDishColor(IN.uvWorld, t);

                // v1: flat normal, all-ones mask. All 2D lights affect the surface uniformly.
                SurfaceData2D surfaceData;
                surfaceData.albedo   = col;
                surfaceData.alpha    = 1.0h;
                surfaceData.mask     = half4(1, 1, 1, 1);
                surfaceData.normalTS = half3(0, 0, 1);

                InputData2D inputData;
                inputData.uv         = IN.uvWorld;
                inputData.lightingUV = IN.lightingUV;

                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
