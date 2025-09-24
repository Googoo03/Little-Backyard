// Upgrade NOTE: replaced '_CameraToWorld' with 'unity_CameraToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/Ocean_Unlit_Shader"
{
    Properties
    {
       _DepthCoef ("Depth Coefficient", float) = 0.0
       _Shallow ("SHALLOW", Color) = (1,1,1,1)
       _Deep ("DEEP", Color) = (1,1,1,1)
       _Levels("Cell Segments",int) = 5
       _Bubbles("Bubble Texture",2D) = "white"{}
       _SunPos("Sun Position", Vector) = (1,1,1,1)
       _Threshold("Threshold",float)  =1
       _Specular("Specular",float)  =1

       _WaveA ("WaveA", 2D) = "white"{}
       _WaveB ("WaveB", 2D) = "white"{}
    }
    SubShader
    {

        /*Pass{
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On
            ZTest LEqual
		    Blend SrcAlpha OneMinusSrcAlpha

            LOD 100

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            // make fog work
            

            #include "UnityCG.cginc"

            

            struct v2f
            {

                V2F_SHADOW_CASTER;
            };

            v2f vert (appdata_base v)
            {
                v2f o;

                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(0,0,0,0);

            }
            ENDCG
        }*/

        Pass
        {
            Tags {"Queue"="Geometry" "RenderType"="Opaque"}
            ZWrite On
            ZTest LEqual
		    Blend SrcAlpha OneMinusSrcAlpha

            LOD 100

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            //#pragma multi_compile_shadowcaster
            // make fog work
            

            #include "UnityCG.cginc"

            

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 viewVector : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                float3 worldNormal : TEXCOORD4;
                float4 vertex : SV_POSITION;
                //V2F_SHADOW_CASTER;
            };

            sampler2D _Bubbles;
            float4 _Bubbles_ST;

            sampler2D _CameraDepthTexture;

            sampler2D _WaveA;
            sampler2D _WaveB;
            
            float _DepthCoef;
            float _Threshold;

            float4 _Shallow;
            float4 _Deep;
            float3 _SunPos;
            int _Levels;

            float _Specular;

            v2f vert (appdata_base v)
            {
                v2f o;

                

                float3 worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float3 worldPos = mul (unity_ObjectToWorld, v.vertex).xyz;

                

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
                //o.worldPos += float4(v.normal, 0) * waveVertexAmplitude;
                //*-----------------------------------------------------------
                o.worldNormal = worldNormal;
                o.worldPos = worldPos;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = v.texcoord.xy;

                float3 viewVector = mul(unity_CameraInvProjection, float4((o.screenPos.xy/o.screenPos.w) * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                //TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            // Reoriented Normal Mapping
			// http://blog.selfshadow.com/publications/blending-in-detail/
			// Altered to take normals (-1 to 1 ranges) rather than unsigned normal maps (0 to 1 ranges)
			float3 blend_rnm(float3 n1, float3 n2)
			{
				n1.z += 1;
				n2.xy = -n2.xy;

				return n1 * dot(n1, n2) / n1.z - n2;
			}

            float3 triplanarNormal(float3 vertPos, float3 normal, float3 scale, float2 offset, sampler2D normalMap) {
				float3 absNormal = abs(normal);

				// Calculate triplanar blend
				float3 blendWeight = saturate(pow(normal, 4));
				// Divide blend weight by the sum of its components. This will make x + y + z = 1
				blendWeight /= dot(blendWeight, 1);

				// Calculate triplanar coordinates
				float2 uvX = vertPos.zy * scale + offset;
				float2 uvY = vertPos.xz * scale + offset;
				float2 uvZ = vertPos.xy * scale + offset;

				// Sample tangent space normal maps
				// UnpackNormal puts values in range [-1, 1] (and accounts for DXT5nm compression)
				float3 tangentNormalX = UnpackNormal(tex2D(normalMap, uvX));
				float3 tangentNormalY = UnpackNormal(tex2D(normalMap, uvY));
				float3 tangentNormalZ = UnpackNormal(tex2D(normalMap, uvZ));

				// Swizzle normals to match tangent space and apply reoriented normal mapping blend
				tangentNormalX = blend_rnm(half3(normal.zy, absNormal.x), tangentNormalX);
				tangentNormalY = blend_rnm(half3(normal.xz, absNormal.y), tangentNormalY);
				tangentNormalZ = blend_rnm(half3(normal.xy, absNormal.z), tangentNormalZ);

				// Apply input normal sign to tangent space Z
				float3 axisSign = sign(normal);
				tangentNormalX.z *= axisSign.x;
				tangentNormalY.z *= axisSign.y;
				tangentNormalZ.z *= axisSign.z;

				// Swizzle tangent normals to match input normal and blend together
				float3 outputNormal = normalize(
					tangentNormalX.zyx * blendWeight.x +
					tangentNormalY.xzy * blendWeight.y +
					tangentNormalZ.xyz * blendWeight.z
				);

				return outputNormal;
			}

            

            fixed4 frag (v2f i) : SV_Target
            {
                _Bubbles_ST.x += _SinTime.x/10;
                fixed4 bubbleCol = tex2D(_Bubbles,float2(i.uv.x,i.uv.y));

                


                float3 viewDirection = normalize(i.viewVector);
                float3 toSunVector = normalize(_SunPos-i.worldPos);


                float depthTextureSample = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                float terrainLevel = LinearEyeDepth(depthTextureSample);

                 //CALCULATES WORLD POSITION AS OPPOSED TO DEPTH
                 const float3 ray_direction = normalize(i.worldPos - _WorldSpaceCameraPos);

                float3 world_ray = normalize(UnityObjectToWorldDir(viewDirection));

                float3 cam_forward_world = mul((float3x3)unity_CameraToWorld, float3(0,0,1));
                float ray_depth_world = dot(cam_forward_world, ray_direction);

                float3 terrainPosition = (ray_direction / ray_depth_world) * terrainLevel +  _WorldSpaceCameraPos;
                ///////////////////////////////////////////////


                ////////////////REMOVE LATER
                // -------- Specularity --------
				// Specular normal
				float waveSpeed = 0.1;
				float waveNormalScale = 0.05;
				float waveStrength = 0.4;
				
				float2 waveOffsetA = float2(_Time.x * waveSpeed, _Time.x * waveSpeed * 0.8);
				float2 waveOffsetB = float2(_Time.x * waveSpeed * - 0.8, _Time.x * waveSpeed * -0.3);
				float3 waveNormal1 = triplanarNormal(i.worldPos, i.worldNormal, waveNormalScale, waveOffsetA, _WaveA);
				float3 waveNormal2 = triplanarNormal(i.worldPos, i.worldNormal, waveNormalScale, waveOffsetB, _WaveB);
				float3 waveNormal = triplanarNormal(i.worldPos, waveNormal1, waveNormalScale, waveOffsetB, _WaveB);
				float3 specWaveNormal = normalize(lerp(i.worldNormal, waveNormal, waveStrength));
                ////////////////

                //PHONG SPECULAR HIGHLIGHTS
                float3 ref = normalize((2 * dot(specWaveNormal,toSunVector)* specWaveNormal) - toSunVector); //direction already normalized?
                fixed4 specular = fixed4(1,1,1,1);

                

                float reflection_value = dot(ray_direction,-ref) < 0 ? 0 : dot(ray_direction,-ref);
                reflection_value = pow(reflection_value, _Specular); //is specular_power the same as a?

                float step = 1.0 / _Levels;
                int level = (reflection_value) / (step);
                reflection_value = (float)level / _Levels;

                specular *= reflection_value;
                ////




                float3 waterLevel = i.worldPos;
                float waterDepth = length(terrainPosition - waterLevel);

                float crestDepth = (0.5-(_SinTime.w/2));
                crestDepth = waterDepth < (0.005) ? 0 : crestDepth; //that 0.005 shouldn't really be a constant. Should be a dynamic value instead

                float3 waterColor = lerp(_Deep,_Shallow,exp(-waterDepth*_DepthCoef));

                float depth_marker = .005;
                if(abs((crestDepth*.05)-waterDepth) < depth_marker) waterColor = lerp(fixed4(1,1,1,1),waterColor,crestDepth);
                //waterColor = (bubbleCol.b > _Threshold && waterDepth < depth_marker) ? lerp(fixed4(1,1,1,1),waterColor,waterDepth) : waterColor;

                float alpha = 1;max(1-dot(-viewDirection, i.worldNormal),0.5);

               
                
                //darkness is equal to the current value

                waterColor *= dot(i.worldNormal,toSunVector);//(float)level / _Levels;
                waterColor += specular;

                //SHADOW_CASTER_FRAGMENT(i)

                return float4(waterColor,alpha);

            }
            ENDCG
        }
    }
}
