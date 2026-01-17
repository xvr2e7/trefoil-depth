Shader "Custom/AsymmetricStereo"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                float4 localPos = v.vertex;
                
                // Right eye (index 1): flatten z to 0 (orthographic-like, no depth)
                // Left eye (index 0): keep original z (sees 3D depth)
                if (unity_StereoEyeIndex == 1)
                {
                    localPos.z = 0;
                }
                
                o.vertex = UnityObjectToClipPos(localPos);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                // Simple diffuse lighting
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = max(0.2, dot(normal, lightDir));
                
                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;
                float3 diffuse = _LightColor0.rgb * NdotL;
                
                float3 lighting = ambient + diffuse;
                return fixed4(_Color.rgb * lighting, _Color.a);
            }
            ENDCG
        }
    }
}
