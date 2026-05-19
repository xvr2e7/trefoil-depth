Shader "Custom/RightEyeOnly"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Brightness ("Brightness", Range(0.5, 2.0)) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _Brightness;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color  = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                if (unity_StereoEyeIndex == 0)
                {
                    discard;
                }

                // Vertex color carries per-segment shading; _Color is a global tint.
                // When mesh has no Color array Unity supplies white, so legacy behavior is preserved.
                return fixed4(_Color.rgb * i.color.rgb * _Brightness, _Color.a);
            }
            ENDCG
        }
    }
}
