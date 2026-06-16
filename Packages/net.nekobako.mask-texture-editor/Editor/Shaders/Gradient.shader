Shader "Hidden/MaskTextureEditor/Gradient"
{
    Properties
    {
        _ColorMask("Color Mask", Int) = 15
        _MainTex("Texture", 2D) = "white" {}
        _SelectionMask("Selection Mask", 2D) = "black" {}
        _UseSelectionMask("Use Selection Mask", Float) = 0.0
        _StartPoint("Start Point", Vector) = (0.0, 0.0, 0.0, 0.0)
        _EndPoint("End Point", Vector) = (1.0, 0.0, 0.0, 0.0)
        _StartValue("Start Value", Float) = 0.0
        _EndValue("End Value", Float) = 1.0
        _CurveExponent("Curve Exponent", Float) = 1.0
        _TextureSize("Texture Size", Vector) = (1.0, 1.0, 0.0, 0.0)
        _GradientShape("Gradient Shape", Int) = 0
        _GradientWidth("Gradient Width", Float) = 128.0
        _GradientFeather("Gradient Feather", Float) = 32.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }

        Pass
        {
            ColorMask [_ColorMask]

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _SelectionMask;
            float _UseSelectionMask;
            float2 _StartPoint;
            float2 _EndPoint;
            float _StartValue;
            float _EndValue;
            float _CurveExponent;
            float2 _TextureSize;
            int _GradientShape;
            float _GradientWidth;
            float _GradientFeather;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 pixel = i.uv * _TextureSize;
                float2 start = _StartPoint * _TextureSize;
                float2 end = _EndPoint * _TextureSize;
                float2 direction = end - start;
                float lengthSquared = max(dot(direction, direction), 1e-8);
                float rawT = dot(pixel - start, direction) / lengthSquared;
                float t = saturate(rawT);
                float shapeMask = 1.0;
                if (_GradientShape == 1)
                {
                    float2 closest = start + direction * t;
                    float distanceToBand = length(pixel - closest);
                    float halfWidth = max(_GradientWidth * 0.5, 0.0);
                    float feather = max(_GradientFeather, 1e-4);
                    float segmentMask = step(0.0, rawT) * step(rawT, 1.0);
                    shapeMask = (1.0 - smoothstep(halfWidth, halfWidth + feather, distanceToBand)) * segmentMask;
                }
                else if (_GradientShape == 2)
                {
                    float radius = max(length(direction), 1e-4);
                    float distanceToCenter = length(pixel - start);
                    t = saturate(distanceToCenter / radius);
                    float feather = max(_GradientFeather, 1e-4);
                    shapeMask = 1.0 - smoothstep(radius, radius + feather, distanceToCenter);
                }
                float exponent = max(_CurveExponent, 1e-4);
                float curveStart = pow(t, exponent);
                float curveEnd = pow(1.0 - t, exponent);
                t = curveStart / max(curveStart + curveEnd, 1e-6);
                float value = lerp(_StartValue, _EndValue, t);
                float selection = lerp(1.0, tex2D(_SelectionMask, i.uv).r, _UseSelectionMask) * shapeMask;
                float strength = saturate(value) * selection;
                return lerp(tex2D(_MainTex, i.uv), float4(1.0, 1.0, 1.0, 1.0), strength);
            }
            ENDCG
        }
    }
}
