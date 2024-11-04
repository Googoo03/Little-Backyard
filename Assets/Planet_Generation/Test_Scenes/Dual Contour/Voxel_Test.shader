Shader "Custom/Voxel_Test"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ColorSide ("Side Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _SeaLevel ("Sea Level", float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow

        #pragma target 3.5

        sampler2D _MainTex;

        struct Input
        {
            float3 worldPos;
            float3 vertPos;
            float3 normal;

            float3 w_normal;

            float3 viewVector;
            float3 worldNormal;
            INTERNAL_DATA
        };
        fixed4 _Color;
        fixed4 _ColorSide;
        float _SeaLevel;

        void vert(inout appdata_full vertexData, out Input o) {

            UNITY_INITIALIZE_OUTPUT(Input, o);

            o.normal = vertexData.normal;
            o.vertPos = vertexData.vertex.xyz;//mul (unity_ObjectToWorld, vertexData.vertex).xyz;
            o.worldNormal = WorldNormalVector (IN, o.normal);

            float4 oVertex = UnityObjectToClipPos(vertexData.vertex);

            float4 screenPos = ComputeScreenPos(oVertex);

            float3 viewVector = mul(unity_CameraInvProjection, float4((screenPos.xy/screenPos.w) * 2 - 1, 0, -1));
            o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            
            // Albedo comes from a texture tinted by color
            float dotProduct = max(dot(IN.normal,float3(0,1,0)),0);
            fixed4 c = lerp(_ColorSide,_Color,dotProduct);
            c = lerp(_Color, float4(0,0,1,1),IN.vertPos.y < _SeaLevel);
            o.Albedo = c.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
