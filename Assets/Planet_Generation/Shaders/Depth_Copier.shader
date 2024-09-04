// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/Depth_Copier" {
Properties {
	_MainTex ("", 2D) = "" {}
}
Subshader {
	
// -- DepthTextureCopy
		Pass {
 			ZTest Always Cull Off ZWrite Off Fog { Mode Off }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest

			#include "UnityCG.cginc"
			
			float4 _CameraDepthTexture_ST;
			sampler2D _CameraDepthTexture;
			
			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord,_CameraDepthTexture);
				return o;
			}

			fixed4 frag (v2f i) : COLOR
			{
				half4 OrigDepth = tex2D (_CameraDepthTexture, i.texcoord);
				return OrigDepth;
			}
			ENDCG 

		}

}

Fallback off
}