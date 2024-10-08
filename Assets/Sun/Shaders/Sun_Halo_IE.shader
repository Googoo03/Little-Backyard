Shader "Custom/Sun_Halo_IE"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseTex ("Texture", 3D) = "white" {}
        _HaloColor ("Halo Color", Color) = (1,1,1,1)
        _HaloRadius ("Radius", float) = 1
        _Density ("Density", float) = 1
        _SunPos ("Sun Position", Vector) = (0,0,0,0)

        _OrbitRad ("Orbit Radius", float) = 1
        _SolarSystemNormal ("Solar System Normal", Vector) = (1,1,1,1)
        _PlanetCount ("Planet Count", int) = 1

        _SunRayThreshold ("Sun Ray Threshold", float) = 1
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

            float QuadraticSolve(float a, float b, float c, bool plus){
                return plus ? ( (-b + sqrt(b*b - 4*a*c)) / 2*a) : ( (-b - sqrt(b*b - 4*a*c)) / 2*a);
            }


            sampler2D _MainTex;
            sampler3D _NoiseTex;
            float4 _NoiseTex_ST;
            float _HaloRadius;
            float _Density;
            float3 _SunPos;
            float4 _HaloColor;
            float _SunRayThreshold;

            //HUD RING PARAMETERS
            float _OrbitRad;
            float3 _SolarSystemNormal;
            int _PlanetCount;
            /////////////////////

            fixed4 frag (v2f i) : SV_Target
            {
                float3 viewDirection = normalize(i.viewVector);

                float r = _HaloRadius;
                float3 Q = _WorldSpaceCameraPos -  _SunPos;
                float a = 1;//viewDirection * viewDirection;
                float b = 2 * dot(viewDirection, Q);
                float c = dot(Q,Q) - r*r;
                float d = (dot(viewDirection,Q)*dot(viewDirection,Q)) - c;

                fixed4 noColor = tex2D(_MainTex, i.uv);
                fixed4 haloColor = _HaloColor;

                float atmosphereAlpha = 1;

                float t1 = -1;
                float t2 = -1;
                
                float dotProduct = 0;
                float atmosphere_depth = 0;

                float depthTextureSample = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, i.screenPos);
                float terrainLevel = LinearEyeDepth(depthTextureSample);
                float3 intersectionPoint;

                //CALCULATES WORLD POSITION AS OPPOSED TO DEPTH
                const float3 ray_direction = normalize(viewDirection);

                float3 world_ray = normalize(UnityObjectToWorldDir(viewDirection));

                float3 cam_forward_world = mul((float3x3)unity_CameraToWorld, float3(0,0,1));
                float ray_depth_world = dot(cam_forward_world, ray_direction);

                float3 terrainPosition = (ray_direction / ray_depth_world) * terrainLevel +  _WorldSpaceCameraPos;
                ///////////////////////////////////////////////

                fixed4 col;

                _NoiseTex_ST.xy += float2(_Time.x,_Time.x);

                //HUD RINGS AROUND SOLAR SYSTEM
                _SolarSystemNormal = normalize(_SolarSystemNormal);

                float t_plane = dot(_SunPos - _WorldSpaceCameraPos, _SolarSystemNormal) / dot(viewDirection,_SolarSystemNormal);
                if(t_plane > 0 && terrainLevel > t_plane){
                    
                    float3 plane_intersection = viewDirection*t_plane + _WorldSpaceCameraPos;
                    int dist_marker = (int)(length(plane_intersection-_SunPos) / _OrbitRad);
                    float ratio = (length(plane_intersection-_SunPos) / _OrbitRad) - dist_marker;

                    //noColor *= dist_marker;
                    noColor = (ratio > 1-(0.1/_OrbitRad*t_plane*.1) && dist_marker < _PlanetCount) ? lerp(noColor,fixed4(1,1,1,1),0.3) : noColor;
                }
                //////////////

                if(d >= 0){
                    t1 = QuadraticSolve(a,b,c,false);
                    t2 = QuadraticSolve(a,b,c,true);

                    if(t1 >= 0 || t2 >=0){

                    float t3;// = t1 >= 0 ? t1 : t2;
                    if(t1 > 0 && t2 > 0){
                        t3 = min(t1,t2);
                    }else { t3 = t1 >= 0 ? t1 : t2;}
                    intersectionPoint = _WorldSpaceCameraPos + (t3*viewDirection);
                    //atmosphereAlpha *= 1 - saturate((distanceToPlanet - _Radius) / _AtmosphereHeight);
                    //atmosphere_depth = clamp(terrainLevel - intersection_atmosphere_scalar,0,_AtmosphereHeight);

                    
                    
                    float3 start_point;
                    float3 end_point;

                    if(terrainLevel < t3){
                        //throw out   
                        return noColor;
                    }
                    start_point = t3==t1 ? intersectionPoint : _WorldSpaceCameraPos;

                    end_point = t2 > (length(terrainPosition-_WorldSpaceCameraPos)) ? terrainPosition : _WorldSpaceCameraPos+(t2*viewDirection);

                    atmosphere_depth = length(end_point-start_point);
                    atmosphereAlpha *= lerp(0,1,atmosphere_depth/_HaloRadius);

                    float3 uvCoords = normalize(intersectionPoint-_SunPos);
                    float sunRay = tex3D(_NoiseTex,uvCoords + _NoiseTex_ST).g;

                    haloColor *= sunRay * atmosphere_depth;

                    col = lerp( fixed4(haloColor.xyz,atmosphereAlpha),noColor,exp(-atmosphere_depth * _Density ) );
                    
                    //atmosphereAlpha *= 
                }
                }else{
                    col = noColor;    
                }

                

                
                //col *= 2-dot(i.worldNormal,viewDirection);
                return col;
            }
            ENDCG
        }
    }
}
