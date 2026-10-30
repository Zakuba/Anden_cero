
Shader "Custom/MatrixNoise"
{
    Properties
    {
        _Color ("Matrix Green", Color) = (0.05, 1, 0.15, 1)
        _Columns ("Column Density", Range(5, 150)) = 45
        _Rows ("Character Density", Range(10, 150)) = 65
        _Speed ("Fall Speed", Range(0.1, 10)) = 2
        _TrailLength ("Trail Length", Range(1, 40)) = 15
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Brightness ("Brightness", Range(0, 5)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Columns;
                float _Rows;
                float _Speed;
                float _TrailLength;
                float _Opacity;
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                float column = floor(uv.x * _Columns);
                float row = floor(uv.y * _Rows);

                float2 cell = float2(column, row);

                float randomColumn = Hash(float2(column, 12.7));

                float speedVariation = lerp(0.5, 1.5, randomColumn);

                float head = frac(
                    randomColumn + _Time.y * _Speed * speedVariation
                ) * _Rows;

                float distanceFromHead = frac(
                    (head - row) / _Rows
                ) * _Rows;

                float trail = 1.0 - smoothstep(
                    0.0, _TrailLength, distanceFromHead
                );

                float2 localUV = frac(
                    uv * float2(_Columns, _Rows)
                );

                float2 pixel = floor(localUV * float2(3, 5));

                float glyph = Hash(cell + pixel * 13.17);

                float character = step(0.42, glyph);

                float activeCell = step(
                    Hash(cell + 5.3), 0.78
                );

                float headGlow = 1.0 - smoothstep(
                    0.0, 1.5, distanceFromHead
                );

                float noise = Hash(cell + floor(_Time.y * 8.0));

                float intensity = trail * character * activeCell;

                intensity *= lerp(0.65, 1.0, noise);

                intensity += headGlow * character * 0.8;

                float3 color = _Color.rgb * intensity * _Brightness;

                float alpha = saturate(intensity) * _Opacity;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}
