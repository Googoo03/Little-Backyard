Shader "Custom/Planet_Surface_Shader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _HeightMap ("HeightMap", 2D) = "white" {}
        _TexArray("Texture Array", 2DArray) = "" {}
        _GroundTexture("Land",2D) = "white" {}
        _GroundDisp("GroundDisp",2D) = "white" {}

        _SandTexture("Sand",2D) = "white" {}
        _RockTexture("Rock",2D) = "white" {}

        _Tile ("VertexTiling", Vector) = (1,1,0,0)
        _Offset ("VertexOffset", Vector) = (0,0,0,0)
        _A1 ("a1",float) = 0
        _A2 ("a2",float) = 0

        _displacementStrength ("displacement map strength", float) = 0

        _L1 ("Level1", float) = 0.1 //these correspond to the boundary level between each texture
        _L2 ("Level2", float) = 0.5
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
            float3 worldNormal;
            //float arrayIndex;
        };

        fixed4 _Color;

        float _L1;
        float _L2;

        float texelSize;
        float textureSize;
        float2 uv_HeightMap;

        float distortionScale;
        float _displacementStrength;

        float3 blend(fixed4 c1, float a1, fixed4 c2, float a2){
            //return c1.a + a1 > c2.a + a2 ? c1.rgb : c2.rgb;
            float denom = a1+a2;
            return (c1.rgb * a1/denom) + (c2.rgb * a2/denom);
        }

        float easeInOutCubic(float x) {
            float ret = x < 0.5 ? 4 * x * x * x : 1 - pow(-2 * x + 2, 3) / 2;
            ret = (ret > 1) ? 1 : ret;
            ret = (ret < 0) ? 0 : ret;
            return ret;
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

        UNITY_DECLARE_TEX2DARRAY(_TexArray);
        

        void surf (Input IN, inout SurfaceOutputStandard o)
        {

            

            // Albedo comes from a texture tinted by color
            fixed4 red = tex2D (_HeightMap, IN.uv_HeightMap);

            float3 worldInterpolatedNormalVector = IN.worldNormal;

            int index = (red.r > _L1) + (red.r > _L2); //gets the index according to the height level
            int indexPlusOne = index+1;//(red.r > _L2) ? index : index+1; //gets the next texture

            float texBounds[] = {0,_L1,_L2}; //this is for testing, this should be removed with a more dynamic version later.

            fixed4 c1 = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(IN.uv_HeightMap, index));
            fixed4 c2 = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(IN.uv_HeightMap, indexPlusOne));

            float space = 0.1;

            //float bounds = abs(texBounds[index] - red.r) < abs(texBounds[index-1] - red.r) ? texBounds[index] : texBounds[index-1];
            //float blendOpacity = easeInOutCubic( (red.r - texBounds[index]) / texBounds[index]); //NEED BETTER EQUATION TO PROPERLY REPRESENT HOW TO BLEND BETWEEN TEXTURES
            

            float blendOpacity = abs((    (red.r - texBounds[index]) - (texBounds[indexPlusOne] - texBounds[index]) )) / abs(texBounds[indexPlusOne] - texBounds[index] + .000001);
            blendOpacity = easeInOutCubic(blendOpacity);
            /*if( abs(red.r - _L1) < 0.001 || abs(red.r - _L2) < 0.001) {
                o.Albedo = float4(1,0,0,0);
            }else{
                o.Albedo = blend( c1, blendOpacity,c2,1-blendOpacity);
            }*/
            o.Albedo = blend( c1, blendOpacity,c2,1-blendOpacity);
            o.Alpha = c1.a;
            //o.Normal = UnpackNormal( tex2D( _GroundDisp, IN.uv_HeightMap ) );
            
        }
        ENDCG
    }
    FallBack "Diffuse"
}
