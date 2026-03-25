Shader "Custom/ScreenSpaceDerivative_WorldSpace"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _SideTex ("Side Texture", 2D) = "white" {}
        _AmbientColor ("Ambient Color", Color) = (0.2,0.2,0.2,1)
        _SlopeThreshold ("Slope Threshold", Range(-1,2)) = 0.5
        _Bands ("Bands",int) = 5
        _LightWrap ("Light Wrap", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Back

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            sampler2D _SideTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float _Bands;
            float4 _AmbientColor;
            float _LightWrap;
            float _SlopeThreshold;

            fixed4 frag(v2f i) : SV_Target
            {
                float3 worldPos = i.worldPos;

                // Screen-space derivative normal (world space)
                float3 dpdx = ddx(worldPos);
                float3 dpdy = ddy(worldPos);
                float3 worldN = normalize(cross(dpdx, dpdy));

                // Get main directional light direction (world space)
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);

                float NdotL = dot(worldN, lightDir);
                NdotL = saturate(NdotL * (1.0 - _LightWrap) + _LightWrap);
                NdotL = floor(NdotL * (_Bands-1)) / _Bands;
                NdotL += _AmbientColor.r;
                
                float3 radialDir = normalize(worldPos);

                
                float slope = dot(-worldN, radialDir);
                slope = saturate(slope - _SlopeThreshold);
                slope = pow(slope, 10);
                if(slope - 0.5 < 0.2) slope = 0;
                slope = slope > 0.5 ? 1 : 0;


                float3 albedo = tex2D(_MainTex, i.uv).rgb;
                float3 side = tex2D(_SideTex, i.uv).rgb;

                albedo = lerp(side,albedo,slope);

                float3 diffuse = albedo * _LightColor0.rgb * NdotL;
                float3 color = albedo * NdotL * _LightColor0.rgb;

                //return float4(slope,slope,slope, 1);
                return float4(color, 1);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}