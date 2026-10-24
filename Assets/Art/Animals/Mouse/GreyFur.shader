Shader "HuntHappens/GreyMouse"
{
 Properties { _BaseMap("Texture", 2D) = "white" {} _FurColor("Fur color", Color) = (0.28,0.29,0.30,1) }
 SubShader
 {
 Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
 Pass
 {
 Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST;
 float4 _FurColor;
 CBUFFER_END
 struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
 struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float fog:TEXCOORD3; };
 Varyings Vert(Attributes v)
 {
 Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
 o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.normalWS=TransformObjectToWorldNormal(v.normalOS);
 o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.fog=ComputeFogFactor(p.positionCS.z);return o;
 }
 half4 Frag(Varyings i):SV_Target
 {
 half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
 half high=max(color.r,max(color.g,color.b));half low=min(color.r,min(color.g,color.b));
 // Lift neutral dark fur; retain saturated pink skin and the darkest eye details.
 half fur=(1-smoothstep(0.015,0.08,color.r-color.b))*smoothstep(0.0001,0.002,high);
 color=lerp(color,_FurColor.rgb+color*0.6,fur);
 half3 normal=normalize(i.normalWS); Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
 half3 lighting=SampleSH(normal)+light.color*saturate(dot(normal,light.direction))*light.shadowAttenuation;
 return half4(MixFog(color*lighting,i.fog),1);
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
