// Alpha-blended flat colour for blob shadows, grid overlays and range indicators. Vertex colour x tint; the UV
// radial falloff is applied when _Radial = 1 (blob shadows), otherwise flat with an edge band (cell highlight).
Shader "VerdantSiege/FlatAlpha"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (0,0,0,0.35)
        _Radial ("Radial falloff", Float) = 1
        _Edge ("Edge band width (cells)", Range(0,0.5)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-50" }
        Pass
        {
            Name "Flat"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Radial;
                half _Edge;
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
                half a;
                if (_Radial > 0.5h) a = saturate(1.0h - dot(d, d)) ;
                else
                {
                    float m = max(abs(d.x), abs(d.y));
                    half edge = smoothstep(1.0 - _Edge * 2.0, 1.0 - _Edge, m) * (1.0 - smoothstep(0.985, 1.0, m));
                    a = 0.45h + 0.55h * edge;
                }
                return half4(i.color.rgb, a * i.color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
