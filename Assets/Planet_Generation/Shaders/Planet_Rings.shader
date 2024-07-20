Shader "Custom/Planet_Rings"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _PlaneNormal ("Plane Normal", Vector) = (1,1,1,1)
        _PlanetPos ("Planet Position", Vector) = (1,1,1,1)
        _Radius ("Ring Radius", float) = 1
        _Width ("Ring Width", float) = 1
        _Height ("Ring Height", float) = 1
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
            float3 _PlaneNormal;
            float4 _Color;
            float3 _PlanetPos;
            float _Radius;
            float _Width;
            float _Height;


            fixed4 frag (v2f i) : SV_Target
            {
                //add a check for the rings being no smaller than the planet radius
                _Width = max(_Radius,_Width);
                _PlaneNormal= normalize(_PlaneNormal); //fix parameters to prevent undefined behavior

                float3 viewDirection = normalize(i.viewVector); //initialize needed variables
                float depthTextureSample = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, i.screenPos);
                float terrainLevel = LinearEyeDepth(depthTextureSample);



                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 noCol = tex2D(_MainTex, i.uv); //no color

                ////////END CAPS
                float t1 = dot(_PlanetPos - _WorldSpaceCameraPos, _PlaneNormal) / dot(viewDirection,_PlaneNormal);
                float t2 = dot(_PlanetPos+(_PlaneNormal*_Height) - _WorldSpaceCameraPos, _PlaneNormal) / dot(viewDirection,_PlaneNormal);
                float t3;
                if(t1 > 0 && t2 > 0){
                        t3 = min(t1,t2);
                    }else { t3 = t1 >= 0 ? t1 : t2;}
                if(terrainLevel < t3) t3 = -1;
                float distance = length((viewDirection*t3+_WorldSpaceCameraPos) - _PlanetPos);
                /////////////////
                
                float t4 = (t3 == t1) ? t2 : t1;
                float3 up = (viewDirection*t3+_WorldSpaceCameraPos);
                float3 down = t4 > 0 ? (viewDirection*t4+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                    
                col = (t3 > 0 && distance < _Width && distance > _Radius) ? lerp(noCol,_Color,length(up-down)) : noCol;
                return col;
                
                if(t3>0 && distance < _Width && distance > _Radius){
                    //col = _Color;
                    return col;
                }

                ///////CIRCULAR REGION
                float3 an = cross(viewDirection,_PlaneNormal);
                float3 b = _PlanetPos - _WorldSpaceCameraPos;
                float c = dot(b,an);
                

                float discriminant = (dot(an,an)*(_Width*_Width)) - (dot(_PlaneNormal,_PlaneNormal)*(c*c));
                float t;
                float te;

                if(discriminant >= 0){
                    float d1 = (dot(an,cross(b,_PlaneNormal)) + sqrt(discriminant)) / dot(an,an);
                    float d2 = (dot(an,cross(b,_PlaneNormal)) - sqrt(discriminant)) / dot(an,an);
                    float d3;
                    if(d1 > 0 && d2 > 0){
                        d3 = min(d1,d2);
                    }else { d3 = d1 >= 0 ? d1 : d2;}
                    

                    t= dot(_PlaneNormal,(viewDirection*d3)-(b));
                    if(terrainLevel < d3) t = -1;
                    
                    if(t>0 && d3 > 0 && t < _Height){
                        col = _Color;
                        return col;
                    }
                }

                discriminant = (dot(an,an)*(_Radius*_Radius)) - (dot(_PlaneNormal,_PlaneNormal)*(c*c));
                

                if(discriminant >= 0){
                    float e1 = (dot(an,cross(b,_PlaneNormal)) + sqrt(discriminant)) / dot(an,an);
                    float e2 = (dot(an,cross(b,_PlaneNormal)) - sqrt(discriminant)) / dot(an,an);
                    float e3;
                    if(e1 > 0 && e2 > 0){
                        e3 = max(e1,e2);
                    }else { e3 = e1 >= 0 ? e1 : e2;}
                    

                    te= dot(_PlaneNormal,(viewDirection*e3)-(b));
                    if(terrainLevel < e3) t = -1;
                    
                    if(te>0 && e3 > 0 && te < _Height){
                        col = lerp(_Color, noCol,max(0,abs(te-(_Height/2) ) / (_Height/2) ) );
                        col = _Color;
                    }
                }
                if(t>0 && te > 0 && te < _Height && t < _Height){
                    float3 outintersection = (viewDirection * t) + _WorldSpaceCameraPos;
                    float3 innerintersection = (viewDirection * te) + _WorldSpaceCameraPos;
                        //col = lerp(noCol, _Color, length(outintersection-innerintersection) );
                        //col = _Color;
                }
                ///////////////////////
                //col = lerp(_Color, noCol, saturate(length(t-te)) );
                
                

                return col;
            }
            ENDCG
        }
    }
}