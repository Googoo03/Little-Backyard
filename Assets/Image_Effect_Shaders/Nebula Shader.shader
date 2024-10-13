Shader "Hidden/Nebula Shader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CloudTex ("Cloud Texture", 3D) = "white"{}
        _BlueNoise ("Blue Noise", 2D) = "White"{}

        _NebulaScale ("Nebula Scale", float)  =1
        _NebulaCol ("Nebula Color",Color) = (1,1,1,1)
        _NebulaDensity ("Nebula Density", float)  =1
        _NebulaCoeff ("Nebula Coefficients", Vector) = (0,0,0,0)
        _NebulaThreshold ("Nebula Threshold",float)  = 1
        _DomainWarp ("Domain Warp",float) = 1.0
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
                float4 screenPos : TEXCOORD1;
                float3 viewVector : TEXCOORD2;
                float3 camRelativeWorldPos : TEXCOORD3;
                float3 worldNormal : TEXCOORD4;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata_base v)
            {
                v2f o;

                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz);

                o.worldNormal = worldNormal;
                o.vertex = UnityObjectToClipPos(v.vertex);
                //o.uv = v.uv;

                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = (o.screenPos.xy/o.screenPos.w);

                float3 viewVector = mul(unity_CameraInvProjection, float4((o.screenPos.xy/o.screenPos.w) * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                o.camRelativeWorldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz - _WorldSpaceCameraPos;
                
                return o;
            }

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            sampler2D _LastCameraDepthTexture;
            sampler3D _CloudTex;
            sampler2D _BlueNoise;
            float4 _BlueNoise_ST;
            float4 _CloudTex_ST;
            float _DomainWarp;

            //Nebula PARAMETERS
            float _NebulaScale;
            fixed4 _NebulaCol;
            float _NebulaDensity;
            float4 _NebulaCoeff;
            float _NebulaThreshold;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 noColor = tex2D(_MainTex, i.uv);
                float blueNoise = tex2D(_BlueNoise,i.uv).r * 5 / 25.0;

                //SAMPLE DEPTH TEXTURE-------------------------------------------
                 float terrainLevel = tex2D(_CameraDepthTexture,i.uv);
                terrainLevel = LinearEyeDepth(terrainLevel);

                //water DEPTH
                /*float waterDepthTextureSample = tex2D(_LastCameraDepthTexture,i.uv);
                float3 waterNormal;
                float waterLevel = LinearEyeDepth(waterDepthTextureSample);
                */
                //---------------------------------------------------------------



                //CALCULATES WORLD POSITION AS OPPOSED TO DEPTH------------------
                float3 viewDirection = normalize(i.viewVector);
                const float3 ray_direction = normalize(viewDirection);

                float3 world_ray = normalize(UnityObjectToWorldDir(viewDirection));

                float3 cam_forward_world = mul((float3x3)unity_CameraToWorld, float3(0,0,1));
                float ray_depth_world = dot(cam_forward_world, ray_direction);

                float3 terrainPosition = (ray_direction / ray_depth_world) * (terrainLevel) +  _WorldSpaceCameraPos;
                
                ///--------------------------------------------------------------

                //INCORPORATE NEBULAE HERE
                float tNebula = 0.0;
                float3 farPlanePosition = (ray_direction / ray_depth_world) * 100 +  _WorldSpaceCameraPos;
                bool returnCondNeb = false;
                float3 intersectionLineNeb;
                float densityNeb = 0.0;
                float nebR = tex3D(_CloudTex,_WorldSpaceCameraPos * (0.5/_NebulaScale) ).r;
                float nebG = tex3D(_CloudTex,float3(0.5,0.5,0.5)+_WorldSpaceCameraPos * (0.5/_NebulaScale) ).r;
                
                float nebB = tex3D(_CloudTex,float3(0.6,0.2,0.8)+_WorldSpaceCameraPos * (0.5/_NebulaScale) ).r;
                

                nebR = (1-nebR)*(1-nebR);
                nebB = (1-nebB)*(1-nebB);
                nebG = (1-nebG)*(1-nebG);

                float sum = nebR+nebG+nebB;
                nebR/=sum;
                nebG/=sum;
                nebB/=sum;
                _NebulaCol = float4(nebR,nebG,nebB,1);

                fixed4 nebulaSample;
                float nebula; //sample with applied coefficients
                if(terrainLevel > 100){
                    for(;tNebula < 1.0; tNebula+= 0.1){
                        //FIX THE NORMALIZE LOCATION. RIGHT NOW TCLOUD DOESNT DO ANYTHING
                    
                        intersectionLineNeb = (farPlanePosition-_WorldSpaceCameraPos)*((tNebula*5)+blueNoise) + _WorldSpaceCameraPos;
                        float3 domainWarp = tex3D(_CloudTex,((intersectionLineNeb * (1.0/_NebulaScale))*2)).xyz * _DomainWarp;
                        nebulaSample = tex3D(_CloudTex,(intersectionLineNeb * (1.0/_NebulaScale))+domainWarp );
                        nebula = (nebulaSample.r*_NebulaCoeff.x) + (nebulaSample.g*_NebulaCoeff.y) + (nebulaSample.b*_NebulaCoeff.z);
                           
                        
                        densityNeb += ( nebula > _NebulaThreshold) ? nebula : 0;
                        //densityNeb += nebula;
                    }
                    returnCondNeb = densityNeb > 0.1 ? true : false;
                        
                    densityNeb /= 10.0 * (1.0/_NebulaDensity);
                    

                    noColor = returnCondNeb ? lerp(_NebulaCol,noColor,exp(-densityNeb) ) : noColor;
                    
                }
                return noColor;
            }
            ENDCG
        }
    }
}
