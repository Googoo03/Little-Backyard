// Upgrade NOTE: commented out 'float3 _WorldSpaceCameraPos', a built-in variable

// Upgrade NOTE: commented out 'float3 _WorldSpaceCameraPos', a built-in variable

// Upgrade NOTE: commented out 'float3 _WorldSpaceCameraPos', a built-in variable

Shader "Custom/Atmosphere_IE"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PlanetPos ("Planet Position", Vector) = (0,0,0,0)
        _SunPos ("Sun Position", Vector) = (0,0,0,0)
        _Color ("Atmosphere Color", Color) = (1,1,1,1)
        _Radius ("Planet Radius", float) = 5
        _AtmosphereHeight ("Atmosphere Height", float) = 0.5
        _Density ("Atmosphere Density", float) = 1
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

                return o;
            }


            sampler2D _MainTex;
            float3 _PlanetPos;
            float3 _SunPos;
            float _Radius;
            float _AtmosphereHeight;
            float _Density;
            float4 _Color;

            float distanceToPlanet;

            RWStructuredBuffer<float3> planetPositions;
            float numPlanets;


            float3 orthogonalProjection(float3 mu, float3 v, float cameraDistanceToPlanet) //calculates the orthogonal projection of v onto mu, returns the orthogonal vector
            {
                float lengthV = length(v);
                float scalar = (dot(mu,v)/(lengthV*lengthV));
                float3 w1 = scalar * v;
                float3 w2 = mu - w1;
                return (scalar < 0 && cameraDistanceToPlanet > _Radius+_AtmosphereHeight) ? w2 * 1000 : w2;
            }

            float QuadraticSolve(float a, float b, float c, bool plus){
                return plus ? ( (-b + sqrt(b*b - 4*a*c)) / 2*a) : ( (-b - sqrt(b*b - 4*a*c)) / 2*a);
            }

            fixed4 compute(v2f i, float3 planetPos){
                float3 viewDirection = normalize(i.viewVector);
                //set vector from camera to planet and get magnitude of said vector
                float3 cameraToPlanetVector =  planetPos - _WorldSpaceCameraPos;
                float cameraDistanceToPlanet = length(cameraToPlanetVector);

                //find the orgonal projection of the view vector onto the camera to planet vector
                float3 orthogonalToPlanet = orthogonalProjection(cameraToPlanetVector, viewDirection,cameraDistanceToPlanet);
                distanceToPlanet = length(orthogonalToPlanet);
                

                //find distance from camera, max being the atmosphere
                float angleView_W2 = atan(cameraDistanceToPlanet / distanceToPlanet); //finds angle connecting the viewVector to W2
                float sin_angleView_W2 = sin(angleView_W2);
                float _C = asin( (sin_angleView_W2 * distanceToPlanet) / (_Radius + _AtmosphereHeight) );
                float _A = 180 - _C - angleView_W2;
                float intersection_atmosphere_scalar = ((_Radius + _AtmosphereHeight) * (sin(_A)) / sin_angleView_W2);

                

                float depthTextureSample = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, i.screenPos);
                float terrainLevel = LinearEyeDepth(depthTextureSample);

                float atmosphere_depth = clamp(terrainLevel - intersection_atmosphere_scalar,0,_AtmosphereHeight);

                

                fixed4 noColor = tex2D(_MainTex, i.uv);
                fixed4 atmosphereColor = _Color;
                float atmosphereAlpha = 1;

                atmosphereAlpha *= 1 - saturate((distanceToPlanet - _Radius) / _AtmosphereHeight);

                /////////ONLY SHOW WHERE INTERSECTS WITH LIGHT
                float r = _Radius + _AtmosphereHeight;
                float3 Q = _WorldSpaceCameraPos -  planetPos;
                float a = 1;//viewDirection * viewDirection;
                float b = 2 * dot(viewDirection, Q);
                float c = dot(Q,Q) - r*r;
                float d = (dot(viewDirection,Q)*dot(viewDirection,Q)) - c;

                float t1 = -1;
                float t2 = -1;
                
                float dotProduct = 0;

                if(d >= 0){
                    t1 = QuadraticSolve(a,b,c,false);
                    t2 = QuadraticSolve(a,b,c,true);

                    if(t1 >= 0 || t2 >=0){

                    
                    float3 intersectionPoint = _WorldSpaceCameraPos + (t1*viewDirection);

                    float3 normalVector = normalize(intersectionPoint -  planetPos); //is this right?
                    float3 lightVector = normalize(_SunPos -  planetPos);
                    dotProduct = dot(lightVector,normalVector); //always outputs -1, why?
                    //dot product's not working.
                    atmosphereAlpha *= dotProduct;
                    atmosphereAlpha = max(0,atmosphereAlpha);
                    }
                }
                
                
                /////////////////////////////////////////////////
                

                fixed4 col = (distanceToPlanet < (_Radius + _AtmosphereHeight) ) ? lerp( fixed4(atmosphereColor.xyz,atmosphereAlpha),noColor,exp(-atmosphere_depth * _Density * atmosphereAlpha) ) : noColor;
                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                
                float3 viewDirection = normalize(i.viewVector);
                //set vector from camera to planet and get magnitude of said vector
                float3 cameraToPlanetVector =  _PlanetPos - _WorldSpaceCameraPos;
                float cameraDistanceToPlanet = length(cameraToPlanetVector);

                //find the orgonal projection of the view vector onto the camera to planet vector
                float3 orthogonalToPlanet = orthogonalProjection(cameraToPlanetVector, viewDirection,cameraDistanceToPlanet);
                distanceToPlanet = length(orthogonalToPlanet);
                

                //find distance from camera, max being the atmosphere
                float angleView_W2 = atan(cameraDistanceToPlanet / distanceToPlanet); //finds angle connecting the viewVector to W2
                float sin_angleView_W2 = sin(angleView_W2);
                float _C = asin( (sin_angleView_W2 * distanceToPlanet) / (_Radius + _AtmosphereHeight) );
                float _A = 180 - _C - angleView_W2;
                float intersection_atmosphere_scalar = ((_Radius + _AtmosphereHeight) * (sin(_A)) / sin_angleView_W2);

                

                float depthTextureSample = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, i.screenPos);
                float terrainLevel = LinearEyeDepth(depthTextureSample);

                float atmosphere_depth = clamp(terrainLevel - intersection_atmosphere_scalar,0,_AtmosphereHeight);

                

                fixed4 noColor = tex2D(_MainTex, i.uv);
                fixed4 atmosphereColor = _Color;
                float atmosphereAlpha = 1;

                atmosphereAlpha *= 1 - saturate((distanceToPlanet - _Radius) / _AtmosphereHeight);

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

                if(d >= 0){
                    t1 = QuadraticSolve(a,b,c,false);
                    t2 = QuadraticSolve(a,b,c,true);

                    if(t1 >= 0 || t2 >=0){

                    float t3 = t1 >= 0 ? t1 : t2;
                    float3 intersectionPoint = _WorldSpaceCameraPos + (t3*viewDirection);

                    float3 normalVector = normalize(intersectionPoint -  _PlanetPos); //is this right?
                    float3 lightVector = normalize(_SunPos -  _PlanetPos);
                    dotProduct = dot(lightVector,normalVector); //always outputs -1, why?
                    //dot product's not working.
                    if(t3 == t1){
                        atmosphereAlpha *= max(0,dotProduct);
                    }else if(t3 == t2) atmosphereAlpha *= sqrt(dot(intersectionPoint-_WorldSpaceCameraPos, intersectionPoint-_WorldSpaceCameraPos));
                    //atmosphereAlpha = max(0,atmosphereAlpha);;
                    }
                }
                
                //USE BILLBOARD TECHNIQUE FOR MULTIPLE ATMOSPHERES??? SWITCH THE _PlanetPos WHENEVER A NEW CLOSEST PLANET IS ASSIGNED


                /////////////////////////////////////////////////
                
                fixed4 col;
                col = (distanceToPlanet < (_Radius + _AtmosphereHeight)) ? lerp( fixed4(atmosphereColor.xyz,atmosphereAlpha),noColor,exp(-atmosphere_depth * _Density * atmosphereAlpha) ) : noColor;
                
                /*fixed4 color;

                for(int j = 0; j < numPlanets; ++j){
                    color = compute(i, planetPositions[j].xyz);
                }*/

                return col;
            }
            ENDCG
        }
    }
}
