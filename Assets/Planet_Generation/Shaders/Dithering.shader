Shader "Custom/Dithering"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Bands ("Num of Bands", int) = 1
        _DitherStrength ("Dither Strength", float) = 1.0
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            int _Bands;
            float _DitherStrength;

            fixed4 frag (v2f i) : SV_Target
            {
                int4x4 dither = int4x4(
                    -4, 0, -3, 1,
                    2, -2, 3, -1,
                    -3, 1, -4, 0,
                    3, -1, 2, -2
                );

                int column = ((int)(i.uv.x * 320)) % 4;
                
                int row = (int)((i.uv.y * 223)) % 4;
                
                float dither_val = (float)dither[column][row] / _Bands;

                fixed4 col = tex2D(_MainTex, i.uv);
                
                col.rgb *= _Bands-1;
                col.rgb += (float)dither_val * _DitherStrength;
                col.rgb = int3(col.rgb);
                col.rgb /= (_Bands);
                


                
                col.rgb = clamp(col.rgb, 0.0, _Bands - 1);
                
                return col;
            }
            ENDCG
        }
    }
}
