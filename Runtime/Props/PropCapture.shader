Shader "Custom/PropCapture"
{
    Properties
    {
        _CaptureDiffuseMap ("Diffuse Map", 2D) = "white" {}
        _CaptureNormalMap ("Normal Map", 2D) = "bump" {}
        _CaptureMaskMap ("Mask Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0  // Disable color output

            CGPROGRAM
            #pragma vertex vert_depth
            #pragma fragment frag_depth
            #include "UnityCG.cginc"

            struct appdata_depth
            {
                float4 vertex : POSITION;
            };

            struct v2f_depth
            {
                float4 pos : SV_POSITION;
            };

            v2f_depth vert_depth(appdata_depth v)
            {
                v2f_depth o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag_depth(v2f_depth i) : SV_Target
            {
                return 0; // No color output
            }
            ENDCG
        }

        Pass
        {

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Uniforms
            sampler2D _CaptureDiffuseMap;
            sampler2D _CaptureNormalMap;
            sampler2D _CaptureMaskMap;
            int _CaptureMapSelector;

            int _NormalXSwizzle;
            int _NormalYSwizzle;
            int _NormalZSwizzle;

            // Structs
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 tspace0 : TEXCOORD1; // tangent.x, bitangent.x, normal.x
                float3 tspace1 : TEXCOORD2; // tangent.y, bitangent.y, normal.y
                float3 tspace2 : TEXCOORD3; // tangent.z, bitangent.z, normal.z
            };

            float SwizzleNormalAxis(float3 normal, int mode) {
                if (mode < 3) {
                    return normal[mode];
                } else {
                    return -normal[mode % 3];
                }    

                return 0;
            }

            // Vertex shader
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                // Build world-space TBN matrix
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                float3 worldBitangent = cross(worldNormal, worldTangent) * v.tangent.w;

                o.tspace0 = float3(worldTangent.x, worldBitangent.x, worldNormal.x);
                o.tspace1 = float3(worldTangent.y, worldBitangent.y, worldNormal.y);
                o.tspace2 = float3(worldTangent.z, worldBitangent.z, worldNormal.z);

                return o;
            }

            // Fragment shader
            fixed4 frag (v2f i) : SV_Target
            {
                // Sample textures
                fixed4 albedo = tex2D(_CaptureDiffuseMap, i.uv);
                fixed4 mask = tex2D(_CaptureMaskMap, i.uv);

                // Sample normal map and unpack
                fixed3 normalTS = UnpackNormal(tex2D(_CaptureNormalMap, i.uv));
                normalTS.z = sqrt(1.0 - saturate(dot(normalTS.xy, normalTS.xy)));

                // Transform normal to world space
                half3 worldNormal;
                worldNormal.x = dot(i.tspace0, normalTS);
                worldNormal.y = dot(i.tspace1, normalTS);
                worldNormal.z = dot(i.tspace2, normalTS);
                worldNormal = normalize(worldNormal);

                fixed4 finalColor = 0;

                if (_CaptureMapSelector == 0) {
                    finalColor = fixed4(albedo.rgb, 1);
                } else if (_CaptureMapSelector == 1) {
                    float3 tempNormal = 0;
                    tempNormal.x = SwizzleNormalAxis(worldNormal, _NormalXSwizzle);
                    tempNormal.y = SwizzleNormalAxis(worldNormal, _NormalYSwizzle);
                    tempNormal.z = SwizzleNormalAxis(worldNormal, _NormalZSwizzle);

                    finalColor = fixed4(tempNormal * 0.5 + 0.5, 1);
                } else if (_CaptureMapSelector == 2) {
                    finalColor = fixed4(mask.rgb, 1); 
                }

                return finalColor;
            }
            ENDCG
        }
    }
}
