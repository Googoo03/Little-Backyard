Shader "Custom/Water Depth Replacement"
{
	SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            sampler2D _CameraDepthTexture;

            struct v2f
            {
                //float4 screenPos : TEXCOORD1;
                //float4 vertex : SV_POSITION;
                V2F_SHADOW_CASTER;
            };

            v2f vert(appdata_tan v)
            {
                v2f o;

                //Courtesy of Sebastian Lague 2024
                float3 worldPos =  mul(unity_ObjectToWorld,v.vertex).xyz;
                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz);

				//float3 worldPos =  mul(unity_ObjectToWorld,v.vertex).xyz;

				float vertexAnimWeight = length(worldPos - _WorldSpaceCameraPos);
				vertexAnimWeight = 1;//saturate(pow(vertexAnimWeight / 30, 3));

				float waveAnimDetail = 100;
				float maxWaveAmplitude = 0.001* vertexAnimWeight; // 0.001
				float waveAnimSpeed = 1;

				//float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz);
				float theta = acos(worldNormal.z);
				float phi = sin(v.vertex.y);//atan2(v.vertex.y, v.vertex.x);
				float waveA = sin(_Time.x * 3.5*waveAnimSpeed + theta * waveAnimDetail);
				float waveB = sin(_Time.y * waveAnimSpeed + phi * waveAnimDetail);
				float waveVertexAmplitude = (waveA + waveB) * maxWaveAmplitude;
				v.vertex += float4(v.normal, 0) * waveVertexAmplitude;
                TRANSFER_SHADOW_CASTER(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                /*float depthTextureSample = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                depthTextureSample = LinearEyeDepth(depthTextureSample);
                return float4(depthTextureSample,depthTextureSample,depthTextureSample,1);
                */
                //return float4(1,0,0,1);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}