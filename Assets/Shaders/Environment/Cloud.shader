Shader "HuntHappens/Soft Cloud"
{
    Properties
    {
        _TopColor ("Sunlit White", Color) = (1,0.98,0.92,1)
        _BottomColor ("Underside", Color) = (0.64,0.73,0.82,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _TopColor, _BottomColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float distanceToCamera = distance(i.positionWS, _WorldSpaceCameraPos);
                float visibility = 1 - smoothstep(310, 380, distanceToCamera);
                float3 n = normalize(i.normalWS);
                Light sun = GetMainLight();
                float light = saturate(dot(n, sun.direction) * 0.5 + 0.5);
                float top = smoothstep(-0.7, 0.65, n.y);
                half3 color = lerp(_BottomColor.rgb, _TopColor.rgb, saturate(top * 0.55 + light * 0.45));
                // Aerial perspective softens distant silhouettes.
                float haze = smoothstep(150, 420, distance(i.positionWS, _WorldSpaceCameraPos)) * 0.55;
                return half4(lerp(color, half3(0.72,0.84,0.91), haze), visibility);
            }
            ENDHLSL
        }
    }
}
