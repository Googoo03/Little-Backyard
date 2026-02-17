Shader "Custom/Triplanar"
{
    Properties
    {
        _PlanetPos ("Planet Position", vector) = (1,1,1,1)
        _Color ("Color", Color) = (1,1,1,1)
        _TopTex ("Albedo (RGB)", 2D) = "white" {}
        _NormalMapTop ("Normal Top", 2D) = "bump" {}
        _SideText ("Side Texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _BlendOffset ("Blend Offset", Range(0,0.5)) = 0.0
        _BlendExponent ("Blend Exponent", Range(1,4)) = 1.0
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

        sampler2D _TopTex;
        sampler2D _SideText;
        sampler2D _NormalMapTop;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal; INTERNAL_DATA
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float3 _PlanetPos;
        float _BlendOffset;
        float _BlendExponent;
        
        /*
        void vert (inout appdata_full v, out Input o) {
            
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vertPos = v.vertex.xyz;
            o.normal = v.normal;
        }*/

        float3 BlendTriplanarNormal (float3 mappedNormal, float3 surfaceNormal) {
            float3 n;
            n.xy = mappedNormal.xy + surfaceNormal.xy;
            n.z = mappedNormal.z * surfaceNormal.z;
            return n;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            IN.worldNormal = WorldNormalVector(IN, float3(0,0,1));
            // Albedo comes from a texture tinted by color
            float3 xAxis = tex2D (_SideText, IN.worldPos.yz).rgb;
            float3 yAxis = tex2D (_TopTex, IN.worldPos.xz).rgb;
            float3 zAxis = tex2D (_SideText, IN.worldPos.xy).rgb;

            //Triplanar the normal maps too. In Tangent Space
            float3 xAxisNormal = UnpackNormal(tex2D (_NormalMapTop, IN.worldPos.yz));
            float3 yAxisNormal = UnpackNormal(tex2D (_NormalMapTop, IN.worldPos.xz));
            float3 zAxisNormal = UnpackNormal(tex2D (_NormalMapTop, IN.worldPos.xy));

            
            if (IN.worldNormal.x < 0) {
		        xAxisNormal.x = -xAxisNormal.x;
            }
            if (IN.worldNormal.y < 0) {
                yAxisNormal.x = -yAxisNormal.x;
            }
            if (IN.worldNormal.z >= 0) {
                zAxisNormal.x = -zAxisNormal.x;
            }

            float3 worldNormalX =
                BlendTriplanarNormal(xAxisNormal, IN.worldNormal.zyx).zyx;
            float3 worldNormalY =
                BlendTriplanarNormal(yAxisNormal, IN.worldNormal.xzy).xzy;
            float3 worldNormalZ =
                BlendTriplanarNormal(zAxisNormal, IN.worldNormal).xyz;

            float3 blending = abs(IN.worldNormal);
            blending = saturate(blending-_BlendOffset);
            blending = normalize(max(blending, 0.00001)); // Force weights to sum to 1
            blending = pow(blending, _BlendExponent); // Exponentiate the weights
            blending /= (blending.x + blending.y + blending.z);

            float3 c = (xAxis*blending.x) + (yAxis*blending.y) + (zAxis*blending.z) * _Color.rgb;

            //=======================



            float3 n = (xAxisNormal*blending.x) + (yAxisNormal*blending.y) + (zAxisNormal*blending.z);

            //=======================

            
            o.Normal = n;
            o.Albedo = c.rgb;
            
            // Metallic and smoothness come from slider variables
        }
        ENDCG
    }
    FallBack "Diffuse"
}
