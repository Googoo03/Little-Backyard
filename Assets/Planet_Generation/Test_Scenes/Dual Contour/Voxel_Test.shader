Shader "Custom/Voxel_Test"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ColorSide ("Side Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2DArray) = "white" {}
        _Tile ("Tiling", Vector) = (0,0,0,0)
        _VoxelData ("Voxel Data", 3D) = "white" {}
        _HeightMap ("Height Map", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _SeaLevel ("Sea Level", float) = 0
        _Scale ("VoxelScale",float) = 0
        _CELL_SIZE("Cell Size", float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.5
        #pragma require 2darray

        #include "UnityCG.cginc"


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
        fixed4 _Color;
        sampler2D _HeightMap;
        sampler3D _VoxelData;
        fixed4 _ColorSide;
        float _SeaLevel;
        float _Scale;
        float _CELL_SIZE;

        void vert(inout appdata_full vertexData, out Input o) {

            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.worldPos = mul(unity_ObjectToWorld, float4(vertexData.vertex.xyz, 1.0)).xyz;

            

            //o.normal = vertexData.normal;
            o.vertPos = vertexData.vertex.xyz;//mul (unity_ObjectToWorld, vertexData.vertex).xyz;
            o.worldNormal = WorldNormalVector (IN, o.normal);
            //o.uv = vertexData.texcoord.xy;

            float4 oVertex = UnityObjectToClipPos(vertexData.vertex);

            float4 screenPos = ComputeScreenPos(oVertex);

            float3 viewVector = mul(unity_CameraInvProjection, float4((screenPos.xy/screenPos.w) * 2 - 1, 0, -1));
            o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
        }

        UNITY_DECLARE_TEX2DARRAY(_MainTex);
        float2 _Tile;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            
            float3 dpdx = ddx(IN.worldPos);
	        float3 dpdy = ddy(IN.worldPos);
	        IN.normal = normalize(cross(dpdy, dpdx));

            float voxel = tex3D(_VoxelData,float3(0.5,0.5,0.5)+(IN.vertPos.xyz/_CELL_SIZE) * _Scale).r * 65535;
            //voxel = (int)voxel == 15 ? tex3D(_VoxelData,float3(0.5,0.5,0.5)+(IN.vertPos.xyz/_CELL_SIZE) * _Scale ).r * 65535 : voxel;
            //float4 col = voxel > 0 ? float4(1,1,0,1) : _Color;

            //(IN.vertPos.xyz/_CELL_SIZE) * _Scale         ----This gives [-0.5, 0.5]. We want 0, dimension

            float4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(IN.uv_MainTex * _Tile.xy, 2*voxel));
            float4 col_side_x = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3((float2(0.5,0.5)+(IN.vertPos.xy/_CELL_SIZE * _Scale)) * 32, 2*voxel + 1));
            float4 col_side_z = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3((float2(0.5,0.5)+(IN.vertPos.zy/_CELL_SIZE * _Scale)) * 32, 2*voxel + 1));

            // Albedo comes from a texture tinted by color
            float dotProduct = max(dot(IN.normal,float3(0,1,0)),0);
            fixed4 c = col * abs(IN.normal.y*IN.normal.y) + col_side_x * abs(IN.normal.z*IN.normal.z) + col_side_z * abs(IN.normal.x*IN.normal.x);//lerp(_ColorSide,col,pow(dotProduct,4));

            o.Albedo = c.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
