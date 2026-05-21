Shader "Custom/FlatSurfaceShader"
{
    Properties
    {
        _MainTex ("Albedo", 2DArray) = "white" {}
        _SideTex ("Side Texture", 2D) = "white" {}
        _AmbientColor ("Ambient Color", Color) = (0.2,0.2,0.2,1)
        _SlopeThreshold ("Slope Threshold", Range(-1,2)) = 0.5
        _Bands ("Bands",int) = 5
        _LightWrap ("Light Wrap", Range(0,1)) = 0.5
        _DirToSun ("Direction To Sun", Vector) = (0,1,0)
        _LightColor ("Light Color", Color) = (1,1,1,1)
        _BlendOffset  ("Blend Offset",float) = 1.0
        _BlendExponent("Blend Exponent",float) = 1.0
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

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            sampler2D _SideTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = o.worldPos;
                o.worldNormal = v.normal;
                o.color = v.color;
                return o;
            }

            //Triplanar Parameters
            float _BlendOffset;
            float _BlendExponent;

            float _Bands;
            float4 _AmbientColor;
            float _LightWrap;
            float3 _LightColor;
            float _SlopeThreshold;
            float3 _DirToSun;
            float3 planetCentre;

            float3 BlendWeights(float3 normal)
            {
                float3 b = abs(normal);
                b = saturate(b - _BlendOffset);
                b = pow(b, _BlendExponent);
                return b / (b.x + b.y + b.z + 1e-5);
            }

            float3 SampleTriplanar(float3 uv, float3 localPos, float index, float3 weights)
            {
                float3 x = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(uv.yz - localPos.yz,index )).rgb;
                float3 y = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(uv.xz - localPos.xz,index )).rgb;
                float3 z = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(uv.xy - localPos.xy,index )).rgb;

                return x * weights.x + y * weights.y + z * weights.z;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 worldPos = i.worldPos;
                float eps = 1e-9;

                float3 worldN = i.worldNormal;//normalize(cross(dpdx, dpdy));

                // Get main directional light direction (world space)
                

                float NdotL = dot(worldN, _DirToSun);
                NdotL = saturate(NdotL * (1.0 - _LightWrap) + _LightWrap);
                NdotL = floor(NdotL * (_Bands-1)) / _Bands;
                NdotL += _AmbientColor.r;

                //float3 voxelTexture = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.uv - planetCentre.xy,i.color.a - eps )).rgb;
                float3 triplanarTexture = SampleTriplanar(i.uv,planetCentre,i.color.a - eps,BlendWeights(normalize(worldN)));

                float3 albedo = triplanarTexture;

                //float3 diffuse = albedo * _LightColor.rgb * NdotL;
                float3 color = albedo * _LightColor.rgb * NdotL;
                
                return float4(color, 1);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}