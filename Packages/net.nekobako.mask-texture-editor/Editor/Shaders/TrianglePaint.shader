Shader "Hidden/MaskTextureEditor/TrianglePaint"
{
    Properties
    {
        _ColorMask("Color Mask", Int) = 15
        _MainTex("Texture", 2D) = "white" {}
        _TriangleMask("Triangle Mask", 2D) = "black" {}
        _BrushStrength("Brush Strength", Float) = 1.0
        _BrushColor("Brush Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
        }

        Pass
        {
            Cull Off
            ZTest Always
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            float4 frag() : SV_Target
            {
                return 1.0;
            }
            ENDCG
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
            sampler2D _TriangleMask;
            float _BrushStrength;
            float4 _BrushColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float strength = tex2D(_TriangleMask, i.uv).r * _BrushStrength;
                return lerp(tex2D(_MainTex, i.uv), _BrushColor, strength);
            }
            ENDCG
        }
    }
}
