Shader "Hidden/AIToolkit/ProgressDisk"
{
    Properties
    {
        _StartValue ("Start Value", Range(0,1)) = 0        // Start value from 0 to 1.
        _Value ("Value", Range(0,1)) = 1                   // End value from 0 to 1.
        _InnerRadius ("Inner Radius", Range(0,1)) = 0.35   // Inner radius of the ring.
        _OuterRadius ("Outer Radius", Range(0,1)) = 0.45   // Outer radius of the ring.
        _BackgroundColor ("Background Color", Color) = (0,0,0,0) // Background color.
        _Color ("Color", Color) = (0.5,0.5,0.5,1)          // Ring color.
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _StartValue;
            float _Value;
            float _InnerRadius;
            float _OuterRadius;
            float4 _Color;
            float4 _BackgroundColor;

            struct attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct varyings
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            varyings vert(const attributes v)
            {
                varyings o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(varyings i) : SV_Target
            {
                // Constants
                const float PI = 3.14159265359;
                const float TWO_PI = 6.28318530718;

                const float start_value = _StartValue;
                const float value = min(_Value, _StartValue + 1 - 1e-5);

                // Transform UV coordinates to [-1, 1]
                float2 uv = i.uv * 2.0 - 1.0;
                uv.y = -uv.y; // Flip Y-axis if needed

                const float radius = length(uv);

                // Compute angle of the current point in [0, TWO_PI], starting from the top
                float ang = atan2(uv.y, uv.x);
                ang += PI * 0.5; // Shift angle so that 0 is at the top
                if (ang < 0.0)
                    ang += TWO_PI;

                // Convert start and end values to angles in radians [0, TWO_PI]
                const float start_angle = frac(start_value) * TWO_PI;
                const float end_angle = frac(value) * TWO_PI;

                // Determine if the sector crosses the 0-degree line
                const bool crosses_zero = end_angle < start_angle;

                // Compute angular distance to the sector edges
                float dist_to_start = ang - start_angle;
                float dist_to_end = end_angle - ang;

                if (crosses_zero)
                {
                    if (ang < start_angle)
                        dist_to_start = ang + (TWO_PI - start_angle);
                    if (ang > end_angle)
                        dist_to_end = (end_angle + TWO_PI) - ang;
                }

                const float dist_angular = max(-dist_to_start, -dist_to_end);

                // Radial SDFs
                const float dist_inner = _InnerRadius - radius; // Negative inside inner radius
                const float dist_outer = radius - _OuterRadius; // Negative inside outer radius
                const float dist_radial = max(dist_inner, dist_outer);

                // Combine radial and angular SDFs
                float sdf = max(dist_radial, dist_angular);

                // Anti-aliasing width
                float aa = fwidth(sdf);

                // Compute alpha using smoothstep for anti-aliasing
                float alpha = 1.0 - smoothstep(-aa, aa, sdf);

                // Apply ring color alpha
                alpha *= _Color.a;

                // Output color with computed alpha
                return lerp(_BackgroundColor, _Color, alpha);
            }

            ENDCG
        }
    }
}
