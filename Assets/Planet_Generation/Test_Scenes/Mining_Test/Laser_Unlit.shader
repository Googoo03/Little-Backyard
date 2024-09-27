Shader "Unlit/Laser_Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Haze ("Haze", 2D) = "white" {}
        _Perlin ("Perlin Noise", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Threshold ("Noise Threshold", float) = 1.0

        _AnimSpeed ("Animation Speed", float) = 1.0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha 

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work

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

            sampler2D _MainTex;
            sampler2D _Perlin;
            float4 _Perlin_ST;
            float4 _MainTex_ST;

            sampler2D _Haze;

            float4 _Color;

            float _Threshold;

            float _AnimSpeed;

            v2f vert (appdata_base v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                //o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 perlin = tex2D(_Perlin, (i.uv - float2(_Time.x*_AnimSpeed,0)) * float2(_Perlin_ST.x, _Perlin_ST.y) );
                fixed4 haze = tex2D(_Haze,i.uv) * _Color;

                perlin = perlin.r > _Threshold ? float4(1,1,1,perlin.r): float4(0,0,0,0);

                //haze += perlin;
                fixed4 blowout = tex2D(_MainTex, (i.uv - float2(_Time.x*_AnimSpeed,0))  * _MainTex_ST );

                fixed4 col = haze+blowout;
                //col = haze;

                return col;
            }
            ENDCG
        }
    }
}
