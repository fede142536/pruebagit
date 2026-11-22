// Material de los jugadores (URP): color por vértice + luz de borde (rim) + suciedad en la parte baja del cuerpo.
// Barato a propósito para gama baja: sin texturas, sin luces reales, solo cálculos por vértice y por píxel simples.
// Si el dispositivo no lo admite, Materials.cs usa el material plano de siempre (no aparece la pantalla rosa).
// Los valores globales los pone el juego: _FootballRim (0 = apagado en calidad BAJA) y _FootballDirt (crece con los minutos).
Shader "Football/Player"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _RimColor ("Color del borde", Color) = (1, 0.97, 0.9, 1)
        _RimPower ("Dureza del borde", Range(0.5, 8)) = 3
        _RimStrength ("Intensidad del borde", Range(0, 1)) = 0.35
        _DirtColor ("Color de la suciedad", Color) = (0.22, 0.17, 0.1, 1)
        _DirtHeight ("Altura de la suciedad (m)", Range(0, 1.5)) = 0.75
        _Cull ("Caras", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half4 _DirtColor;
                half _DirtHeight;
                half _Cull;
            CBUFFER_END

            // Globales (fuera del CBUFFER: no rompen el agrupado SRP).
            half _FootballRim;
            half _FootballDirt;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR; // color de cada pieza (las piezas de un jugador van juntas en una malla)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half rim : TEXCOORD0;
                half dirt : TEXCOORD1;
                half3 color : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);

                // Luz de borde: más fuerte donde la superficie se aleja de la cámara (siluetas legibles sobre el césped).
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 viewWS = normalize(GetWorldSpaceViewDir(positionWS));
                half facing = saturate(abs(dot(normalWS, viewWS)));
                output.rim = pow(1.0h - facing, _RimPower) * _RimStrength * _FootballRim;

                // Suciedad: solo cerca de los pies, con un patrón irregular según la posición para que no sea una franja lisa.
                half low = saturate(1.0h - positionWS.y / max(_DirtHeight, 0.01h));
                half noise = frac(sin(dot(floor(positionWS.xz * 7.0), float2(12.9898, 78.233))) * 43758.5453);
                output.dirt = saturate(low * low * _FootballDirt * (0.6h + 0.4h * noise));
                output.color = input.color.rgb * _BaseColor.rgb;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = lerp(input.color, _DirtColor.rgb, input.dirt);
                color += _RimColor.rgb * input.rim;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
