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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Uniforms
            sampler2D _CaptureDiffuseMap;
            sampler2D _CaptureNormalMap;
            sampler2D _CaptureMaskMap;
            int _CaptureMapSelector;

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
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBinormal : TEXCOORD3;
            };

            // Vertex shader
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                // Build world-space TBN matrix
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                float3 worldBinormal = cross(worldNormal, worldTangent) * v.tangent.w;

                o.worldNormal = worldNormal;
                o.worldTangent = worldTangent;
                o.worldBinormal = worldBinormal;

                return o;
            }

            // Fragment shader
            fixed4 frag (v2f i) : SV_Target
            {
                // Sample textures
                fixed4 albedo = tex2D(_CaptureDiffuseMap, i.uv);
                fixed4 mask = tex2D(_CaptureMaskMap, i.uv);

                // Sample normal map and unpack
                fixed3 normalTS = tex2D(_CaptureNormalMap, i.uv).xyz * 2.0 - 1.0;

                // Transform normal to world space
                float3x3 TBN = float3x3(i.worldTangent, i.worldBinormal, i.worldNormal);
                float3 normal = normalize(mul(normalTS, TBN));

                fixed4 finalColor = 0;

                if (_CaptureMapSelector == 0) {
                    finalColor = fixed4(albedo.rgb, 1);
                } else if (_CaptureMapSelector == 1) {
                    finalColor = fixed4(normal * 0.5 + 0.5, 1);
                } else if (_CaptureMapSelector == 2) {
                    finalColor = fixed4(mask.rgb, 1); 
                }

                return finalColor;
            }
            ENDCG
        }
    }
}
