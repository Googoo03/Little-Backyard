// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

Shader "Custom/Rock_Surface_Shader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Accent ("Accent Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _BlueNoise ("Blue Noise", 2D) = "white" {}
        _SunPos( "Sun Position", Vector) = (1,1,1,1)
        _Shake ("Shake", int) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BlueNoise;
        int _Shake;

        struct Input
        {
            float2 uv_MainTex;

            float3 worldPos;

            float3 vertPos;
            float3 normal;

            float3 worldNormal;
            float3 viewVector;
            INTERNAL_DATA
        };

        float easeInOutExpo(float x){
        return x == 0
          ? 0
          : x == 1
          ? 1
          : x < 0.5 ? pow(2, 20 * x - 10) / 2
          : (2 - pow(2, -20 * x + 10)) / 2;
        }

        void vert(inout appdata_full vertexData, out Input o) {

            //UNITY_INITIALIZE_OUTPUT(Input, o);

            // transform into worlspace
             float4 world_space_vertex = mul( unity_ObjectToWorld, vertexData.vertex );

             /* Do some cool things here */
             world_space_vertex *= _SinTime;

             // transform back into local space
             vertexData.vertex = mul( unity_WorldToObject, world_space_vertex );

            o.normal = vertexData.normal;
            o.vertPos = vertexData.vertex * _SinTime;//(_Shake ==1 ? float4(1,1,1,1)*_SinTime*100 : float4(0,0,0,0));
            
            o.worldNormal = WorldNormalVector (IN, o.normal);

            float4 oVertex = UnityObjectToClipPos(vertexData.vertex);

            float4 screenPos = ComputeScreenPos(oVertex);

            float3 viewVector = mul(unity_CameraInvProjection, float4((screenPos.xy/screenPos.w) * 2 - 1, 0, -1));
            o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
        }

        fixed4 _Color;
        float4 _BlueNoise_ST;
        fixed4 _Accent;
        float3 _SunPos;
        

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 toSunVector = normalize(_SunPos - IN.worldPos);
            float3 viewDirection = normalize(IN.viewVector);
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
            fixed4 blueNoise = tex2D (_BlueNoise, IN.uv_MainTex * _BlueNoise_ST);
            //blueNoise = blueNoise.r > 0.15 ? fixed4(1,1,1,1) : fixed4(0,0,0,0);
            //o.Albedo = c.rgb;
            o.Albedo = lerp(_Accent, c.rgb,1-(easeInOutExpo(blueNoise)));
            o.Albedo *= dot(IN.worldNormal,toSunVector) > 0.5 ? 1 : 0.2;
            
            o.Alpha = c.a;
            //o.Albedo = _Shake == 1 ? float4(0,1,0,1) : float4(1,0,0,1);
            //BLACK OUTLINE
            /*if(abs(dot(IN.worldNormal,viewDirection))< 0.5){
                o.Albedo = fixed4(0,0,0,0);
                return;
            }*/
            /////
            // Metallic and smoothness come from slider variables

        }
        ENDCG
    }
    FallBack "Diffuse"
}
