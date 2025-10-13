Shader "Custom/Triplanar"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
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

        sampler2D _MainTex;
        sampler2D _SideText;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _BlendOffset;
        float _BlendExponent;
        
        /*
        void vert (inout appdata_full v, out Input o) {
            
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vertPos = v.vertex.xyz;
            o.normal = v.normal;
        }*/

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 xAxis = tex2D (_SideText, IN.worldPos.yz);
            fixed4 yAxis = tex2D (_MainTex, IN.worldPos.xz);
            fixed4 zAxis = tex2D (_SideText, IN.worldPos.xy);

            float3 blending = abs(IN.worldNormal);
            blending = saturate(blending-_BlendOffset);
            blending = normalize(max(blending, 0.00001)); // Force weights to sum to 1
            blending = pow(blending, _BlendExponent); // Exponentiate the weights
            blending /= (blending.x + blending.y + blending.z);

            

            fixed4 c = (xAxis*blending.x) + (yAxis*blending.y) + (zAxis*blending.z) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
