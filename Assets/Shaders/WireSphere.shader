Shader "Custom/WireSphere"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (1,1,1,1)
        _BackgroundColor ("Background Color", Color) = (0,0,0,0)
        _LineThickness ("Line Thickness", Range(0.001, 0.1)) = 0.02
        _LatitudeLines ("Latitude Lines", Int) = 8
        _LongitudeLines ("Longitude Lines", Int) = 16
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _LineColor;
            float4 _BackgroundColor;
            float _LineThickness;
            int _LatitudeLines;
            int _LongitudeLines;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                
                // Latitude lines
                float latLine = frac(uv.y * _LatitudeLines);
                float lat = min(latLine, 1.0 - latLine);
                
                // Longitude lines with pole handling
                float lonLine = frac(uv.x * _LongitudeLines);
                float lon = min(lonLine, 1.0 - lonLine);
                
                // At poles, force longitude lines to appear
                float poleThreshold = 0.02;
                float poleNorth = smoothstep(poleThreshold, 0.0, uv.y);
                float poleSouth = smoothstep(1.0 - poleThreshold, 1.0, uv.y);
                float atPole = max(poleNorth, poleSouth);
                
                lon = lerp(lon, 0.0, atPole);
                
                float grid = min(lat, lon);
                float alpha = 1.0 - smoothstep(0.0, _LineThickness, grid);
                
                fixed4 col = lerp(_BackgroundColor, _LineColor, alpha);
                return col;
            }
            ENDCG
        }
    }
}