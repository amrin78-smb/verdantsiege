// Additive glow for VFX particles, projectile trails, pickup halos and cell highlights. Vertex colour x tint x
// a procedural radial falloff (UV centred) so no texture is needed.
Shader "VerdantSiege/Glow"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Softness ("Softness", Range(0.5,6)) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (One=additive, OneMinusSrcAlpha=alpha)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Softness;
                half _DstBlend;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color * _BaseColor;
                o.uv = i.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                half r = saturate(1.0h - dot(d, d));
                half a = pow(r, _Softness) * i.color.a;
                return half4(i.color.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
