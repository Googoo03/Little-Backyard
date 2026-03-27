Shader "Custom/Triplanar"
{
    Properties
    {
        _PlanetPos ("Planet Position", vector) = (1,1,1,1)
        _Color ("Color", Color) = (1,1,1,1)
        _TopTex ("Albedo (RGB)", 2D) = "white" {}
        _NormalMapTop ("Normal Top", 2D) = "bump" {}
        _SideTex ("Side Texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _BlendOffset ("Blend Offset", Range(0,0.5)) = 0.0
        _BlendExponent ("Blend Exponent", Range(1,4)) = 1.0
        _SlopeThreshold ("Slope Threshold", Range(0,1)) = 0.5
        _SlopeExponent ("Blend Exponent", Range(1,20)) = 1.0
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
        sampler2D _SideTex;
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
        float _SlopeThreshold;
        float _SlopeExponent;
        
        float3 BlendWeights(float3 normal)
        {
            float3 b = abs(normal);
            b = saturate(b - _BlendOffset);
            b = pow(b, _BlendExponent);
            return b / (b.x + b.y + b.z + 1e-5);
        }

        float3 SampleTriplanar(sampler2D tex, sampler2D sidetex, float3 worldPos, float3 weights)
        {
            float3 x = tex2D(sidetex, worldPos.yz).rgb;
            float3 y = tex2D(sidetex, worldPos.xz).rgb;
            float3 z = tex2D(tex, worldPos.xy).rgb;

            return x * weights.x + y * weights.y + z * weights.z;
        }

        float3 BlendTriplanarNormal (float3 mappedNormal, float3 surfaceNormal) {
            float3 n;
            n.xy = mappedNormal.xy + surfaceNormal.xy;
            n.z = mappedNormal.z * surfaceNormal.z;
            return n;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 normalWS = normalize(IN.worldNormal);
            float3 localPos = IN.worldPos - _PlanetPos;
            float3 radialDir = normalize(IN.worldPos - _PlanetPos);

            
            float slope = dot(normalWS, radialDir);
            slope = saturate(slope - _SlopeThreshold);
            slope = pow(slope, _SlopeExponent);

            float3 top = tex2D(_TopTex, localPos.xz).rgb;
            float3 side = SampleTriplanar(_SideTex, _SideTex, localPos, BlendWeights(normalWS));

            float3 albedo = lerp(side, top, slope);

            o.Albedo = albedo;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
