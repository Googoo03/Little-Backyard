Shader "Hidden/Dithering_ColorBand"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Bands ("Num of Bands", int) = 1
        _DitherStrength ("Dither Strength", float) = 1.0
        _AtmoColor ("Atmosphere Color", Color) = (0,0,0,0)
        _NightColor ("Night Color", Color) = (0,0,0,0)
        _CloudColor ("Cloud Color", Color) = (0,0,0,0)
        _ScatterCoef ("Scatter Coefficient", float) = 1.0
        _WindVec ("Wind Vector", Vector) = (0,0,0,0)
        _CloudTex ("Cloud Texture", 3D) = "white" {}
        _CloudDensity ("Cloud Density", float) = 1.0
        _Threshold ("Cloud Threshold", float) = 1.0
        _CloudCoeff ("Cloud Coefficient", Vector) = (0,0,0,0)
        _SkyLine ("Sky Level", float) = 1.0
        _SunPos ("Sun Position", Vector) = (0,0,0,0)
        _SunColor ("Sun Color", Color) = (0,0,0,0)
        _SecondSunColor ("Second Sun Color", Color) = (0,0,0,0)
        _MoonColor("Moon Color", Color) = (0,0,0,0)
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
            sampler3D _CloudTex;
            int _Bands;
            float _DitherStrength;
            sampler2D _CameraDepthTexture;
            fixed4 _AtmoColor;
            fixed4 _NightColor;

            float _ScatterCoef;
            float4 _CloudTex_ST;
            float _CloudDensity;
            float _CloudFalloff;
            float4 _CloudColor;
            float4 _CloudCoeff;
            float _CloudSpeed;
            float _SkyLine;
            float4 _WindVec;

            float _Threshold;

            //Sun Parameters
            float3 _SunPos;
            fixed4 _SunColor;
            fixed4 _SecondSunColor;
            
            //Moon Parameters
            fixed4 _MoonColor;

            fixed4 frag (v2f i) : SV_Target
            {
                int4x4 dither = int4x4(
                -4, 0, -3, 1,
                2, -2, 3, -1,
                -3, 1, -4, 0,
                3, -1, 2, -2
                );
                fixed4 col = tex2D(_MainTex, i.uv);
                // just invert the colors
                

                int column = ((int)(i.uv.x * 320)) % 4;
                
                int row = (int)((i.uv.y * 223)) % 4;
                
                float dither_val = (float)dither[column][row] / _Bands;

                
                //Calculate World Position
                float terrainLevel = tex2D(_CameraDepthTexture,i.uv);
                terrainLevel = LinearEyeDepth(terrainLevel);
               
                const float3 ray_direction = normalize(i.viewVector);

                float3 world_ray = normalize(UnityObjectToWorldDir(i.viewVector));

                float3 cam_forward_world = mul((float3x3)unity_CameraToWorld, float3(0,0,1));
                float ray_depth_world = dot(cam_forward_world, ray_direction);

                float3 terrainPosition = (ray_direction / ray_depth_world) * terrainLevel +  _WorldSpaceCameraPos;
                        
                float tCloud = 0.0;
                float3 intersectionLine;
                fixed4 cloudSample;
                        
                int iterator = 0;
                float density = 0.0;
                float cloud;
                        
                float3 start_point = _WorldSpaceCameraPos;
                bool returnCond = false;

                


                for(;tCloud < 100.0; tCloud += 2){
                    iterator ++;
                    intersectionLine = normalize(i.viewVector)*(tCloud + dither_val) + start_point;
                            
                    float3 uvCoords = intersectionLine*.1;
                    cloudSample = length(terrainPosition-_WorldSpaceCameraPos) > length(intersectionLine-_WorldSpaceCameraPos) ? tex3D(_CloudTex,(uvCoords*_CloudTex_ST) + (_WindVec.xyz*_Time)) : fixed4(0,0,0,0);
                            
                    cloud = (cloudSample.r*_CloudCoeff.x) + (cloudSample.g*_CloudCoeff.y) + (cloudSample.b*_CloudCoeff.z);
                    cloud = intersectionLine.y < _SkyLine ? 0 : cloud;

                    density += ( cloud > _Threshold) ? cloud : 0;
                    
                }

                density /= iterator;
                density *= _CloudDensity;
                        
                // Step 1: Scale to 0–(bands-1)
                if(dither_val > 1000){
                    col.rgb = float4(1,0,0,1);
                    return col;
                }
                _AtmoColor *= 2-dot(i.viewVector, float3(0,1,0));
                float sunDot = abs(dot(float3(1,0,0), normalize(_SunPos)));
                fixed4 sunColor = lerp(_SunColor, _SecondSunColor, sunDot);


                float sunT = max(0,dot(ray_direction,normalize(_SunPos-_WorldSpaceCameraPos)));
                _AtmoColor = lerp(_AtmoColor,sunColor,sunT*sunT*sunT);

                


                if(sunT > 0.99 && length(terrainPosition-_WorldSpaceCameraPos) > length(_SunPos-_WorldSpaceCameraPos)){ _AtmoColor = fixed4(1,1,1,1)*10; }

                sunT = min(dot(float3(0,1,0),normalize(_SunPos)) + 1,1);
                _AtmoColor = lerp(_NightColor,_AtmoColor,sunT*sunT*sunT);

                float moonT = abs(clamp(dot(ray_direction,normalize(_SunPos-_WorldSpaceCameraPos)),-1,0));
                _AtmoColor = lerp(_AtmoColor,_MoonColor,moonT*moonT*moonT);


                col.rgb = lerp(_AtmoColor, col.rgb,exp(-length(terrainPosition-_WorldSpaceCameraPos) * _ScatterCoef));
                col.rgb = lerp(col.rgb, _CloudColor, density);
                //return col;

                
                

                col.rgb *= _Bands-1;
                col.rgb += (float)dither_val * _DitherStrength;
                col.rgb = int3(col.rgb);
                // Step 2: Add dither *before* rounding
                col.rgb /= (_Bands);
                


                // Step 3: Clamp to avoid overflow
                
                col.rgb = clamp(col.rgb, 0.0, _Bands - 1);
                
                // Step 4: Quantize and normalize
                
                return col;
            }
            ENDCG
        }
    }
}
