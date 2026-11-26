Shader "Hidden/MaskTextureEditor/Fill"
{
    Properties
    {
        [HideInInspector] _MainTex("Mask", 2D) = "black" {}
        [HideInInspector] _OriginalTex("Original", 2D) = "black" {}
        _ColorMask("Color Mask", Int) = 15
        _Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Fill"
            ColorMask [_ColorMask]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 _Color;

            float4 vert(float4 v : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(v);
            }

            float4 frag() : SV_Target
            {
                return _Color;
            }
            ENDCG
        }

        Pass
        {
            Name "Occlusion"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _OriginalTex;
            float4 _MainTex_TexelSize;
            float4 _Color;
            float4 _PaintChannels;

            float4 frag(v2f_img i) : SV_Target
            {
                float coverage = 1;
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 uv = i.uv + float2(x, y) * abs(_MainTex_TexelSize.xy);
                        float covered = all(uv >= 0) && all(uv <= 1) ? step(0.5, tex2D(_MainTex, uv).r) : 0;
                        coverage = min(coverage, covered);
                    }
                }
                return lerp(tex2D(_OriginalTex, i.uv), _Color, coverage * _PaintChannels);
            }
            ENDCG
        }
    }
}
