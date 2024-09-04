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