Shader "ImanTransmedia/FrostedGlassV3_UI"
{
    Properties
    {
        // UI standard
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1,1,1,1)

        _BlurRadius ("Blur Radius", Range(0.0, 20.0)) = 4.0
        _Sigma ("Blur Sigma", Range(0.1, 15.0)) = 8.75
        _DepthFadeDistance ("Depth Fade Distance", Range(0.0, 3.0)) = 0.5
        _DepthFadePower ("Depth Fade Power", Range(0.1, 5.0)) = 1.0
        [Toggle] _DebugNoBlur ("Debug: Disable Blur", Float) = 0
        [Toggle] _DebugNoFade ("Debug: Disable Depth Fade", Float) = 0

        //  UI masking / stencil
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        // NEW: clip rect used by uGUI when using masking/clipping
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        // NEW: optional alpha clip toggle (usually keep 0)
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "FrostedGlassUI"
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            //stencil block 
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;

                float _BlurRadius;
                float _Sigma;
                float _DepthFadeDistance;
                float _DepthFadePower;
                float _DebugNoBlur;
                float _DebugNoFade;

                // uGUI clip rect data
                float4 _ClipRect;
                float _UseUIAlphaClip;
            CBUFFER_END

            //_MainTex is what uGUI Image/Panel provides automatically
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; 
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float linearDepth : TEXCOORD2;
                float4 color : COLOR;
                float2 posOS : TEXCOORD3;
            };

            float Gaussian(float2 pos, float sigma)
            {
                return exp(-dot(pos, pos) / (2.0 * sigma * sigma))
                     / (2.0 * 3.14159265 * sigma * sigma);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.linearDepth = -TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz)).z;
                o.color = input.color;
                o.posOS = input.positionOS.xy; 
                return o;
            }

            float UnityGet2DClipping(float2 pos, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, pos) * step(pos, clipRect.zw);
                return inside.x * inside.y;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // read alpha from the UI 
                half4 uiTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float uiAlpha = uiTex.a * input.color.a;

                if (uiAlpha <= 0.001)
                    discard;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // RESOLUTION-INDEPENDENT BLUR
                // _BlurRadius ahora es en "unidades de pantalla" (como 4.0 = 4 píxeles en 1080p, pero se escala perfecto en cualquier resolución)
                float2 blurOffset = _BlurRadius * 0.001; // 0.001 ≈ 1 píxel en 1080p → escalará perfecto en 4K

                half4 sceneCol;

                if (_DebugNoBlur > 0.5)
                {
                    sceneCol = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);
                }
                else
                {
                    half4 acc = 0;
                    float weightSum = 0;

                    [unroll]
                    // 7x7 kernel
                    for (int x = -3; x <= 3; x++)
                    {
                        [unroll]
                        for (int y = -3; y <= 3; y++)
                        {
                            float2 offset = float2(x, y) * blurOffset;
                            float2 uv = screenUV + offset;

                            float d = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                            float w = Gaussian(float2(x, y), _Sigma);

                            if (d >= input.linearDepth)
                            {
                                // Solo incluir píxeles detrás del plano
                                half4 sc = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
                                acc += sc * w;
                                weightSum += w;
                            }
                        }
                    }

                    sceneCol = (weightSum > 0.0001)
                        ? acc / weightSum
                        : SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);
                }

                // Depth Fade (bordes del plano)
                float fade = 1.0;
                if (_DebugNoFade < 0.5)
                {
                    float sd = SampleSceneDepth(screenUV);
                    float dd = abs(LinearEyeDepth(sd, _ZBufferParams) - input.linearDepth);
                    fade = pow(saturate(dd / _DepthFadeDistance), _DepthFadePower);
                }

                float clipFactor = UnityGet2DClipping(input.posOS, _ClipRect);
                float finalAlpha = uiAlpha * fade * clipFactor;
                // Apply UI Image color + tint
                half3 outRGB = sceneCol.rgb * _TintColor.rgb * input.color.rgb;

                // Premultiply for clean edges
                outRGB *= finalAlpha;


                half4 outCol = half4(outRGB, finalAlpha);

                if (_UseUIAlphaClip > 0.5)
                    clip(outCol.a - 0.001);

                return outCol;
            }
            ENDHLSL
        }
    }
}
