Shader "Custom/Planet_Surface_Shader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _HeightMap ("HeightMap", 2D) = "white" {}
        _TexArray("Texture Array", 2DArray) = "" {}

        _Tiling("TextureTiling", Vector) = (1,1,0,0)
        _PlanetType("Planet Type", int) = 2

        _Tile ("VertexTiling", Vector) = (1,1,0,0)
        _Offset ("VertexOffset", Vector) = (0,0,0,0)

        _CliffThreshold ("Cliff Threshold", float) = 0.1

        _SunPos ("Sun Position", vector) = (5,300,5,1)

        //_PlanetPos("Planet Position", Vector) = (31.06755,300,10.62083,0)

        _L1 ("Level1", float) = 0.1 //these correspond to the boundary level between each texture
        _L2 ("Level2", float) = 0.5

        _Levels ("Cell Segments", int) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        ZWrite On
        ZTest LEqual
        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.5

        sampler2D _HeightMap;
        float4 _Tile;
        float4 _Offset;
        float4 _Tiling;
        float4 _SunPos;

        float4 _PlanetPos;

        struct Input
        {
            float2 uv_HeightMap;

            float3 worldPos;
            float3 vertPos;
            float3 normal;

            float3 w_normal;

            float3 viewVector;
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

        float3 vertLocalPos;
        int _Levels;
        int _PlanetType;



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

        void vert(inout appdata_full vertexData, out Input o) {

            UNITY_INITIALIZE_OUTPUT(Input, o);

            o.normal = vertexData.normal;
            o.vertPos = vertexData.vertex;
            o.worldNormal = WorldNormalVector (IN, o.normal);

            float4 oVertex = UnityObjectToClipPos(vertexData.vertex);

            float4 screenPos = ComputeScreenPos(oVertex);

            float3 viewVector = mul(unity_CameraInvProjection, float4((screenPos.xy/screenPos.w) * 2 - 1, 0, -1));
            o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
        }





        float CliffOpacity(Input IN){

            float3 toPlanetVector = normalize(IN.vertPos);
            float steepness = 1-dot(toPlanetVector,IN.normal);
            if(steepness > _CliffThreshold) return 1;

            return steepness;
        }






        UNITY_DECLARE_TEX2DARRAY(_TexArray);
        

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 red = tex2D (_HeightMap, IN.uv_HeightMap);
            float3 viewDirection = normalize(IN.viewVector);

            //GET THE 2 SURFACE TEXTURE ALONG WITH CLIFF OVERRIDE TEXTURE
            int level_index = (red.r > _L1) + (red.r > _L2); //used for blending between colors
            int index = (level_index*6)+_PlanetType; //gets the index according to the height level
            int indexPlusOne = index+(1*6);//gets the next texture

            ////////////////////////////////
            float texBounds[] = {0,_L1,_L2}; //this is for testing, this should be removed with a more dynamic version later.
            ////////////////////////////////

            fixed4 c1 = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(IN.uv_HeightMap * _Tiling.xy, index));
            fixed4 c2 = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(IN.uv_HeightMap * _Tiling.xy, indexPlusOne));
            fixed4 cliff = UNITY_SAMPLE_TEX2DARRAY(_TexArray, float3(IN.uv_HeightMap * _Tiling.xy, (3*6) + _PlanetType)); //sample index 0. There should be a designated index for cliffs.
            ///////////////////////////////////////////////////////////////


            //CALCULATE ALL SURFACE OPACITIES
            float cliffOpacity = CliffOpacity(IN);

            float blendOpacity = abs((    (red.r - texBounds[level_index]) - (texBounds[level_index+1] - texBounds[level_index]) )) / abs(texBounds[level_index+1] - texBounds[level_index] + .000001);
            blendOpacity = easeInOutCubic(blendOpacity);
            /////////////////////////////////

            //BLACK OUTLINE
            if(abs(dot(IN.worldNormal,viewDirection))< 0.05){
                o.Albedo = fixed4(0,0,0,0);
                return;
            }
            /////

            //APPLY SURFACE COLOR
            fixed4 normalAlbedo = fixed4(blend( c1, blendOpacity,c2,1-blendOpacity),0);

            float3 toSunVector = normalize(_SunPos - IN.worldPos);
            float3 toPlanetVector = normalize(IN.worldPos - _PlanetPos);
            float mask = max(0,dot(toSunVector,toPlanetVector));

            o.Albedo = blend(normalAlbedo, 1-cliffOpacity, cliff, cliffOpacity);
            float dotProduct = dot(IN.normal,toSunVector)*mask;

            float step = 1.0 / _Levels;
            float dotP = (dot(IN.worldNormal,toSunVector)) > 0.8 ? 1 : (dot(IN.worldNormal,toSunVector));
            int level = dotP / (step);
            //darkness is equal to the current value


            //int range = dot(IN.worldNormal,toSunVector)*mask
            /*o.Albedo *= dot(IN.worldNormal,toSunVector) <= 0.01 ? .25 : 1;
            o.Albedo *= dot(IN.worldNormal,toSunVector) <= 0.1 ? .5 : 1;
            o.Albedo *= dot(IN.worldNormal,toSunVector) >= 0.7 ? 1.5 : 1;
            */
            o.Albedo *= (float)level / _Levels;

            o.Alpha = c1.a;
            /////////////////////
            
            
        }
        ENDCG
    }
    FallBack "Diffuse"
}
