Shader "Hidden/DepthNormalsOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DepthBias ("Depth Bias", float) = 1.0
        _NormalBias ("Normal Bias", float) = 1.0

        _DepthMult ("Depth Multiplier", float) = 1.0
        _NormalMult ("Normal Multiplier", float) = 1.0
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            

            sampler2D _MainTex;
            sampler2D _CameraDepthNormalsTexture;
            float4 _CameraDepthNormalsTexture_TexelSize;

            float _NormalBias;
            float _DepthBias;

            float _NormalMult;
            float _DepthMult;
            //sampler2D _LastCameraDepthNormalsTexture;

            void Compare(inout float depthOutline, inout float normalOutline, float baseDepth,float3 baseNormal, float2 _uv, float2 offset){
                
                float4 neighborSample = tex2D(_CameraDepthNormalsTexture, _uv + _CameraDepthNormalsTexture_TexelSize.xy * offset);
                float neighborDepth;
                float3 neighborNormal;
                DecodeDepthNormal(neighborSample, neighborDepth, neighborNormal);
                neighborDepth = neighborDepth * _ProjectionParams.z;

                depthOutline += abs(baseDepth-neighborDepth);
                
                float3 normalDifference = baseNormal - neighborNormal;
                normalDifference = normalDifference.r + normalDifference.g + normalDifference.b;
                normalOutline = normalOutline + normalDifference;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex,i.uv);
                float4 depthnormalsSample = tex2D(_CameraDepthNormalsTexture,i.uv);
                float3 normal;
                float depth;
                DecodeDepthNormal(depthnormalsSample, depth, normal);
                depth = depth* _ProjectionParams.z;

                float depthDifference = 0.0;
                float normalDifference = 0.0;

                //if(depth > 1000) return col;

                Compare(depthDifference,normalDifference,depth,normal, i.uv, float2(1, 0));
                Compare(depthDifference,normalDifference,depth,normal, i.uv, float2(0, 1));
                Compare(depthDifference,normalDifference,depth,normal, i.uv, float2(0, -1));
                Compare(depthDifference,normalDifference,depth,normal, i.uv, float2(-1, 0));

                
                depthDifference = depthDifference * _DepthMult;
                depthDifference = saturate(depthDifference);
                depthDifference = pow(depthDifference, _DepthBias);

                normalDifference = normalDifference * _NormalMult;
                normalDifference = saturate(normalDifference);
                normalDifference = pow(normalDifference, _NormalBias);

                //return col + depthDifference + normalDifference;
                return lerp(col,fixed4(0,0,0,0),(depthDifference+normalDifference));
            }
            ENDCG
        }
    }
}
