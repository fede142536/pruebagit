// Material de los jugadores (URP), apto para gama baja. Una sola textura chica y compartida (atlas R8 de 256x256, 64 KB) para el
// dorsal y el escudo; el resto es color por vértice:
//  - color por vértice (las piezas de un jugador van juntas en una malla) y, en el alfa del vértice, el tipo de material:
//    1 piel, 0,75 tela, 0,5 botines, 0,25 pelo (lo pone PlayerModel);
//  - difuso envolvente (wrap) con la luz principal del estadio, para que las sombras no queden negras;
//  - luz de borde sutil para recortar la silueta sobre el césped;
//  - pliegues suaves en la tela (camiseta, pantalón, medias) según la posición en la malla;
//  - brillo especular en botines (fino e intenso) y en el pelo (ancho y suave);
//  - suciedad en la parte baja del cuerpo que crece con los minutos;
//  - piel con calidez subdérmica en la zona de penumbra y brillo de sudor que crece con los minutos (máscara en la UV.x);
//  - tela mate con trama fina y pliegues más marcados donde la máscara lo indica (UV.y: cintura, codos, ingle, tobillos).
// Las máscaras las pone SkinnedBodyGeometry; las mallas sin UV (público) usan los pliegues de siempre y no sudan.
//  - dorsal en la espalda y escudo en el pecho: la camiseta trae una proyección plana (UV2), qué dígitos y escudo (UV3) y el color de la
//    tinta (UV4); el shader lee la cobertura del atlas global _FootballAtlas y la mezcla con el color de la tela (sin geometría extra,
//    sin recorte de píxeles y sin MaterialPropertyBlock). Las mallas sin esos canales leen ceros y no dibujan nada;
//  - segunda pasada «PlanarShadow»: proyecta la silueta sobre el césped según la luz global (calidad MEDIA y ALTA), con stencil para que las
//    piernas y los brazos superpuestos no oscurezcan dos veces. El público usa Football/Crowd, que toma solo la primera pasada (UsePass).
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
        _SubsurfaceColor ("Color subdérmico", Color) = (0.85, 0.25, 0.15, 1)
        _Subsurface ("Intensidad subdérmica", Range(0, 1)) = 0.18
        _SweatSpecular ("Brillo del sudor", Range(0, 1)) = 0.35
        _SweatGloss ("Dureza del brillo del sudor", Range(4, 128)) = 40
        _WeaveStrength ("Trama de la tela", Range(0, 0.3)) = 0.05
        _WeaveFrequency ("Frecuencia de la trama", Range(50, 600)) = 260
        _ShadowColor ("Color de la sombra proyectada", Color) = (0.02, 0.08, 0.03, 1)
        _ShadowStrength ("Intensidad de la sombra proyectada", Range(0, 1)) = 0.4
        _ShadowHeight ("Altura del plano de la sombra (m)", Range(0, 0.1)) = 0.015
        _Cull ("Caras", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Body"
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
                half4 _SubsurfaceColor;
                half _Subsurface;
                half _SweatSpecular;
                half _SweatGloss;
                half _WeaveStrength;
                half _WeaveFrequency;
                half4 _ShadowColor;
                half _ShadowStrength;
                half _ShadowHeight;
                half _Cull;
            CBUFFER_END

            // Globales (fuera del CBUFFER: no rompen el agrupado SRP).
            half _FootballRim;
            half _FootballDirt;
            half4 _FootballLightDir;   // dirección hacia la luz principal (focos del estadio), normalizada
            half _FootballLightAmount; // 0 = sin sombreado ni brillos (calidad baja), 1 = volumen completo
            half _FootballSweat;       // 0 al empezar, 1 al final del partido
            half _FootballShadow;      // 1 = sombra proyectada encendida (calidad MEDIA y ALTA)
            TEXTURE2D(_FootballAtlas);
            SAMPLER(sampler_FootballAtlas);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR; // rgb: color de la pieza; a: tipo de material
                float2 masks : TEXCOORD0; // x: sudor (piel), y: pliegues (tela); 0 si la malla no las trae
                float3 rest : TEXCOORD1;  // posición en reposo (malla con huesos): pliegues y trama pegados a la tela
                float3 decalUv : TEXCOORD2;   // proyección plana del decal: x, y = parche; z = −1 dorsal, +1 escudo, 0 nada
                float3 decalData : TEXCOORD3; // x = decena (−1 si no hay), y = unidad (−1 si no hay dorsal), z = variante de escudo (−1 si no hay)
                float3 ink : TEXCOORD4;       // color de la tinta
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : TEXCOORD0;   // rgb color, a tipo de material
                half3 normalWS : TEXCOORD1;
                half3 viewWS : TEXCOORD2;
                half dirt : TEXCOORD3;
                half fold : TEXCOORD4;
                half2 masks : TEXCOORD5;
                half weave : TEXCOORD6;
                float3 decalUv : TEXCOORD7;
                half3 decalData : TEXCOORD8;
                half3 ink : TEXCOORD9;
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

                // Pliegues: ondas suaves e inclinadas en el espacio de la pieza. En la malla con huesos se usa la posición de
                // reposo, así acompañan a la tela al moverse en lugar de «nadar»; las mallas sin ese canal usan la suya.
                float hasRest = step(1e-6, dot(input.rest, input.rest));
                float3 p = lerp(input.positionOS.xyz, input.rest, hasRest);
                output.fold = (half)(p.y * _FoldFrequency + p.x * _FoldFrequency * 0.6 + sin(p.z * _FoldFrequency * 0.5) * 1.5);
                output.masks = (half2)input.masks;
                output.decalUv = input.decalUv;
                output.decalData = (half3)input.decalData;
                output.ink = (half3)input.ink;
                // Trama: dos ondas cruzadas finas en el espacio de la pieza (se interpolan; no hace falta textura).
                output.weave = (half)(sin(p.x * _WeaveFrequency + p.y * _WeaveFrequency) * sin(p.y * _WeaveFrequency - p.z * _WeaveFrequency));
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
                half skin = step(0.9h, id);
                half sweatMask = input.masks.x;
                half foldMask = input.masks.y + step(input.masks.y, 0.0001h); // sin máscara: pliegues de siempre

                // Difuso envolvente: la luz «da la vuelta» un poco, como en la piel y la tela reales.
                half ndl = dot(n, l);
                half wrapped = saturate((ndl + _Wrap) / (1.0h + _Wrap));
                half diffuse = lerp(1.0h, wrapped * _LightGain + _Ambient, _FootballLightAmount);

                half fold = 1.0h - _FoldStrength * fabric * foldMask * (0.5h + 0.5h * sin(input.fold)) * _FootballLightAmount;
                fold *= 1.0h - _WeaveStrength * fabric * (0.5h + 0.5h * input.weave) * _FootballLightAmount;

                // Piel: la luz que atraviesa la piel calienta la penumbra (subdérmica, barata: sin texturas ni pasadas).
                half terminator = saturate(1.0h - abs(ndl)) * skin * _Subsurface * _FootballLightAmount;

                half3 h = normalize(l + v);
                half nh = saturate(dot(n, h));
                half specular = (boots * pow(nh, _BootGloss) * _BootSpecular + hair * pow(nh, _HairGloss) * _HairSpecular
                                 + skin * sweatMask * _FootballSweat * pow(nh, _SweatGloss) * _SweatSpecular)
                                * _FootballLightAmount * step(0.0h, ndl);

                half rim = pow(1.0h - saturate(abs(dot(n, v))), _RimPower) * _RimStrength * _FootballRim;

                // Decales de la camiseta: dorsal en la espalda (z < 0) y escudo en el pecho (z > 0), leídos del atlas por la proyección plana.
                half3 albedo = input.color.rgb;
                half flag = (half)input.decalUv.z;
                if (abs(flag) > 0.5h)
                {
                    float2 patch = input.decalUv.xy;
                    half cell = -1.0h;
                    float local = 0.0;
                    if (flag < 0.0h)
                    {
                        // Con dos dígitos, cada uno ocupa la mitad del parche; con uno, el del medio.
                        if (input.decalData.x >= 0.0h) { cell = patch.x < 0.5 ? input.decalData.x : input.decalData.y; local = frac(patch.x * 2.0); }
                        else if (patch.x >= 0.25 && patch.x <= 0.75) { cell = input.decalData.y; local = (patch.x - 0.25) * 2.0; }
                    }
                    else if (input.decalData.z >= 0.0h)
                    {
                        cell = 10.0h + input.decalData.z;
                        local = patch.x;
                    }

                    if (cell >= 0.0h && patch.x >= 0.0 && patch.x <= 1.0 && patch.y >= 0.0 && patch.y <= 1.0)
                    {
                        float2 cellOrigin = float2(fmod(cell, 4.0), floor(cell * 0.25));
                        float2 atlasUv = (cellOrigin + float2(local, patch.y) * 0.875 + 0.0625) * 0.25; // margen de 4 px por celda
                        half coverage = SAMPLE_TEXTURE2D_LOD(_FootballAtlas, sampler_FootballAtlas, atlasUv, 0).r;
                        albedo = lerp(albedo, input.ink, smoothstep(0.42h, 0.58h, coverage) * 0.94h);
                    }
                }

                half3 color = lerp(albedo, _DirtColor.rgb, input.dirt) * diffuse * fold;
                color += input.color.rgb * _SubsurfaceColor.rgb * terminator;
                color += specular.xxx + _RimColor.rgb * rim;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Sombra proyectada sobre el césped: aplasta la silueta en el plano y = _ShadowHeight siguiendo la luz global. Se mezcla con el fondo
        // (la cancha se dibuja antes: los jugadores usan una cola de dibujo posterior) y el stencil deja pasar un solo fragmento por píxel.
        Pass
        {
            Name "PlanarShadow"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex VertShadow
            #pragma fragment FragShadow
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
                half4 _SubsurfaceColor;
                half _Subsurface;
                half _SweatSpecular;
                half _SweatGloss;
                half _WeaveStrength;
                half _WeaveFrequency;
                half4 _ShadowColor;
                half _ShadowStrength;
                half _ShadowHeight;
                half _Cull;
            CBUFFER_END

            half4 _FootballLightDir;
            half _FootballShadow;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings VertShadow(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                float3 l = _FootballLightDir.xyz;
                float height = max(p.y - _ShadowHeight, 0.0);
                // Se desplaza el vértice por el rayo de luz hasta el plano del césped (la luz nunca queda casi horizontal).
                float3 onGround = p - l * (height / max(l.y, 0.25));
                onGround.y = _ShadowHeight;
                output.positionCS = TransformWorldToHClip(onGround);
                if (_FootballShadow < 0.5h)
                {
                    output.positionCS = float4(2.0, 2.0, 2.0, 1.0); // apagada: el triángulo queda fuera de la pantalla
                }

                return output;
            }

            half4 FragShadow(ShadowVaryings input) : SV_Target
            {
                return half4(_ShadowColor.rgb, _ShadowStrength);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
