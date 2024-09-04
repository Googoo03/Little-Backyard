

Shader "Custom/Atmosphere_IE"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CloudTex ("Cloud Texture", 3D) = "white"{}
        _BlueNoise ("Blue Noise", 2D) = "White"{}

        _CloudColor ("Cloud Color", Color) = (1,1,1,1)
        _Threshold ("Cloud Threshold", float) = 1
        _ThresholdG ("Cloud Threshold G", float) = 1
        _CloudDensity ("Cloud Density Coeff",float) = 1
        _CloudFalloff ("CLoud Falloff Coeff",float) = 1
        _CloudCoeff ("Cloud Coefficients",Vector) = (0,0,0,0)

        _PlanetPos ("Planet Position", Vector) = (0,0,0,0)
        _SunPos ("Sun Position", Vector) = (0,0,0,0)
        _Color ("Atmosphere Color", Color) = (1,1,1,1)
        _Color2 ("Sunset Color", Color) = (1,1,1,1)
        _Radius ("Planet Radius", float) = 5
        _AtmosphereHeight ("Atmosphere Height", float) = 0.5
        _Density ("Atmosphere Density", float) = 1

        _Samples ("Number of Samples", int) = 1

        _NebulaScale ("Nebula Scale", float)  =1
        _NebulaCol ("Nebula Color",Color) = (1,1,1,1)
        _NebulaDensity ("Nebula Density", float)  =1
        _NebulaCoeff ("Nebula Coefficients", Vector) = (0,0,0,0)
        _NebulaThreshold ("Nebula Threshold",float)  = 1

        _OceanRad ("Ocean Radius", float) = 1
        _Ocean ("Ocean Present", int) = 1
        _OceanCol ("Ocean Color", Color) = (1,1,1,1)

        _Generate ("Generate",int) = 0
    }
    SubShader
    {
        // No culling or depth
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

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

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

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
            sampler2D _LastCameraDepthTexture;
            
            float3 _PlanetPos;
            float3 _SunPos;
            float _Radius;
            float _AtmosphereHeight;
            float _Density;
            float4 _Color;
            float4 _Color2;

            float distanceToPlanet;

            int _Generate;

            //CLOUD PARAMETERS
            float _Threshold;
            float _ThresholdG;
            float _Samples;
            sampler3D _CloudTex;
            sampler2D _BlueNoise;
            float4 _CloudTex_ST;
            float _CloudDensity;
            float _CloudFalloff;
            float4 _CloudColor;
            float4 _CloudCoeff;

            //Nebula PARAMETERS
            float _NebulaScale;
            fixed4 _NebulaCol;
            float _NebulaDensity;
            float4 _NebulaCoeff;
            float _NebulaThreshold;

            //Ocean PARAMETERS
            float _OceanRad;
            int _Ocean;
            float4 _OceanCol;



            float QuadraticSolve(float a, float b, float c, bool plus){
                return plus ? ( (-b + sqrt(b*b - 4*a*c)) / 2*a) : ( (-b - sqrt(b*b - 4*a*c)) / 2*a);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                
                float3 viewDirection = normalize(i.viewVector);
                //set vector from camera to planet and get magnitude of said vector
                float3 cameraToPlanetVector =  _PlanetPos - _WorldSpaceCameraPos;
                float cameraDistanceToPlanet = length(cameraToPlanetVector);
                

                float depthTextureSample = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float terrainLevel = LinearEyeDepth(depthTextureSample);


                //water DEPTH
                float waterDepthTextureSample = SAMPLE_DEPTH_TEXTURE(_LastCameraDepthTexture, i.uv);
                float waterLevel = LinearEyeDepth(waterDepthTextureSample);

                 //CALCULATES WORLD POSITION AS OPPOSED TO DEPTH
                 const float3 ray_direction = normalize(viewDirection);

                float3 world_ray = normalize(UnityObjectToWorldDir(viewDirection));

                float3 cam_forward_world = mul((float3x3)unity_CameraToWorld, float3(0,0,1));
                float ray_depth_world = dot(cam_forward_world, ray_direction);

                float3 terrainPosition = (ray_direction / ray_depth_world) * min(terrainLevel,waterLevel) +  _WorldSpaceCameraPos;
                //float3 waterPosition = (ray_direction / ray_depth_world) * waterLevel +  _WorldSpaceCameraPos;
                ///////////////////////////////////////////////

                

                fixed4 noColor = tex2D(_MainTex, i.uv);
                float blueNoise = tex2D(_BlueNoise,i.uv).r * 5 / 25.0;

                

                //INCORPORATE NEBULAE HERE
                float tNebula = 1.0;
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
                    for(;tNebula > 0.1; tNebula-= 1.0/25.0){
                        //FIX THE NORMALIZE LOCATION. RIGHT NOW TCLOUD DOESNT DO ANYTHING
                    
                        intersectionLineNeb = (farPlanePosition-_WorldSpaceCameraPos)*(tNebula+blueNoise) + _WorldSpaceCameraPos;
                            
                        nebulaSample = tex3D(_CloudTex,intersectionLineNeb * (1.0/_NebulaScale) );
                        nebula = (nebulaSample.r*_NebulaCoeff.x) + (nebulaSample.g*_NebulaCoeff.y) + (nebulaSample.b*_NebulaCoeff.z);
                           
                        
                        densityNeb += ( nebula > _NebulaThreshold) ? nebula : 0;
                        //densityNeb += nebula;
                    }
                    returnCondNeb = densityNeb > 0.1 ? true : false;
                        
                    densityNeb /= 25.0 * (1.0/_NebulaDensity);
                    

                    if(returnCondNeb){
                                    //col = fixed4(1,1,1,1);
                        noColor = lerp(_NebulaCol,noColor,exp(-densityNeb) );//fixed4(1,1,1,1);
                    }
                }
               
                float atmosphereAlpha = 1;
                

                if(_Generate==0) return noColor;

                /////////ONLY SHOW WHERE INTERSECTS WITH LIGHT
                float r = _Radius + _AtmosphereHeight;
                float3 Q = _WorldSpaceCameraPos -  _PlanetPos;
                float a = 1;//viewDirection * viewDirection;
                float b = 2 * dot(viewDirection, Q);
                float c = dot(Q,Q) - r*r;
                float d = (dot(viewDirection,Q)*dot(viewDirection,Q)) - c;

                float t1 = -1;
                float t2 = -1;
                
                float dotProduct = 0;
                float atmosphere_depth = 0;
                float distanceAlpha = 1.0/ (length(_WorldSpaceCameraPos-_PlanetPos) / r);

                fixed4 col;

                if(d >= 0){
                    t1 = QuadraticSolve(a,b,c,false);
                    t2 = QuadraticSolve(a,b,c,true);

                    if(t1 >= 0 || t2 >=0){

                        float t3;// = t1 >= 0 ? t1 : t2;
                        if(t1 > 0 && t2 > 0){
                            t3 = min(t1,t2);
                        }else { t3 = t1 >= 0 ? t1 : t2;}
                        float t4 = t3 == t1 ? t2 : t1;

                        float3 intersectionPoint = _WorldSpaceCameraPos + (t3*viewDirection);
                        distanceToPlanet = length(intersectionPoint-_PlanetPos);
                        //atmosphereAlpha *= 1 - saturate((distanceToPlanet - _Radius) / _AtmosphereHeight);
                        //atmosphere_depth = clamp(terrainLevel - intersection_atmosphere_scalar,0,_AtmosphereHeight);

                    
                    
                        float3 start_point;
                        float3 end_point;

                        //throw out if outside the circle and terrain is in front
                        if(length(terrainPosition-_WorldSpaceCameraPos) < t3 && t1>0 && t2>0) return noColor;

                        start_point = t3 == t1 ? intersectionPoint : _WorldSpaceCameraPos;

                        float3 viewPlane = i.camRelativeWorldPos.xyz / dot(i.camRelativeWorldPos.xyz, unity_WorldToCamera._m20_m21_m22);
 
                        // calculate the world position
                        // multiply the view plane by the linear depth to get the camera relative world space position
                        // add the world space camera position to get the world space position from the depth texture
                        float3 terrainworldPos = viewPlane * terrainLevel + _WorldSpaceCameraPos;

                        end_point = t2 > (min(terrainLevel,waterLevel)) ? terrainPosition : _WorldSpaceCameraPos+(t2*viewDirection);

                        
                    
                        float3 normalVector = normalize(end_point -  _PlanetPos); 
                        float3 lightVector = normalize(_SunPos -  _PlanetPos);
                        dotProduct = dot(lightVector,normalVector);

                        atmosphere_depth = length(end_point-start_point);
                        atmosphere_depth = atmosphere_depth*atmosphere_depth;

                         fixed4 atmosphereColor = lerp(_Color,_Color2,max(0,dot(lightVector,viewDirection) * saturate(atmosphere_depth) ));
                         
                        atmosphereAlpha *= max(0,dotProduct);
                        //atmosphereAlpha /= cloudColor.b;


                        col = lerp( fixed4(atmosphereColor.xyz,atmosphereAlpha),noColor,exp(-atmosphere_depth * _Density*atmosphereAlpha*distanceAlpha) );

                        //Calculate cloudcolor
                        _CloudTex_ST.x += _Time.x / 10.0;

                        
                        float tCloud = 5.0;
                        float3 intersectionLine;
                        fixed4 cloudSample;
                        
                        int iterator = 0;
                        bool returnCond = false;
                        //float cloudNormal = 1;
                        float density = 0.0;
                        float cloud;
                        
                        float blueNoise = tex2D(_BlueNoise,i.uv).r * 5 / 25.0;
                        float3 startCloud = end_point;

                        for(;tCloud > 0.0; tCloud -= 1.0/25.0){
                            //FIX THE NORMALIZE LOCATION. RIGHT NOW TCLOUD DOESNT DO ANYTHING
                            iterator++;
                            intersectionLine = normalize(ray_direction)*(tCloud+blueNoise) + start_point;
                            
                            float3 uvCoords = (intersectionLine-_PlanetPos) / r;
                            float atmosphereLength = length(uvCoords-float3(0,0,0));
                            cloudSample = atmosphereLength < (1.0) && atmosphereLength > (_Radius/r) && length(terrainPosition-_WorldSpaceCameraPos)> length(intersectionLine-_WorldSpaceCameraPos) ? tex3D(_CloudTex,uvCoords+_CloudTex_ST) : fixed4(0,0,0,0);
                            
                            cloud = (cloudSample.r*_CloudCoeff.x) + (cloudSample.g*_CloudCoeff.y) + (cloudSample.b*_CloudCoeff.z);

                            
                            //changes cloud opacity based on the height its at
                            float densityModifier = min(length(intersectionLine-_PlanetPos)-_Radius,r-length(intersectionLine-_PlanetPos))*_CloudFalloff;
                            //compute gradient, then calculate normal?
                            //cloudNormal = (cloudSample.r + cloudSample.g > _Threshold) ? dot(normalize(intersectionLine-_SunPos),normalize(intersectionLine-_PlanetPos)) : cloudNormal;
                            if(density == 0.0 && ( cloud > _Threshold) && start_point.x == end_point.x) startCloud = intersectionLine;
                            density += ( cloud > _Threshold) ? cloud*_CloudFalloff : 0;
                        }


                        returnCond = density > 0.1 ? true : false;
                        
                        density /= iterator* (1.0/_CloudDensity);
                        float cloudAlpha = dot(normalize(startCloud-_PlanetPos),normalize(_SunPos-_PlanetPos));
                        
                        if(returnCond){
                                //col = fixed4(1,1,1,1);
                                col = lerp(_CloudColor*max(0.2,cloudAlpha),col,exp(-density*distanceAlpha));//fixed4(1,1,1,1);
                        }
                        
                        //cloudColor /= 100.0;
                        //Change to cloud if meets condition
                        //if(end_point != terrainworldPos)
                        //if(length(intersectionPoint-start_point) > length(terrainPosition-start_point)) return col;
                        //col = cloudColor.r > _Threshold || cloudColor.g > _ThresholdG ? lerp(col,fixed4(1,1,1,1),cloudColor.b) : col;
                        
                    }
                    
                }else{

                    //If not intersecting atmosphere
                    col = noColor;
                }
                

                
                float3 nearPlanePosition = (ray_direction * 0.01) +  _WorldSpaceCameraPos;
                //if under water, color 
                if(length(nearPlanePosition-_PlanetPos) <= _OceanRad){
                    
                    col = lerp(_OceanCol,col,exp(-terrainLevel)) * _OceanCol;
                }
                //////////////////////////////////////
               

                return col;
            }
            ENDCG
        }
    }
}

