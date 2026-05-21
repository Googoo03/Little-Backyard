Shader "Unlit/Star"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Blowout ("Blowout", float) = 1.0
        _Intensity("Intensity", float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            StructuredBuffer<float4x4> _Matrices;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD3;
                float3 worldNormal : TEXCOORD4;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Intensity;
            float _Blowout;

            v2f vert (appdata v)
            {
                v2f o;

                float4x4 model = _Matrices[v.instanceID];
                float4 world = mul(model, v.vertex);

                o.vertex = mul(UNITY_MATRIX_VP, world);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                float3 worldPos = mul(UNITY_MATRIX_M, v.vertex).xyz;
                float3 worldNormal = normalize(mul((float3x3)model,v.normal));

                o.worldNormal = worldNormal;
                o.worldPos = world.xyz;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float fresnel = dot(i.worldNormal,normalize(-i.worldPos));
                fresnel = max(fresnel, 0.01);
                float intensity = _Intensity * exp(-length(i.worldPos) / 5000000);
                fixed4 col = _Color*intensity;
                col /= max(0.01,pow(1-fresnel,_Blowout));
                col = float4(1,1,1,1);
                return col;
            }
            ENDCG
        }
    }
}
