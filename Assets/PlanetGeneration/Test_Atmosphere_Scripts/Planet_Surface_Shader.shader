Shader "Custom/Planet_Surface_Shader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _HeightMap ("HeightMap", 2D) = "white" {}
        _TexArray("Texture Array", 2DArray) = "" {}

        _Tiling("TextureTiling", Vector) = (1,1,0,0)

        _Tile ("VertexTiling", Vector) = (1,1,0,0)
        _Offset ("VertexOffset", Vector) = (0,0,0,0)

        _CliffThreshold ("Cliff Threshold", float) = 0.5

        _L1 ("Level1", float) = 0.1 //these correspond to the boundary level between each texture
        _L2 ("Level2", float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _HeightMap;
        float4 _Tile;
        float4 _Offset;
        float4 _Tiling;

        float4 _LIGHT;

        float _A1;
        float _A2;

        struct Input
        {
            float2 uv_HeightMap;

            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        fixed4 _Color;

        float _L1;
        float _L2;

        float texelSize;
        float textureSize;
        float2 uv_HeightMap;

        float distortionScale;

        float _CliffThreshold;

        float CliffOpacity(float threshold, Input IN, float3 normal){
            float dotProduct = abs(dot(IN.worldPos,normal));

            if(dotProduct < threshold) return 1;

            return 0;
        }

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

        float3 WorldToTangentNormalVector(Input IN, float3 normal) {
            float3 t2w0 = WorldNormalVector(IN, float3(1,0,0));
            float3 t2w1 = WorldNormalVector(IN, float3(0,1,0));
            float3 t2w2 = WorldNormalVector(IN, float3(0,0,1));
            float3x3 t2w = float3x3(t2w0, t2w1, t2w2);
            return normalize(mul(t2w, normal));
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
            //float4 displacement = tex2Dlod(_GroundDisp, float4(_uv, 0.0, 0.0)) * _displacementStrength;
            float3 n = vertexData.normal;
            //float multiplier = (r.x > _L1) ? _LandMultiplier : _OceanMultiplier; //if the value exceeds ocean level, then multiply by landmultiplier, otherwise by OceanMultiplier
            vertexData.vertex.xyz += (n * r.x * (distortionScale) ); //distort vertex posiiton
        }

        UNITY_DECLARE_TEX2DARRAY(_TexArray);
        

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 red = tex2D (_HeightMap, IN.uv_HeightMap);

            //CALCULATE THE SURFACE NORMAL
            float3 worldInterpolatedNormalVector = IN.worldNormal;

            float3 ddxPos = ddx(IN.worldPos);
            float3 ddyPos = ddy(IN.worldPos)  * _ProjectionParams.x;
            float3 worldCrossProduct = cross(ddxPos, ddyPos);
            //turns the worldCrossProduct to tangent space;
            float3 normal = WorldToTangentNormalVector(IN,worldCrossProduct);
            o.Normal = normal;
            //////////////////////////////


            //GET THE 2 SURFACE TEXTURE ALONG WITH CLIFF OVERRIDE TEXTURE
            int index = (red.r > _L1) + (red.r > _L2); //gets the index according to the height level
            int indexPlusOne = index+1;//gets the next texture

            float texBounds[] = {0,_L1,_L2}; //this is for testing, this should be removed with a more dynamic version later.

            fixed4 c1 = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(IN.uv_HeightMap * _Tiling.xy, index));
            fixed4 c2 = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(IN.uv_HeightMap * _Tiling.xy, indexPlusOne));
            fixed4 cliff = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(IN.uv_HeightMap * _Tiling.xy, 0)); //sample index 0. There should be a designated index for cliffs.
            ///////////////////////////////////////////////////////////////


            //CALCULATE ALL SURFACE OPACITIES
            float cliffOpacity = CliffOpacity(_CliffThreshold,IN,normal);


            float blendOpacity = abs((    (red.r - texBounds[index]) - (texBounds[indexPlusOne] - texBounds[index]) )) / abs(texBounds[indexPlusOne] - texBounds[index] + .000001);
            blendOpacity = easeInOutCubic(blendOpacity);
            /////////////////////////////////

            //APPLY SURFACE COLOR
            fixed4 normalAlbedo = fixed4(blend( c1, blendOpacity,c2,1-blendOpacity),0);

            o.Albedo = blend(normalAlbedo, 1-cliffOpacity, cliff, cliffOpacity);
            o.Alpha = c1.a;
            /////////////////////
            
            
        }
        ENDCG
    }
    FallBack "Diffuse"
}
