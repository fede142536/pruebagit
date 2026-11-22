// Material de los jugadores (URP), sin texturas y apto para gama baja:
//  - color por vértice (las piezas de un jugador van juntas en una malla) y, en el alfa del vértice, el tipo de material:
//    1 piel, 0,75 tela, 0,5 botines, 0,25 pelo (lo pone PlayerModel);
//  - difuso envolvente (wrap) con la luz principal del estadio, para que las sombras no queden negras;
//  - luz de borde sutil para recortar la silueta sobre el césped;
//  - pliegues suaves en la tela (camiseta, pantalón, medias) según la posición en la malla;
//  - brillo especular en botines (fino e intenso) y en el pelo (ancho y suave);
//  - suciedad en la parte baja del cuerpo que crece con los minutos.
// Todas las propiedades del material están en el CBUFFER UnityPerMaterial (compatible con el agrupado SRP Batcher); los
// valores que cambian en juego son globales (_FootballRim, _FootballDirt, _FootballLightDir, _FootballLightAmount), así no
// hace falta MaterialPropertyBlock ni se crean objetos por fotograma.
// Si el dispositivo no lo admite, Materials.cs usa el material plano de siempre (no aparece la pantalla rosa).
Shader "Football/Player"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _RimColor ("Color del borde", Color) = (1, 0.97, 0.9, 1)
        _RimPower ("Dureza del borde", Range(0.5, 8)) = 3.5
        _RimStrength ("Intensidad del borde", Range(0, 1)) = 0.22
        _DirtColor ("Color de la suciedad", Color) = (0.22, 0.17, 0.1, 1)
        _DirtHeight ("Altura de la suciedad (m)", Range(0, 1.5)) = 0.75
        _Wrap ("Difuso envolvente", Range(0, 1)) = 0.5
        _LightGain ("Ganancia de la luz", Range(0, 2)) = 0.95
        _Ambient ("Luz ambiente", Range(0, 1)) = 0.3
        _FoldStrength ("Pliegues de la tela", Range(0, 0.5)) = 0.08
        _FoldFrequency ("Frecuencia de los pliegues", Range(1, 80)) = 34
        _BootSpecular ("Brillo de los botines", Range(0, 2)) = 0.7
        _BootGloss ("Dureza del brillo de los botines", Range(4, 128)) = 48
        _HairSpecular ("Brillo del pelo", Range(0, 1)) = 0.22
        _HairGloss ("Dureza del brillo del pelo", Range(2, 64)) = 10
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
                half _Wrap;
                half _LightGain;
                half _Ambient;
                half _FoldStrength;
                half _FoldFrequency;
                half _BootSpecular;
                half _BootGloss;
                half _HairSpecular;
                half _HairGloss;
                half _Cull;
            CBUFFER_END

            // Globales (fuera del CBUFFER: no rompen el agrupado SRP).
            half _FootballRim;
            half _FootballDirt;
            half4 _FootballLightDir;   // dirección hacia la luz principal (focos del estadio), normalizada
            half _FootballLightAmount; // 0 = sin sombreado ni brillos (calidad baja), 1 = volumen completo

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR; // rgb: color de la pieza; a: tipo de material
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : TEXCOORD0;   // rgb color, a tipo de material
                half3 normalWS : TEXCOORD1;
                half3 viewWS : TEXCOORD2;
                half dirt : TEXCOORD3;
                half fold : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.color = half4(input.color.rgb * _BaseColor.rgb, input.color.a);

                // Suciedad: solo cerca de los pies, con un patrón irregular según la posición para que no sea una franja lisa.
                half low = saturate(1.0h - positionWS.y / max(_DirtHeight, 0.01h));
                half noise = frac(sin(dot(floor(positionWS.xz * 7.0), float2(12.9898, 78.233))) * 43758.5453);
                output.dirt = saturate(low * low * _FootballDirt * (0.6h + 0.4h * noise));

                // Pliegues: ondas suaves e inclinadas en el espacio de la pieza (se mueven con el cuerpo, no «nadan»).
                float3 p = input.positionOS.xyz;
                output.fold = (half)(p.y * _FoldFrequency + p.x * _FoldFrequency * 0.6 + sin(p.z * _FoldFrequency * 0.5) * 1.5);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 v = normalize(input.viewWS);
                half3 l = _FootballLightDir.xyz;
                half id = input.color.a;
                half fabric = step(0.6h, id) * step(id, 0.9h);
                half boots = step(0.4h, id) * step(id, 0.6h);
                half hair = step(id, 0.4h);

                // Difuso envolvente: la luz «da la vuelta» un poco, como en la piel y la tela reales.
                half ndl = dot(n, l);
                half wrapped = saturate((ndl + _Wrap) / (1.0h + _Wrap));
                half diffuse = lerp(1.0h, wrapped * _LightGain + _Ambient, _FootballLightAmount);

                half fold = 1.0h - _FoldStrength * fabric * (0.5h + 0.5h * sin(input.fold)) * _FootballLightAmount;

                half3 h = normalize(l + v);
                half nh = saturate(dot(n, h));
                half specular = (boots * pow(nh, _BootGloss) * _BootSpecular + hair * pow(nh, _HairGloss) * _HairSpecular)
                                * _FootballLightAmount * step(0.0h, ndl);

                half rim = pow(1.0h - saturate(abs(dot(n, v))), _RimPower) * _RimStrength * _FootballRim;

                half3 color = lerp(input.color.rgb, _DirtColor.rgb, input.dirt) * diffuse * fold;
                color += specular.xxx + _RimColor.rgb * rim;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
