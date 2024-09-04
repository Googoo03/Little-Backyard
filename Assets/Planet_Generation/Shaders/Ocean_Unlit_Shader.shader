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
            Tags {"Queue"="Geometry"}
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

            

            fixed4 frag (v2f i) : SV_Target
            {
                _Bubbles_ST.x += _SinTime.x/10;
                fixed4 bubbleCol = tex2D(_Bubbles,float2(i.uv.x*64,i.uv.y));

                


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


                //PHONG SPECULAR HIGHLIGHTS
                float3 ref = normalize((2 * dot(i.worldNormal,toSunVector)* i.worldNormal) - toSunVector); //direction already normalized?
                fixed4 specular = fixed4(1,1,1,1);

                float reflection_value = dot(ray_direction,-ref) < 0 ? 0 : dot(ray_direction,-ref);
                reflection_value = pow(reflection_value, _Specular); //is specular_power the same as a?

                specular *= reflection_value/* * bubbleCol.b*/;
                ////




                float3 waterLevel = i.worldPos;
                float waterDepth = length(terrainPosition - waterLevel);

                float crestDepth = (0.5-(_SinTime.w/2));

                float3 waterColor = lerp(_Deep,_Shallow,exp(-waterDepth*_DepthCoef));

                float depth_marker = .005;
                if(abs((crestDepth*.05)-waterDepth) < depth_marker) waterColor = lerp(fixed4(1,1,1,1),waterColor,crestDepth);
                //waterColor = (bubbleCol.b > _Threshold && waterDepth < depth_marker) ? lerp(fixed4(1,1,1,1),waterColor,waterDepth) : waterColor;

                float alpha = 1;max(1-dot(-viewDirection, i.worldNormal),0.5);

                float step = 1.0 / _Levels;
                int level = (dot(i.worldNormal,toSunVector)) / (step);
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
