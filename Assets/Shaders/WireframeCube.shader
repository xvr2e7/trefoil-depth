Shader "Custom/WireframeCube"
{
    Properties
    {
        _EdgeColor      ("Primary Edge Color",   Color) = (1,1,1,1)
        _SecondaryColor ("Secondary Edge Color", Color) = (0.4,0.4,0.4,1)
        _LineThickness  ("Line Thickness",       Range(0.001, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv  : TEXCOORD0;
                float2 uv2 : TEXCOORD1; // 1 = side face (depth edges only), 0 = front/back face
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            fixed4 _EdgeColor;
            fixed4 _SecondaryColor;
            float  _LineThickness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                o.uv2 = v.uv2;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float eu = min(uv.x, 1.0 - uv.x);
                float ev = min(uv.y, 1.0 - uv.y);

                float edgeDist;
                fixed4 color;

                if (i.uv2.x > 0.5)
                {
                    // Side face: V axis = depth direction → draw only depth edges in secondary color
                    edgeDist = ev;
                    color    = _SecondaryColor;
                }
                else
                {
                    // Front / back face: draw all four edges in primary color
                    edgeDist = min(eu, ev);
                    color    = _EdgeColor;
                }

                float alpha = 1.0 - smoothstep(0.0, _LineThickness, edgeDist);
                if (alpha < 0.01) discard;
                return fixed4(color.rgb, alpha * color.a);
            }
            ENDCG
        }
    }
}
