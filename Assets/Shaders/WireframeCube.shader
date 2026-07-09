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
        // ZWrite On so the edge bands occlude the finger cursor per-pixel. With ZWrite Off
        // the cursor (nearer than the cube's bounds center whenever it works near the front
        // face) is sorted on top of every line, so it reads as "in front of" the front edge
        // even when it is behind it — occlusion is the only monocular depth cue here.
        // Face interiors are discarded below, so they never write depth.
        ZWrite On
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
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _EdgeColor;
            fixed4 _SecondaryColor;
            float  _LineThickness;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                o.uv2 = v.uv2;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if (unity_StereoEyeIndex == 0) discard;

                float2 uv  = i.uv;
                float  eu  = min(uv.x, 1.0 - uv.x);
                float  ev  = min(uv.y, 1.0 - uv.y);
                float  tag = i.uv2.x;

                float  alpha;
                fixed4 color;

                if (tag < 0.5)
                {
                    // Front/back face: all four edges in primary color
                    float edgeDist = min(eu, ev);
                    alpha = 1.0 - smoothstep(0.0, _LineThickness, edgeDist);
                    color = _EdgeColor;
                }
                else if (tag < 2.5)
                {
                    // Side face (left/bottom): V=0 edge is the traced depth edge (primary),
                    // V=1 edge is a non-traced depth edge (secondary). U edges are skipped
                    // because they are already covered by the front/back face passes.
                    float ap = 1.0 - smoothstep(0.0, _LineThickness, uv.y);
                    float as = 1.0 - smoothstep(0.0, _LineThickness, 1.0 - uv.y);
                    if (ap >= as) { alpha = ap; color = _EdgeColor; }
                    else          { alpha = as; color = _SecondaryColor; }
                }
                else
                {
                    // Side face (right/top): all V edges are non-traced depth edges (secondary)
                    alpha = 1.0 - smoothstep(0.0, _LineThickness, ev);
                    color = _SecondaryColor;
                }

                if (alpha < 0.01) discard;
                return fixed4(color.rgb, alpha * color.a);
            }
            ENDCG
        }
    }
}
