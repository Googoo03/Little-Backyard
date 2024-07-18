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


            fixed4 frag (v2f i) : SV_Target
            {
                //add a check for the rings being no smaller than the planet radius
                _Width = max(_Radius,_Width);
                _PlaneNormal= normalize(_PlaneNormal); //fix parameters to prevent undefined behavior

                float3 viewDirection = normalize(i.viewVector); //initialize needed variables
                float depthTextureSample = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, i.screenPos);
                float terrainLevel = LinearEyeDepth(depthTextureSample);



                fixed4 col = tex2D(_MainTex, i.uv); //no Color


                float t = dot(_PlanetPos - _WorldSpaceCameraPos, _PlaneNormal) / dot(viewDirection,_PlaneNormal);
                if(terrainLevel < t) t = -1;
                float distance = length((viewDirection*t+_WorldSpaceCameraPos) - _PlanetPos);



                if(t>0 && distance < _Width && distance > _Radius) col = _Color;

                return col;
            }
            ENDCG
        }
    }
}
