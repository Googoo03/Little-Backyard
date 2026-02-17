Shader "Custom/Atmosphere"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EXTINCTION ("Extinction factor",float) = 1.0
        _AtmoColor ("Atmosphere Color", Color) = (1,1,1,1)
        _Radius ("Radius", float) = 1.0
        _AtmosphereHeight ("Atmosphere Height", float) = 1.0
        _PlanetPos ("Planet Position", vector) = (0,0,0,0)
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

            float QuadraticSolve(float a, float b, float c, bool plus){
                return plus ? ( (-b + sqrt(b*b - 4*a*c)) / 2*a) : ( (-b - sqrt(b*b - 4*a*c)) / 2*a);
            }

            //Texture details
            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            //Planet details
            float4 _AtmoColor;
            float3 _PlanetPos;
            float _Radius;

            //Atmosphere details
            float _EXTINCTION;
            float _AtmosphereHeight;

            fixed4 frag (v2f i) : SV_Target
            {
                float3 viewDirection = normalize(i.viewVector);

                float r = _Radius + _AtmosphereHeight;
                float3 Q = _WorldSpaceCameraPos -  _PlanetPos;
                float a = 1;//viewDirection * viewDirection;
                float b = 2 * dot(viewDirection, Q);
                float c = dot(Q,Q) - r*r;
                float d = (dot(viewDirection,Q)*dot(viewDirection,Q)) - c;

                float t1 = -1;
                float t2 = -1;
                fixed4 col = tex2D(_MainTex, i.uv);


                //if facing planet, solve
                if(d < 0) return col;

                t1 = QuadraticSolve(a,b,c,false);
                t2 = QuadraticSolve(a,b,c,true);

                if(t1 < 0 && t2 < 0) return col;
                
                //t3 is the closest forward facing t value
                float t3;
                if(t1 > 0 && t2 > 0){
                    t3 = min(t1,t2);
                }else { t3 = t1 >= 0 ? t1 : t2;}

                //t4 is the end t value
                float t4 = t3 == t1 ? t2 : t1;
                
                
                float terrainLevel = tex2D(_CameraDepthTexture, i.uv);
                terrainLevel = LinearEyeDepth(terrainLevel);
                
                


                const float3 ray_direction = normalize(viewDirection);
                float3 cam_forward_world = mul((float3x3)unity_CameraToWorld, float3(0,0,1));
                float ray_depth_world = dot(cam_forward_world, ray_direction);
                float3 terrainPosition = (ray_direction) * terrainLevel + _WorldSpaceCameraPos;

                if(length(terrainPosition-_WorldSpaceCameraPos) < t3 && t1>0 && t2>0) return col;

                float3 start_point = t1 < 0 ? _WorldSpaceCameraPos : (ray_direction) * t1 + _WorldSpaceCameraPos;
                float3 end_point = t2 < terrainLevel ? (ray_direction) * t2 + _WorldSpaceCameraPos : terrainPosition;

                float atmosphereLength = length(start_point-end_point);

                col = lerp(_AtmoColor,col, exp(-atmosphereLength*_EXTINCTION));
                return col;
            }
            ENDCG
        }
    }
}
