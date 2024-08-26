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

         _Generate ("Generate",int) = 0
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
            #define FLT_MAX 3.402823466e+38

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

            int _Generate;


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

                if(_Generate == 0) return noCol;

                int intersectionCombination = 0;

                ////////END CAPS
                float t1 = dot(_PlanetPos - _WorldSpaceCameraPos, _PlaneNormal) / dot(viewDirection,_PlaneNormal);
                float t2 = dot(_PlanetPos+(_PlaneNormal*_Height) - _WorldSpaceCameraPos, _PlaneNormal) / dot(viewDirection,_PlaneNormal);

                float t3; //closest intersection
                float t4; //farthest intersection

                if(t1 > 0 && t2 > 0){
                        t3 = min(t1,t2);
                }else { 
                    t3 = t1 >= 0 ? t1 : t2;
                }
                t4 = (t3 == t1) ? t2 : t1;

                if(terrainLevel < t3) t3 = -1;
                if(terrainLevel < t4) t4 = -1;

                
                /////////////////
                
                float3 up = float3(FLT_MAX,FLT_MAX,FLT_MAX);
                float3 down = float3(FLT_MAX,FLT_MAX,FLT_MAX);

                //float3 up = (viewDirection*t3+_WorldSpaceCameraPos);
                //float3 down = t4 > 0 ? (viewDirection*t4+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;

                float distance = t3 > 0 ? length((viewDirection*t3+_WorldSpaceCameraPos) - _PlanetPos) : -1;
                float distancet2 = t4 > 0 ? length((viewDirection*t4+_WorldSpaceCameraPos) - _PlanetPos+(_PlaneNormal*_Height)) : -1;
                    
                //NEED TO CONSIDER
                bool meetsDistance = (distance < _Width && distance > _Radius) || (distancet2 < _Width && distancet2 > _Radius);

                //IF A PLANE IS INTERSECTED AND WITHIN RANGE, THEN SET UP AND DOWN ACCORDINGLY.
                if( (distance < _Width && distance > _Radius)  && up.x == FLT_MAX){
                    up = (viewDirection*t3+_WorldSpaceCameraPos);
                }
                if( (distancet2 < _Width && distancet2 > _Radius) && down.x == FLT_MAX){
                    down = t4 > 0 ? (viewDirection*t4+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                }
                ///////////////////////////////////////////////////////////////////////////////
                
                /*if((t3 > 0 && t4 > 0)){
                    col = meetsDistance ? lerp(noCol,_Color,length(down-up)) : noCol;
                    //return col;
                }else if((t3 > 0 || t4 > 0) &&  meetsDistance){
                    col = ( (t3 > 0 || t4 > 0) && meetsDistance) ? lerp(noCol,_Color,length(down-up)) : noCol;
                    //return col;
                }*/
                
                //return col;

                ///////CIRCULAR REGION

                float3 an = cross(viewDirection,_PlaneNormal);
                float3 b = _PlanetPos - _WorldSpaceCameraPos;
                float c = dot(b,an);
                

                float discriminant = (dot(an,an)*(_Width*_Width)) - (dot(_PlaneNormal,_PlaneNormal)*(c*c));
                float t;
                float te;


                float d3;
                float d4;
                if(discriminant >= 0){
                    float d1 = (dot(an,cross(b,_PlaneNormal)) + sqrt(discriminant)) / dot(an,an);
                    float d2 = (dot(an,cross(b,_PlaneNormal)) - sqrt(discriminant)) / dot(an,an);
                    
                    if(d1 > 0 && d2 > 0){
                        d3 = min(d1,d2);
                    }else { d3 = d1 >= 0 ? d1 : d2;}
                    d4 = (d3 == d1) ? d2 : d1;

                    t= dot(_PlaneNormal,(viewDirection*d3)-(b));
                    if(terrainLevel < d3) t = -1;
                    //if(terrainLevel < d4) t4 = -1;
                    

                    if(t>0 && t < _Height && d3 > 0 && up.x == FLT_MAX){
                        up = (viewDirection*d3+_WorldSpaceCameraPos);
                    }else if(t>0 && t < _Height && down.x == FLT_MAX){
                        down = d3 > 0 ? (viewDirection*d3+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                    }

                    if(t>0 && t < _Height && d4 > 0 && up.x == FLT_MAX){
                        up = (viewDirection*d4+_WorldSpaceCameraPos);
                    }else if(t>0 && t < _Height && down.x == FLT_MAX){
                        down = d4 > 0 ? (viewDirection*d4+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                    }

                    /*if(t>0 && d3 > 0 && t < _Height){
                        col = _Color;
                        //return col;
                    }*/
                }

                //INNER CIRCULAR REGION 
                discriminant = (dot(an,an)*(_Radius*_Radius)) - (dot(_PlaneNormal,_PlaneNormal)*(c*c));
                
                float e3;
                float e4;
                if(discriminant >= 0){
                    float e1 = (dot(an,cross(b,_PlaneNormal)) + sqrt(discriminant)) / dot(an,an);
                    float e2 = (dot(an,cross(b,_PlaneNormal)) - sqrt(discriminant)) / dot(an,an);
                    
                    if(e1 > 0 && e2 > 0){
                        e3 = min(e1,e2);
                    }else { e3 = e1 >= 0 ? e1 : e2;}
                    e4 = (e3 == e1) ? e2 : e1;

                    te= dot(_PlaneNormal,(viewDirection*e3)-(b));
                    if(terrainLevel < e3) te = -1;

                    if(te>0 && te < _Height && e3 > 0 && up.x == FLT_MAX){
                        up = (viewDirection*e3+_WorldSpaceCameraPos);
                    }else if(te>0 && te < _Height && down.x == FLT_MAX){
                        down = e3 > 0 ? (viewDirection*e3+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                    }

                    /*if(te>0 && te < _Height && up.x == FLT_MAX){
                        up = (viewDirection*e4+_WorldSpaceCameraPos);
                    }else if(te>0 && te < _Height && down.x == FLT_MAX){
                        down = e4 > 0 ? (viewDirection*e4+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                    }*/
                }

                ///////////////////////



                //gotta assign 2 consecutive points based on the intersectionCombination value. Then, take the length between and assign a value.
                /*switch (intersectionCombination){
                    case 6:
                        up = (viewDirection*d3+_WorldSpaceCameraPos);
                        down = t4 > 0 ? (viewDirection*t4+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                        
                        break;
                    case 12:
                        up = (viewDirection*t3+_WorldSpaceCameraPos);
                        down = t4 > 0 ? (viewDirection*t4+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                        col = fixed4(1,1,1,1);
                        return col;
                        break;
                    case 3:
                        up = (viewDirection*te+_WorldSpaceCameraPos);
                        down = d3 > 0 ? (viewDirection*d3+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                        break;
                    case 9:
                        up = (viewDirection*d3+_WorldSpaceCameraPos);
                        down = te > 0 ? (viewDirection*te+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                        break;
                    case 5:
                        up = (viewDirection*te+_WorldSpaceCameraPos);
                        down = t4 > 0 ? (viewDirection*t4+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                        break;
                    case 10:
                        up = (viewDirection*t3+_WorldSpaceCameraPos);
                        down = d3 > 0 ? (viewDirection*d3+_WorldSpaceCameraPos) : _WorldSpaceCameraPos;
                        break;
                    default:
                        //col = _Color;
                        //return col;
                        break;
                }*/
                if(down.x == FLT_MAX) down = _WorldSpaceCameraPos;
                bool withinBounds = up.x != FLT_MAX && down.x != FLT_MAX;
                col = withinBounds ? lerp(noCol,_Color,saturate(length(down-up))) : noCol;
                

                return col;
            }
            ENDCG
        }
    }
}