Shader "Custom/Planet_Surface_Shader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _HeightMap ("Albedo (RGB)", 2D) = "white" {}
        _Tile ("VertexTiling", Vector) = (1,1,0,0)
        _Offset ("VertexOffset", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _HeightMap;
        float4 _Tile;
        float4 _Offset;

        struct Input
        {
            float2 uv_HeightMap;
        };

        fixed4 _Color;

        float texelSize;
        float textureSize;
        float2 uv_HeightMap;

        float distortionScale;

        void vert(inout appdata_full vertexData) {

            distortionScale = 0.3;

            textureSize = 16; //assumes the texture is 16x16
            texelSize = 1.0 / textureSize;


            float2 _uv = vertexData.texcoord.xy; //get original uv
            float2 _uvOffset = float2(texelSize * _Offset.x, texelSize * _Offset.y); //create offset
            _uv += _uvOffset; //shift
            _uv *= _Tile.xy; //shrink


            float4 r = tex2Dlod(_HeightMap, float4(_uv, 0.0, 0.0));
            float3 n = vertexData.normal;
            vertexData.vertex.xyz += (n * r.x * distortionScale); //distort vertex posiiton
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_HeightMap, IN.uv_HeightMap) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
