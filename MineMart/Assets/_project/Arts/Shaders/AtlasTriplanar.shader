Shader "MineMart/Voxel/AtlasTriplanar"
{
    Properties
    {
        [MainTexture] _BaseMap ("Atlas Texture (64x64 Blockbench)", 2D) = "white" {}
        _BaseColor ("Color Tint", Color) = (1, 1, 1, 1)

        [Header(Grid Settings)]
        _BlockSize ("Block Size (Meters)", Float) = 1.0
        [Toggle] _UseWorldSpace ("Use World Space (1=World, 0=Local Scaled)", Float) = 0.0

        [Header(Atlas Rectangles MinX MinY SizeX SizeY)]
        _TopUV ("Top UV Rect (Co Xanh)", Vector) = (0.0, 0.25, 0.25, 0.25)
        _SideUV ("Side UV Rect (Vien Co Dat)", Vector) = (0.0, 0.75, 0.25, 0.25)
        _BottomUV ("Bottom UV Rect (Dat Nau)", Vector) = (0.5, 0.75, 0.25, 0.25)

        [Header(Voxel Stair Depth And Face Shading)]
        _SideDarkness ("Do Toi Mat Dung Cua Bac Thang (Side Face Darkness)", Range(0.4, 1.0)) = 0.72
        _FaceContrast ("Do Chenh Lech Huong X/Z (Minecraft Shading)", Range(0.6, 1.0)) = 0.88
        _StepAO ("Bong Do Chan Bac Thang (Step Occlusion)", Range(0.0, 0.6)) = 0.25

        [Header(Lighting Settings)]
        _MinAmbient ("Min Ambient Light (Do Sang Toi Thieu)", Range(0.2, 0.9)) = 0.50
        _ShadowStrength ("Shadow Strength (Do Dam Bong Do)", Range(0.0, 1.0)) = 0.35
        [Toggle] _ReceiveShadows ("Nhan Bong Do Tu Vat The Khac", Float) = 1.0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 positionOS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float fogFactor     : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BlockSize;
                float _UseWorldSpace;
                float4 _TopUV;
                float4 _SideUV;
                float4 _BottomUV;
                float _SideDarkness;
                float _FaceContrast;
                float _StepAO;
                float _MinAmbient;
                float _ShadowStrength;
                float _ReceiveShadows;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.positionOS.xyz);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normalInput.normalWS;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            // Lay mau tu mot vung hinh chu nhat cua Texture Atlas khong bi lem vien (khu mipmap seam)
            float4 SampleAtlas(float2 localUV, float4 rect)
            {
                float2 wrapped = frac(localUV);

                // Tinh dao ham truoc khi frac de tranh vach den o bien 0..1
                float2 dx = ddx(localUV) * rect.zw;
                float2 dy = ddy(localUV) * rect.zw;

                // Co vien an toan 0.005 de giu nguyen ven pixel ben trong rect
                float2 insetUV = rect.xy + clamp(wrapped, 0.005, 0.995) * rect.zw;

                return SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, insetUV, dx, dy);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // BƯỚC 1: TÍNH FLAT FACE NORMAL TUYỆT ĐỐI (VUÔNG VỨC 90 ĐỘ KHÔNG BO TRÒN)
                float3 ddxPos = ddx(input.positionWS);
                float3 ddyPos = ddy(input.positionWS);
                float3 faceNormalWS = normalize(cross(ddyPos, ddxPos));
                
                if (dot(faceNormalWS, input.normalWS) < 0.0)
                {
                    faceNormalWS = -faceNormalWS;
                }

                // BƯỚC 2: TÍNH TỌA ĐỘ VỊ TRÍ THEO MÉT THỰC TẾ
                float blockSize = max(_BlockSize, 0.001);
                float3 pos;

                if (_UseWorldSpace > 0.5)
                {
                    pos = input.positionWS / blockSize;
                }
                else
                {
                    float3 objectScale = float3(
                        length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)),
                        length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y)),
                        length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z))
                    );

                    pos = (input.positionOS * objectScale) / blockSize;
                    pos += (objectScale / (blockSize * 2.0));
                }

                // BƯỚC 3: PHÂN LOẠI MẶT & TÍNH ĐỘ TƯƠNG PHẢN KHỐI (MINECRAFT DIRECTIONAL SHADING)
                float4 col = float4(1, 1, 1, 1);
                float faceShade = 1.0;
                float aoFactor = 1.0;

                if (faceNormalWS.y > 0.5)
                {
                    // Mặt Trên (Mặt dẫm chân lên): Sáng nhất (100%)
                    float2 uv = float2(pos.x, pos.z);
                    col = SampleAtlas(uv, _TopUV);
                    faceShade = 1.0;
                }
                else if (faceNormalWS.y < -0.5)
                {
                    // Mặt Đáy
                    float2 uv = float2(pos.x, pos.z);
                    col = SampleAtlas(uv, _BottomUV);
                    faceShade = 0.55;
                }
                else
                {
                    // 4 Mặt Đứng Của Bậc Thang (Side Faces)
                    // Tối hơn mặt trên rõ rệt để tách bạch từng bậc thang zíc zắc
                    float2 uv;
                    if (abs(faceNormalWS.x) > abs(faceNormalWS.z))
                    {
                        uv = float2(pos.z, pos.y);
                        faceShade = _SideDarkness * _FaceContrast; // Mặt hướng trục X
                    }
                    else
                    {
                        uv = float2(pos.x, pos.y);
                        faceShade = _SideDarkness;                 // Mặt hướng trục Z
                    }
                    col = SampleAtlas(uv, _SideUV);

                    // Hiệu ứng bóng đổ chân bậc thang (Step Occlusion):
                    // Chân bậc thang (phần đáy của mặt đứng) có bóng đổ nhẹ góc 90 độ
                    float localY = frac(pos.y);
                    if (localY < 0.25)
                    {
                        float aoT = localY / 0.25; // 0 o chan, 1 o mep tren
                        aoFactor = lerp(1.0 - _StepAO, 1.0, aoT);
                    }
                }

                col *= _BaseColor;
                col.rgb *= (faceShade * aoFactor);

                // BƯỚC 4: HỆ THỐNG ÁNH SÁNG URP CHUẨN VOXEL
                Light mainLight = GetMainLight();
                
                half NdotL = saturate(dot(faceNormalWS, mainLight.direction));

                half shadowAtten = 1.0;
                if (_ReceiveShadows > 0.5)
                {
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    Light shadowLight = GetMainLight(shadowCoord);
                    shadowAtten = lerp(1.0, shadowLight.shadowAttenuation, _ShadowStrength);
                }

                half3 directLight = mainLight.color * (NdotL * shadowAtten);

                half3 ambientSH = SampleSH(faceNormalWS);
                half3 ambientMin = half3(_MinAmbient, _MinAmbient, _MinAmbient);
                half3 finalAmbient = max(ambientSH, ambientMin);

                half3 totalLight = directLight + finalAmbient;
                half3 finalRGB = col.rgb * totalLight;

                finalRGB = MixFog(finalRGB, input.fogFactor);

                return half4(finalRGB, col.a);
            }
            ENDHLSL
        }

        // Shadow Caster Pass (Do bong chuan tren URP)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
