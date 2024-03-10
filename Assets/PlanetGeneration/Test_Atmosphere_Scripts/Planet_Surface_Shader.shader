Shader "Custom/Planet_Surface_Shader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _HeightMap ("HeightMap", 2D) = "white" {}
        _GroundTexture("Land",2D) = "white" {}
        _GroundDisp("GroundDisp",2D) = "white" {}

        _SandTexture("Sand",2D) = "white" {}
        _RockTexture("Rock",2D) = "white" {}

        _Tile ("VertexTiling", Vector) = (1,1,0,0)
        _Offset ("VertexOffset", Vector) = (0,0,0,0)
        _A1 ("a1",float) = 0
        _A2 ("a2",float) = 0

        _displacementStrength ("displacement map strength", float) = 0

        _SandLevel ("Sand Level", float) = 0.3
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
        sampler2D _GroundTexture;
        sampler2D _GroundDisp;
        float4 _Tile;
        float4 _Offset;

        float _A1;
        float _A2;

        struct Input
        {
            float2 uv_HeightMap;
        };

        fixed4 _Color;

        float texelSize;
        float textureSize;
        float2 uv_HeightMap;

        float distortionScale;
        float _displacementStrength;

        float3 blend(fixed4 c1, float a1, fixed4 c2, float a2){
            return c1.a + a1 > c2.a + a2 ? c1.rgb : c2.rgb;
            //return c1.rgb * a1 + c2.rgb * a2;
        }

        float easeInOutCubic(float x) {
            return x < 0.5 ? 4 * x * x * x : 1 - pow(-2 * x + 2, 3) / 2;
        }

        void vert(inout appdata_full vertexData) {

            distortionScale = 0.5;

            textureSize = 16; //assumes the texture is 16x16
            texelSize = 1.0 / textureSize;


            float2 _uv = vertexData.texcoord.xy; //get original uv
            float2 _uvOffset = float2(texelSize * _Offset.x, texelSize * _Offset.y); //create offset
            _uv += _uvOffset; //shift
            _uv *= _Tile.xy; //shrink


            float4 r = tex2Dlod(_HeightMap, float4(_uv, 0.0, 0.0));
            float4 displacement = tex2Dlod(_GroundDisp, float4(_uv, 0.0, 0.0)) * _displacementStrength;
            float3 n = vertexData.normal;
            vertexData.vertex.xyz += (n * r.x * (distortionScale+ displacement) ); //distort vertex posiiton
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 red = tex2D (_HeightMap, IN.uv_HeightMap);
            fixed4 c = tex2D (_GroundTexture, IN.uv_HeightMap)*_Color;

            float blendOpacity = easeInOutCubic( (0.3-red.r+_A1) / 0.3); //the 0.5 represents a placeholder for sea level. Or I guess a boundary for texture change

            o.Albedo = blend( c, 1-blendOpacity,fixed4(0,0,1,0),blendOpacity);
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
