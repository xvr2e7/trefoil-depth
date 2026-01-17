Shader "Custom/SegmentHighlight"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _HighlightColor ("Highlight Color", Color) = (0,0,0,1)
        _HighlightStart ("Highlight Start Phi", Float) = -0.523
        _HighlightEnd ("Highlight End Phi", Float) = 0.523
        _R1 ("Trefoil R1", Float) = 1.0
        _R2 ("Trefoil R2", Float) = 2.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
        };

        fixed4 _BaseColor;
        fixed4 _HighlightColor;
        float _HighlightStart;
        float _HighlightEnd;
        half _Glossiness;
        half _Metallic;

        // Trefoil parameters (matching the FourierTrefoil3D)
        float _R1;
        float _R2;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Convert world position to local position
            float3 localPos = mul(unity_WorldToObject, float4(IN.worldPos, 1.0)).xyz;

            // Find the closest point on the trefoil curve and its phi value
            float closestPhi = 0.0;
            float minDist = 1000.0;

            // Sample the trefoil curve at many points to find the closest phi
            for (float phi = -3.14159; phi <= 3.14159; phi += 0.05)
            {
                float x = _R1 * cos(phi) + _R2 * cos(2.0 * phi);
                float y = _R1 * sin(phi) - _R2 * sin(2.0 * phi);

                // Project to XY plane for comparison (ignore Z)
                float dist = length(localPos.xy - float2(x, y));

                if (dist < minDist)
                {
                    minDist = dist;
                    closestPhi = phi;
                }
            }

            // Check if the closest phi is within highlighted segment range
            bool isHighlighted = (closestPhi >= _HighlightStart && closestPhi <= _HighlightEnd);

            // Set color based on whether in highlighted region
            fixed4 color = isHighlighted ? _HighlightColor : _BaseColor;

            o.Albedo = color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
