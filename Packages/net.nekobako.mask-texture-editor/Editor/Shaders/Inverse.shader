Shader "Hidden/MaskTextureEditor/Inverse"
{
    Properties
    {
        _ColorMask("Color Mask", Int) = 15
        _MainTex("Texture", 2D) = "white" {}
        _SelectionMask("Selection Mask", 2D) = "white" {}
        _UseSelectionMask("Use Selection Mask", Float) = 0.0
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

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);
                float4 inverse = float4(1.0 - color.rgb, color.a);
                float selection = lerp(1.0, tex2D(_SelectionMask, i.uv).r, _UseSelectionMask);
                return lerp(color, inverse, selection);
            }
            ENDCG
        }
    }
}
