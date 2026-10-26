Shader "HuntHappens/Day Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.20,0.51,0.82,1)
        _Horizon ("Horizon", Color) = (0.72,0.87,0.95,1)
        _SunColor ("Sun", Color) = (1,0.95,0.76,1)
        _SunDirection ("Sun Direction", Vector) = (0,1,0,0)
        _SunRadius ("Sun Radius (degrees)", Range(0.2,4)) = 1.15
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Zenith, _Horizon, _SunColor;
            float4 _SunDirection;
            float _SunRadius;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.direction = v.positionOS.xyz;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                half3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(direction.y), 0.65));
                float separation = length(direction - normalize(_SunDirection.xyz));
                float radius = radians(_SunRadius);
                float edge = max(fwidth(separation), 0.0004);
                float disc = 1 - smoothstep(radius - edge, radius + edge, separation);
                float halo = exp(-pow(separation / (radius * 3.8), 2)) * 0.28;
                sky = lerp(sky, _SunColor.rgb, halo);
                return half4(lerp(sky, _SunColor.rgb * 1.35, disc), 1);
            }
            ENDHLSL
        }
    }
}
