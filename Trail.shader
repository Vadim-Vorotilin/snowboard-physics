Shader "Trail/Trail" {
	Properties {
		_Color ("Diffuse color", Color) = (1,1,1,1)
		_SpecColor ("Specular color", Color) = (1,1,1,1)
		_Shininess ("Shininess", Range(0, 100)) = 10
		_SideColor ("Side color", Color) = (1,1,1,1)
		_SideFactor ("Side size factor", Range(0, 100)) = 1
		_SideAlphaFactor ("Side alpha factor", Range(0, 100)) = 3
		_MaxSideNormalCorrection ("Max side normal correction", Range(0, 1)) = 0.5
		_AmbientAmount ("Amount of ambient light", Range(0, 1)) = 1
		_OppositeSideAlphaDropSharpness ("Opposite side alpha drop sharpness", Range(1, 10000)) = 10
	}
	SubShader {
		Tags { "Queue" = "Transparent" }
		Tags { "LightMode" = "ForwardBase" }

		ZWrite Off  // don't write to depth buffer
		            // in order not to occlude other objects
		Blend SrcAlpha OneMinusSrcAlpha // use alpha blending

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			uniform float4 _LightColor0;
			uniform float4 _Color;
			uniform float4 _SpecColor;
			uniform float _Shininess;
			uniform float4 _SideColor;
			uniform float _SideFactor;
			uniform float _SideAlphaFactor;
			uniform float _MaxSideNormalCorrection;
			uniform float _AmbientAmount;
			uniform float _OppositeSideAlphaDropSharpness;

			struct appdata {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				float3 normal : NORMAL;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
				float3 dir : TEXCOORD1;
				float3 viewDir : TEXCOORD2;
				float4 worldPos : TEXCOORD3;
			};

			v2f vert (appdata v) {
				v2f o;

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.dir = normalize(v.color.xyz);
				o.normal = normalize(mul(unity_ObjectToWorld, float4(v.normal, 0.0)).xyz);
				o.viewDir = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);

				return o;
			}

			float4 frag (v2f i) : COLOR {
				float k = pow(abs(i.uv.x - 0.5) * 2, _SideFactor);
				float ka = pow(abs(i.uv.x - 0.5) * 2, _SideAlphaFactor);
				float4 color = lerp(_Color, _SideColor, k);

				float3 normalDirection = normalize(lerp(i.normal, i.dir, min(_MaxSideNormalCorrection, k)));

				float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
				float3 diffuseReflection = _LightColor0 * max(0.0, dot(normalDirection, lightDirection));

				float3 ambientLighting = UNITY_LIGHTMODEL_AMBIENT.rgb * _Color.rgb * _AmbientAmount;

				float3 specularReflection;
				if (dot(normalDirection, lightDirection) < 0) {
					specularReflection = float3(0, 0, 0);
				} else {
					specularReflection = _LightColor0.rgb * _SpecColor.rgb * pow(max(0.0, dot(reflect(-lightDirection, normalDirection), i.viewDir)), _Shininess);
				}

				float oppositeSideFactor = dot(normalDirection, normalize(i.worldPos - _WorldSpaceCameraPos));

				float oppositeSideAlpha = pow(sign(clamp(-oppositeSideFactor, 0, 1)) + (1 - pow(oppositeSideFactor, 4)) * clamp(sign(oppositeSideFactor), 0, 1), _OppositeSideAlphaDropSharpness);

				return color * float4(ambientLighting + diffuseReflection + specularReflection, (1 - ka) * oppositeSideAlpha);
			}

			ENDCG
		}
	}
	Fallback "Diffuse"
}
