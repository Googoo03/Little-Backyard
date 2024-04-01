// Upgrade NOTE: commented out 'float3 _WorldSpaceCameraPos', a built-in variable

// Upgrade NOTE: commented out 'float3 _WorldSpaceCameraPos', a built-in variable

// Upgrade NOTE: commented out 'float3 _WorldSpaceCameraPos', a built-in variable

Shader "Custom/Atmosphere_IE"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PlanetPos ("Planet Position", Vector) = (0,0,0,0)
        _Radius ("Planet Radius", float) = 5
        _AtmosphereHeight ("Atmosphere Height", float) = 0.5
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
                float3 camRelativeWorldPos : TEXCOORD3;
                float4 vertex : SV_POSITION;
            };

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                o.screenPos = ComputeScreenPos(o.vertex);

                float3 viewVector = mul(unity_CameraInvProjection, float4((o.screenPos.xy/o.screenPos.w) * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));

                o.camRelativeWorldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz - _WorldSpaceCameraPos;

                return o;
            }

            sampler2D _MainTex;
            float3 _PlanetPos;
            float _Radius;
            float _AtmosphereHeight;

            float distanceToPlanet;

            float3 orthogonalProjection(float3 mu, float3 v) //calculates the orthogonal projection of v onto mu, returns the orthogonal vector
            {
                float lengthV = length(v);
                float3 w1 = (dot(mu,v)/(lengthV*lengthV)) * v;
                float3 w2 = mu - w1;
                return w2;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //normalize(camera position - vertex or pixel's position)
                float2 screenUV = i.uv;
                float depthTextureSample = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV); //cant we use SAMPLE_DEPTH_TEXTURE_PROJ???
                float actualDepth = LinearEyeDepth(depthTextureSample);

                float3 viewPlane = i.camRelativeWorldPos.xyz / dot(i.camRelativeWorldPos.xyz, unity_WorldToCamera._m20_m21_m22);
 
                // calculate the world position
                // multiply the view plane by the linear depth to get the camera relative world space position
                // add the world space camera position to get the world space position from the depth texture
                float3 worldPos = viewPlane * actualDepth;// + _WorldSpaceCameraPos;
                //worldPos = mul(unity_CameraToWorld, float4(worldPos, 1.0));



                float3 viewVec = worldPos;// - _WorldSpaceCameraPos;
                float3 viewDirection = normalize(i.viewVector);

                float3 cameraToPlanetVector = _PlanetPos - _WorldSpaceCameraPos;
                float3 orthogonalToPlanet = orthogonalProjection(cameraToPlanetVector, viewDirection);

                distanceToPlanet = length(orthogonalToPlanet);

                fixed4 noColor = tex2D(_MainTex, i.uv);
                fixed4 atmosphereColor = fixed4(1,0,0,0) / distanceToPlanet;
                //fixed4 col = (distanceToPlanet < (_Radius + _AtmosphereHeight) ) ? atmosphereColor : tex2D(_MainTex, i.uv);
                
                fixed4 col = (distanceToPlanet < (_Radius + _AtmosphereHeight) ) ? atmosphereColor : noColor;
                // just invert the colors
                //col.rgb = 1 - col.rgb;

                return col;
            }
            ENDCG
        }
    }
}
