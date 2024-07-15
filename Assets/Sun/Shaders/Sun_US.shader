Shader "Custom/Sun_US"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorA ("Color_A", Color) = (1,1,1,1)
        _ColorB ("Color_B", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 viewVector : TEXCOORD2;
                float3 worldNormal : TEXCOORD4;
                float4 vertex : SV_POSITION;
            };

            float4 _ColorA;
            float4 _ColorB;
            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata_base v)
            {
                v2f o;

                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz);

                o.worldNormal = worldNormal;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = v.texcoord.xy;

                float3 viewVector = mul(unity_CameraInvProjection, float4((o.screenPos.xy/o.screenPos.w) * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = lerp(_ColorA,_ColorB,tex2D(_MainTex,i.uv).r);

                return col;
            }
            ENDCG
        }
    }
}
