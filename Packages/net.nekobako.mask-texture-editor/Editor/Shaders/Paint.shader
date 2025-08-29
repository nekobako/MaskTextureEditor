Shader "Hidden/MaskTextureEditor/Paint"
{
    Properties
    {
        _ColorMask("Color Mask", Int) = 15
        _MainTex("Texture", 2D) = "white" {}
        _BrushSize("Brush Size", Float) = 100.0
        _BrushHardness("Brush Hardness", Float) = 1.0
        _BrushStrength("Brush Strength", Float) = 1.0
        _BrushColor("Brush Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BrushPosition("Brush Position", Vector) = (0.5, 0.5, 0.0, 0.0)
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
            float4 _MainTex_TexelSize;

            float _BrushSize;
            float _BrushHardness;
            float _BrushStrength;
            float4 _BrushColor;
            float2 _BrushPosition;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r = _BrushSize * _MainTex_TexelSize.xy * 0.5;
                float d = distance(i.uv, _BrushPosition.xy * _MainTex_TexelSize.xy);
                return lerp(tex2D(_MainTex, i.uv), _BrushColor, (1.0 - smoothstep(r * _BrushHardness, r, d)) * _BrushStrength);
            }
            ENDCG
        }
    }
}
