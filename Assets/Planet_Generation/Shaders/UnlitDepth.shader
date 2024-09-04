Shader "Custom/UnlitDepth"
{
    SubShader
    {
        Tags {"LightMode"="ShadowCaster"}
        Pass
        {
            ZWrite On
            ZTest LEqual
            ColorMask 0 // Don't write to the color buffer, only depth

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f
            {

                V2F_SHADOW_CASTER;
            };

            v2f vert (float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}