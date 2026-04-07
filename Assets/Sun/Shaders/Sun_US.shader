Shader "Custom/Sun_US"
{
    Properties
    {
        _ColorA ("Color_A", Color) = (1,1,1,1)
        _Color ("Color_B", Color) = (1,1,1,1)
        _Blowout ("Sun Blowout", float) = 1
        _Threshold ("Threshold", int) = 1.0
        _Displacement ("Displacement", float) = 1
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
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 viewVector : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                float3 worldNormal : TEXCOORD4;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 _ColorA;
            float4 _Color;
            float _Threshold;
            float _Displacement;
            float _Blowout;

            UNITY_INSTANCING_BUFFER_START(Props)
                // per-instance properties go here (optional)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_OUTPUT(v2f, o);

                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(UNITY_MATRIX_M, v.vertex).xyz;

                o.worldNormal = worldNormal;
                o.worldPos = worldPos;

                o.vertex = UnityObjectToClipPos(v.vertex);
                
                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = v.uv;

                float3 viewVector = mul(unity_CameraInvProjection, float4((o.screenPos.xy/o.screenPos.w) * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                
                UNITY_SETUP_INSTANCE_ID(i);
                // sample the texture
                

                float fresnel = dot(i.worldNormal,normalize(_WorldSpaceCameraPos-i.worldPos));
                fresnel = max(fresnel, 0.01);

                fixed4 col = _Color*.05;
                col /= pow(1-fresnel,_Blowout);

                return col;
            }
            ENDCG
        }
    }
}
