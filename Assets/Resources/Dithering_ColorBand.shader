Shader "Unlit/Dithering_ColorBand"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlueNoise ("Blue Noise", 2D) = "white" {}
        _PerlinNoise ("Perlin Noise", 3D) = "white" {}
        _Bands ("Num of Bands", int) = 1
        _DitherStrength ("Dither Strength", float) = 1.0
        _AtmoColor ("Atmosphere Color", Color) = (0,0,0,0)
        _NightColor ("Night Color", Color) = (0,0,0,0)
        _CloudColor ("Cloud Color", Color) = (0,0,0,0)
        _CloudAmbient ("Cloud Ambient Color", Color) = (0,0,0,0)
        _ScatterCoef ("Scatter Coefficient", float) = 1.0
        _WindVec ("Wind Vector", Vector) = (0,0,0,0)
        _CloudTex ("Cloud Texture", 3D) = "white" {}
        _CloudDensity ("Cloud Density", float) = 1.0
        _Threshold ("Cloud Threshold", float) = 1.0
        _WorleyPerlinMix ("Worley Perlin Mix", Range(0,1)) = 0.5
        _CloudCoeffR ("Cloud Coefficient R", Range(0,1)) = 0
        _CloudCoeffG ("Cloud Coefficient G", Range(0,1)) = 0
        _CloudCoeffB ("Cloud Coefficient B", Range(0,1)) = 0
        _CloudCoeffA ("Cloud Coefficient A", Range(0,1)) = 0
        _SkyLine ("Sky Level", float) = 1.0
        _SunPos ("Sun Position", Vector) = (0,0,0,0)
        _SunColor ("Sun Color", Color) = (0,0,0,0)
        _SecondSunColor ("Second Sun Color", Color) = (0,0,0,0)
        _MoonColor("Moon Color", Color) = (0,0,0,0)
        _Position ("Cloud Cube Position", Vector) = (0,0,0,0)
        _Scale ("Cloud Cube Scale", Vector) = (1,1,1,0)
        _PhaseG ("Phase G", Range(-1,1)) = 0.76
        _LightIntensity ("Light Intensity", float) = 1.0
        _EXTINCTION_MULT ("Extinction Multiplier", float) = 1.0
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
                float4 screenPos : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 viewVector : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                float3 viewVector = mul(unity_CameraInvProjection, float4((o.screenPos.xy/o.screenPos.w) * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return o;
            }

            sampler2D _MainTex;
            sampler2D _BlueNoise;
            sampler3D _CloudTex;
            sampler3D _PerlinNoise;
            int _Bands;
            float _DitherStrength;
            sampler2D _CameraDepthTexture;
            fixed4 _AtmoColor;
            fixed4 _NightColor;

            float _ScatterCoef;
            float4 _CloudTex_ST;
            float4 _PerlinNoise_ST;
            float _CloudDensity;
            float _CloudFalloff;
            float4 _CloudColor;
            float4 _CloudAmbient;
            float _CloudCoeffR;
            float _CloudCoeffG;
            float _CloudCoeffB;
            float _CloudCoeffA;
            float _CloudSpeed;
            float _SkyLine;
            float4 _WindVec;
            float _WorleyPerlinMix;
            float _LightIntensity;
            float _EXTINCTION_MULT;

            float _Threshold;

            //Sun Parameters
            float3 _SunPos;
            fixed4 _SunColor;
            fixed4 _SecondSunColor;

            //Cloud Parameters
            float3 _Position;
            float3 _Scale;

            //Phase Function
            float _PhaseG;
            
            //Moon Parameters
            fixed4 _MoonColor;

            bool RayAABBIntersect(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax,
                                out float outTEnter, out float outTExit)
            {
                float tEnter = -1e20; // -infinity
                float tExit  =  1e20; // +infinity

                // For each axis
                [unroll]
                for (int i = 0; i < 3; ++i)
                {
                    float origin = rayOrigin[i];
                    float dir    = rayDir[i];
                    float bMin   = boxMin[i];
                    float bMax   = boxMax[i];

                    if (abs(dir) < 1e-8)
                    {
                        // Ray parallel: must be inside slab, otherwise no hit
                        if (origin < bMin || origin > bMax)
                            return false;
                        // else: no constraint
                    }
                    else
                    {
                        float t1 = (bMin - origin) / dir;
                        float t2 = (bMax - origin) / dir;
                        float tMin = min(t1, t2);
                        float tMax = max(t1, t2);

                        tEnter = max(tEnter, tMin);
                        tExit  = min(tExit,  tMax);

                        if (tEnter > tExit)
                            return false; // early out
                    }
                }

                // If whole intersection is behind the ray
                if (tExit < 0.0) return false;

                outTEnter = max(tEnter, 0.0); // clamp if ray starts inside
                outTExit  = tExit;
                return true;
            }

            float PhaseHG(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5);
            }

            float MultipleOctaveScattering(float density, float mu){
                float attenuation = 0.2;
                float contribution = 0.4;
                float phaseAttenuation = 0.1;

                const float scatteringOctaves = 4.0;

                float a = 1.0;
                float b = 1.0;
                float c = 1.0;
                float g = 0.85;

                float luminance = 0.0;

                for(float i = 0.0; i < scatteringOctaves; i += 1.0){
                    float phase = PhaseHG(0.3*c, mu);
                    float beers = exp(-density * _EXTINCTION_MULT * a);
                    luminance += b * phase * beers;

                    a *= attenuation;
                    b *= contribution;
                    c *= (1.0-phaseAttenuation);
                }
                return luminance;

            }


            fixed4 frag (v2f i) : SV_Target
            {
                // Camera / ray setup
                float terrainLevel = tex2D(_CameraDepthTexture, i.uv);
                terrainLevel = LinearEyeDepth(terrainLevel);

                const float3 ray_direction = normalize(i.viewVector);
                float3 cam_forward_world = mul((float3x3)unity_CameraToWorld, float3(0,0,1));
                float ray_depth_world = dot(cam_forward_world, ray_direction);
                float3 terrainPosition = (ray_direction / ray_depth_world) * terrainLevel + _WorldSpaceCameraPos;

                // Temp vars
                
                float3 start_point = _WorldSpaceCameraPos;

                // Accumulators
                float tCloud = 0.0;
                float tCloudEnter = 0.0;
                float tCloudExit = 0.0;

                float tSunCloudEnter = 0.0;
                float tSunCloudExit = 0.0;
                
                float totalDensity = 0.0;
                float4 accumulatedColor = 0.0;
                fixed4 col = tex2D(_MainTex, i.uv);
                float transmittance = 1.0;

                // ONE declaration for the cloud samples (no duplicates)
                float4 cloudSample = 0;
                float4 toSunCloudSample = 0;

                float blueNoise = tex2D(_BlueNoise, i.uv).x * .01;

                // Choose a sensible scale for mapping world-space pos -> 3D texture UVW.
                // Replace CLOUD_TEX_WORLD_SCALE with a value appropriate for your scene (e.g. 0.01).
                const float CLOUD_TEX_WORLD_SCALE = 0.01;
                float3 cloudTexScale = _CloudTex_ST.xyz;

                // Decide sun direction once (assume _SunPos is a world position)
                // If _SunPos is already a direction, replace with normalize(_SunPos).
                
                bool intersect = RayAABBIntersect(start_point, ray_direction, _Position - _Scale, _Position + _Scale, tCloudEnter, tCloudExit);
                if(!intersect) return col;

                float stepSize =(tCloudExit-tCloudEnter)*0.02;

                for (tCloud = 0; tCloud < 1.0; tCloud += 0.02)
                {
                    float3 pos = start_point + ray_direction * (tCloudEnter + (tCloudExit-tCloudEnter)*(tCloud+blueNoise));
                    

                    // Map world pos into 3D texture coordinates (UVW).
                    float3 cloudUVW = (pos * _CloudTex_ST).xyz + _WindVec.xyz * _Time.y;
                    float3 perlinUVW = (pos * _PerlinNoise_ST).xyz + _WindVec.xyz * _Time.y;
                    float4 cloudSample = tex3D(_CloudTex, cloudUVW);
                    float4 perlinSample = tex3D(_PerlinNoise, perlinUVW);
                    
                    float density = (cloudSample.r * _CloudCoeffR + cloudSample.g * _CloudCoeffG + cloudSample.b * _CloudCoeffB + cloudSample.a * _CloudCoeffA - _Threshold) * _WorleyPerlinMix;
                    density += (perlinSample.r * _CloudCoeffR + perlinSample.g * _CloudCoeffG + perlinSample.b * _CloudCoeffB + perlinSample.a * _CloudCoeffA - _Threshold) * (1.0 - _WorleyPerlinMix);
                    //if(density-stepSize < _Threshold) continue;
                    //density = 1.0-density;
                    density = max(density, 0.0);
                    float lightTransmission = _LightIntensity;

                    float3 sunDir = normalize(_SunPos - pos);

                    bool inCloud = RayAABBIntersect(pos, sunDir, _Position - _Scale, _Position + _Scale, tSunCloudEnter, tSunCloudExit);
                    float sunStep = (tSunCloudExit-tSunCloudEnter)*0.1;
                    
                    [unroll(10)]
                    for(float tSun = 0; tSun < 1.0; tSun += 0.1)
                    {
                        
                        

                        float3 sunPos = pos + sunDir * (tSunCloudEnter + (tSunCloudExit-tSunCloudEnter)*tSun);
                        float3 toSunUVW = (sunPos * _CloudTex_ST).xyz + _WindVec.xyz * _Time.y;
                        float3 toSunPerlinUVW = (sunPos * _PerlinNoise_ST).xyz + _WindVec.xyz * _Time.y;
                        float4 toSunCloudSample = tex3D(_CloudTex, toSunUVW);
                        float4 toSunPerlinSample = tex3D(_PerlinNoise, toSunPerlinUVW);
                        
                        float toSunDensity = (toSunCloudSample.r * _CloudCoeffR + toSunCloudSample.g * _CloudCoeffG + toSunCloudSample.b * _CloudCoeffB + toSunCloudSample.a * _CloudCoeffA - _Threshold) * _WorleyPerlinMix;
                        toSunDensity += (toSunPerlinSample.r * _CloudCoeffR + toSunPerlinSample.g * _CloudCoeffG + toSunPerlinSample.b * _CloudCoeffB + toSunPerlinSample.a * _CloudCoeffA - _Threshold) * (1.0 - _WorleyPerlinMix);
                        //if(toSunDensity < _Threshold) continue;
                        
                        toSunDensity = max(toSunDensity, 0.0);
                        //toSunDensity = 1.0-toSunDensity;

                        lightTransmission *= exp(-toSunDensity * _ScatterCoef * sunStep);
                    }


                    

                    // Shade & accumulate
                    float cosTheta = dot(normalize(ray_direction), sunDir);

                    // Compute phase weight
                    float phase = MultipleOctaveScattering(density, _PhaseG);

                    // Shaded sample contribution
                    float extinction = density * _CloudDensity * stepSize;
                    float localTrans = exp(-extinction);

                    accumulatedColor += (1 - localTrans) * (_CloudColor * lightTransmission * phase + _CloudAmbient) * transmittance;
                    transmittance *= localTrans;
                }

                // Blend with background
                col.rgb = col.rgb * transmittance + accumulatedColor.rgb;
                col.a = 1.0;
                

                return col;
            }
            ENDCG
        }
    }
}
