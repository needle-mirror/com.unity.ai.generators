Shader "Hidden/AIToolkit/ProgressDisk"
{
    Properties
    {
        _MainTex ("Dummy Texture (Used for Aspect Ratio)", 2D) = "white" {} // Needed for _TexelSize
        _StartValue ("Start Value", Range(0, 1)) = 0         // Start value from 0 to 1 (fraction of circle)
        _Value ("Value", Range(0, 1)) = 1                    // End value from 0 to 1 (fraction of circle)
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.35    // Inner radius (0 to 1, where 1 is half the height)
        _OuterRadius ("Outer Radius", Range(0, 1)) = 0.45    // Outer radius (0 to 1, where 1 is half the height)
        _BackgroundColor ("Background Color", Color) = (0,0,0,0) // Background color (can be transparent)
        _Color ("Disk Color", Color) = (0.5,0.5,0.5,1)       // Disk segment color (alpha is used)
    }
    SubShader
    {
        // Use Transparency settings for UI or overlays
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        LOD 100

        Pass
        {
            ZWrite Off          // Don't write to depth buffer (typical for UI/overlays)
            Cull Off            // Don't cull back faces (useful for quads)
            Blend SrcAlpha OneMinusSrcAlpha // Standard alpha blending

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0 // Needed for fwidth

            #include "UnityCG.cginc"

            sampler2D _MainTex; // Declare sampler even if not used for color
            float4 _MainTex_TexelSize; // Contains (1/width, 1/height, width, height)

            float _StartValue;
            float _Value;
            float _InnerRadius;
            float _OuterRadius;
            float4 _Color;
            float4 _BackgroundColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Pass UVs directly, aspect correction happens in fragment shader
                o.uv = v.uv;
                return o;
            }

            // Helper to wrap angle to [-PI, PI]
            float wrapAngle(float angle) {
                return fmod(angle + UNITY_PI, UNITY_TWO_PI) - UNITY_PI;
            }

            float4 frag(v2f i) : SV_Target
            {
                // --- Aspect Ratio Correction ---
                // Calculate aspect ratio (width / height)
                // Use max to avoid division by zero if texture isn't set properly
                float aspect = 1.0;
                if (_MainTex_TexelSize.x > 0.0) {
                     aspect = _MainTex_TexelSize.y / _MainTex_TexelSize.x;
                }


                // Center UVs and scale to [-1, 1] range conceptually (based on height)
                // Then apply aspect correction to the X coordinate
                float2 pos = (i.uv - 0.5) * 2.0; // Range [-1, 1] if square
                pos.x *= aspect; // Correct X based on aspect ratio. Y remains [-1, 1]

                // --- Calculations in Corrected Space ---
                // Calculate distance from center in the aspect-corrected space
                // A distance of 1.0 now corresponds to the distance from center to top/bottom edge
                float dist = length(pos);

                // Calculate angle (0 rad = +X axis, PI/2 rad = +Y axis)
                // We want 0 degrees at the top (+Y), so we calculate angle relative to +Y
                float angle = atan2(pos.x, pos.y); // Note: atan2(y,x) gives angle from +X. atan2(x,y) gives angle from +Y.

                // --- Define Arc ---
                // Convert normalized values [0, 1] to angles [0, 2*PI] (clockwise from top)
                // Ensure _Value is always >= _StartValue for arc calculation logic
                float startNorm = frac(_StartValue);
                float range = _Value - _StartValue; // Use original values to detect > 1 turns

                 // Handle near-zero difference edge case slightly differently for SDF
                if (abs(range) < 1e-5) return _BackgroundColor;
                if (range >= (1.0 - 1e-5)) range = 1.0; // Treat near-full circle as full circle

                float startAngle = startNorm * UNITY_TWO_PI;
                float endAngle = startNorm * UNITY_TWO_PI + range * UNITY_TWO_PI; // Use range to potentially exceed 2PI

                // Center and half-width of the angle arc (for SDF)
                float arcCenter = (startAngle + endAngle) * 0.5;
                float arcHalfWidth = (endAngle - startAngle) * 0.5;

                // --- Signed Distance Field (SDF) ---
                // SDF calculation: positive outside the shape, negative inside.

                // 1. Radial distance (distance from the ring shape)
                //    Positive outside the ring band, negative inside
                float dist_radial = max(dist - _OuterRadius, _InnerRadius - dist);

                // 2. Angular distance (distance from the valid arc)
                //    Calculate the shortest angle difference to the center of the arc
                //    wrapAngle ensures we get the shortest path (-PI to PI)
                float deltaAngle = wrapAngle(angle - arcCenter);
                //    Positive outside the arc width, negative inside
                float dist_angular = abs(deltaAngle) - arcHalfWidth;

                // If it's a full circle, ignore angular distance
                if (range >= 1.0) {
                    dist_angular = -1e9; // Effectively infinitely inside angularly
                }

                // Combine radial and angular distances: max() takes the furthest distance *outside* the shape
                float sdf = max(dist_radial, dist_angular);

                // --- Anti-Aliasing using fwidth ---
                // Calculate width of the transition region based on screen-space derivatives
                float aa = fwidth(sdf) * 0.707; // Use 0.707 (~sqrt(2)/2) or 0.5 for smoother AA width

                // Smoothstep for anti-aliasing: transition from 1 (inside) to 0 (outside)
                // sdf < -aa -> 1 (fully inside)
                // sdf > +aa -> 0 (fully outside)
                // smoothstep input goes from aa to -aa as sdf goes from -aa to aa
                float coverage = smoothstep(aa, -aa, sdf);

                // --- Final Color ---
                // Modulate base color by its alpha and the SDF coverage
                float4 finalColor = _Color;
                finalColor.a *= coverage;

                // Lerp between background and final color based on computed alpha
                return lerp(_BackgroundColor, finalColor, finalColor.a);
            }
            ENDCG
        }
    }
    Fallback "Transparent/VertexLit" // Fallback for platforms that don't support the shader
}
