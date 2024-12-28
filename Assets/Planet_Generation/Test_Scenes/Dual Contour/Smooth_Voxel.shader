Shader "Custom/Smooth_Voxel"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2DArray) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _SideCol ("Side Color", Color) = (1,1,1,1)
        _Tile ("Tiling", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
            float3 vertPos;
            float3 normal;

            float3 w_normal;

            float3 viewVector;
            float3 worldNormal;
            float2 uv_MainTex;
            INTERNAL_DATA
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _SideCol;

        void vert(inout appdata_full vertexData, out Input o) {

            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.worldPos = mul(unity_ObjectToWorld, float4(vertexData.vertex.xyz, 1.0)).xyz;

            

            //o.normal = vertexData.normal;
            o.vertPos = vertexData.vertex.xyz;//mul (unity_ObjectToWorld, vertexData.vertex).xyz;
            o.worldNormal = WorldNormalVector (IN, o.normal);


            float4 oVertex = UnityObjectToClipPos(vertexData.vertex);

            float4 screenPos = ComputeScreenPos(oVertex);

            float3 viewVector = mul(unity_CameraInvProjection, float4((screenPos.xy/screenPos.w) * 2 - 1, 0, -1));
            o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
        }

        UNITY_DECLARE_TEX2DARRAY(_MainTex);
        float2 _Tile;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            float3 dpdx = ddx(IN.worldPos);
	        float3 dpdy = ddy(IN.worldPos);
	        IN.normal = normalize(cross(dpdy, dpdx));

            float4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(IN.uv_MainTex * _Tile.xy, 0));

            // Albedo comes from a texture tinted by color
            float dotProduct = max(dot(IN.normal,float3(0,1,0)),0);
            fixed4 c = dotProduct > 0.5 ? col : _SideCol;

            o.Albedo = c.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
