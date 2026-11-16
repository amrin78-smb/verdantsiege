// Verdant Siege: one lean URP shader for all Blender-generated geometry.
// Albedo = vertex colour (authored sRGB, decoded to linear) x _BaseColor. Vertex ALPHA = emission mask.
// Wrapped main-light diffuse + SH ambient + soft rim, optional hit flash (_FlashColor.a) and fade-to-dark (_Dim).
// No shadow maps, no additional lights, SRP-Batcher compatible (per-object flash via MaterialPropertyBlock only
// while flashing). Main light shadows are faked with blob shadows.
Shader "VerdantSiege/VertexLit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _EmissionStrength ("Emission strength", Range(0,4)) = 1.6
        _RimStrength ("Rim strength", Range(0,1)) = 0.22
        _Wrap ("Light wrap", Range(0,1)) = 0.45
        _FlashColor ("Flash (rgb, a=amount)", Color) = (1,1,1,0)
        _VertexSRGB ("Vertex colours are sRGB", Float) = 1
        _WindStrength ("Wind sway (uses vertex height)", Range(0,0.2)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _EmissionStrength;
                half _RimStrength;
                half _Wrap;
                half4 _FlashColor;
                half _VertexSRGB;
                half _WindStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 pos = i.positionOS.xyz;
                if (_WindStrength > 0.0001h)
                {
                    float3 wp = TransformObjectToWorld(float3(0,0,0));
                    float h = saturate(pos.y);
                    float t = _Time.y * 1.7 + wp.x * 0.6 + wp.z * 0.45;
                    pos.x += sin(t) * _WindStrength * h * h;
                    pos.z += cos(t * 0.8) * _WindStrength * 0.5 * h * h;
                }
                VertexPositionInputs p = GetVertexPositionInputs(pos);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                half4 c = i.color;
                if (_VertexSRGB > 0.5h) c.rgb = SRGBToLinear(c.rgb);
                o.color = c;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = i.color.rgb * _BaseColor.rgb;
                half3 n = normalize(i.normalWS);
                Light L = GetMainLight();
                half ndl = saturate((dot(n, L.direction) + _Wrap) / (1.0h + _Wrap));
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(dot(n, v)), 3.0h) * _RimStrength;
                half3 lit = albedo * (SampleSH(n) + L.color * ndl) + rim * L.color * (albedo + 0.15h);
                half3 col = lerp(lit, albedo * _EmissionStrength, saturate(i.color.a));
                col = lerp(col, _FlashColor.rgb, saturate(_FlashColor.a));
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
