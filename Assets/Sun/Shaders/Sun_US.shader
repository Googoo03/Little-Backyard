Shader "Custom/Sun_US"
{
    Properties
    {
        _NoiseTex ("Texture", 3D) = "white" {}
        _ColorA ("Color_A", Color) = (1,1,1,1)
        _ColorB ("Color_B", Color) = (1,1,1,1)
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
                float3 worldPos : TEXCOORD3;
                float3 worldNormal : TEXCOORD4;
                float4 vertex : SV_POSITION;
            };

            float4 _ColorA;
            float4 _ColorB;
            sampler3D _NoiseTex;
            float4 _NoiseTex_ST;
            float _Threshold;
            float _Displacement;

            v2f vert (appdata_base v)
            {
                v2f o;

                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz);
                float3 worldPos = mul (unity_ObjectToWorld, v.vertex).xyz;

                o.worldNormal = worldNormal;
                o.worldPos = worldPos;

                _NoiseTex_ST.xy += float2(_Time.x,_Time.x);
                v.vertex *= 1-(tex3Dlod(_NoiseTex,float4(worldNormal,0)+_NoiseTex_ST).r * _Displacement);
                o.vertex = UnityObjectToClipPos(v.vertex);/* * tex3D(_NoiseTex,worldNormal).r;*/
                
                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = v.texcoord.xy;

                float3 viewVector = mul(unity_CameraInvProjection, float4((o.screenPos.xy/o.screenPos.w) * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                
                // sample the texture
                _NoiseTex_ST.z += unity_DeltaTime;
                float2 texture_offset = float2(_Time.xx);
                int noiseValue = tex3D(_NoiseTex,i.worldNormal).b;
                //float noiseV = .1 * noiseValue;

                float fresnel = dot(i.worldNormal,normalize(_WorldSpaceCameraPos-i.worldPos));

                fixed4 col = lerp(_ColorA,_ColorB,exp(-noiseValue*fresnel));
                //col.xyz *= unity_DeltaTime.z*100;

                return col;
            }
            ENDCG
        }
    }
}
