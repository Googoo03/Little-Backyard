Shader "Unlit/Ocean_Unlit_Shader"
{
    Properties
    {
       // _DepthCoef ("Depth Coefficient", float) = 0.0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType"="Transparent" }
        ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

        LOD 100

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            

            #include "UnityCG.cginc"

            

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 viewVector : TEXCOORD2;
                float3 worldNormal : TEXCOORD4;
                float4 vertex : SV_POSITION;
            };

            sampler2D _CameraDepthTexture;
            float _DepthCoef;

            float4 SHALLOW;
            float4 DEEP;

            v2f vert (appdata_base v)
            {
                v2f o;

                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz);

                o.worldNormal = worldNormal;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);

                float3 viewVector = mul(unity_CameraInvProjection, float4((o.screenPos.xy/o.screenPos.w) * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));

                return o;
            }

            

            fixed4 frag (v2f i) : SV_Target
            {
                float3 viewDirection = normalize(i.viewVector);


                float depthTextureSample = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, i.screenPos);
                float terrainLevel = LinearEyeDepth(depthTextureSample);

                float waterLevel = i.screenPos.w;
                float waterDepth = (terrainLevel - waterLevel);

                float3 waterColor = lerp(SHALLOW,DEEP,1 - exp(-waterDepth*_DepthCoef));


                float alpha = max(1-dot(-viewDirection, i.worldNormal),0.5);

                return float4(waterColor,alpha);

            }
            ENDCG
        }
    }
}
