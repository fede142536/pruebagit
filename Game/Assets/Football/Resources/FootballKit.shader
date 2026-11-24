// Equipación plana de POTRERO (camiseta, short y medias), URP. Pensado para gama baja y para el estilo «cero adornos» del manual:
//  - Colores 100 % planos: la textura máscara solo dice QUÉ color va en cada píxel; el color sale de tres tintas por renderer.
//      máscara  R = color base (primario) · G = patrón secundario · B = cuello, puños y detalles (acento).
//    Prioridad B > G > R; un píxel sin ningún canal (fuera de la prenda) toma el primario. Se leen con step(0,5): corte neto, sin degradado.
//  - La máscara es un atlas horizontal de 8 celdas (6 camisetas + short + medias) que se elige con _MaskCell. Tiene que ser Point, sin mipmaps,
//    sin compresión y lineal (KitRenderer la crea así); si se reemplaza por arte propio, importarla con esos ajustes.
//  - Sin brillo especular ni sombras suaves: la luz es de dos tonos (step) con la luz del estadio (_FootballLightDir, como el resto de los
//    jugadores). Con _FootballLightAmount = 0 (o _ShadeFloor = 1) queda totalmente plano, que es lo que usa el editor de camisetas.
//  - Grilla técnica opcional (solo vista previa): líneas finas y marcas «+» en las intersecciones de la parte alta, sobre el color base.
// Las tintas, la celda y la grilla son propiedades por instancia: KitRenderer las cambia con un MaterialPropertyBlock compartido (sin crear
// materiales por jugador) y, al estar declaradas como instanciadas, los renderers con el mismo material se agrupan con GPU instancing.
// Contrapartida conocida: un material con propiedades instanciadas no entra en el agrupado SRP Batcher (para eso habría que usar un material por
// equipación). Los renderers con huesos no usan instancing; el MaterialPropertyBlock funciona igual.
Shader "Football/Kit"
{
    Properties
    {
        _MaskAtlas ("Máscara RGB (atlas de 8 celdas)", 2D) = "black" {}
        _PrimaryColor ("Color primario (R)", Color) = (0.059, 0.102, 0.090, 1)
        _SecondaryColor ("Color secundario (G)", Color) = (0.910, 0.937, 0.914, 1)
        _TrimColor ("Color de detalle (B)", Color) = (0.247, 0.714, 0.478, 1)
        _MaskCell ("Celda de la máscara (0-7)", Float) = 0
        _CellCount ("Celdas del atlas", Float) = 8
        _ShadeFloor ("Tono de la sombra (1 = plano)", Range(0, 1)) = 0.86
        _ShadeThreshold ("Umbral de la sombra", Range(-1, 1)) = 0
        _GridEnabled ("Grilla técnica (0/1)", Float) = 0
        _GridColor ("Color de la grilla", Color) = (0.165, 0.231, 0.204, 1)
        _GridCells ("Celdas de la grilla (x, y)", Vector) = (16, 16, 0, 0)
        _GridLine ("Grosor de la línea (fracción de celda)", Range(0, 0.2)) = 0.04
        _MarkLength ("Largo de la marca + (fracción de celda)", Range(0, 0.5)) = 0.16
        _MarkMinV ("Las marcas + empiezan en V", Range(0, 1)) = 0.5
        _Cull ("Caras (2 = solo frente)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Kit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MaskAtlas);
            SAMPLER(sampler_MaskAtlas); // el filtro (Point) lo fija la textura

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskAtlas_ST;
                half _CellCount;
                half _ShadeFloor;
                half _ShadeThreshold;
                half4 _GridColor;
                float4 _GridCells;
                half _GridLine;
                half _MarkLength;
                half _MarkMinV;
                half _Cull;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerKit)
                UNITY_DEFINE_INSTANCED_PROP(half4, _PrimaryColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _SecondaryColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _TrimColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _MaskCell)
                UNITY_DEFINE_INSTANCED_PROP(half, _GridEnabled)
            UNITY_INSTANCING_BUFFER_END(PerKit)

            // Globales del estadio (las mismas que usa Football/Player). Fuera del CBUFFER.
            half4 _FootballLightDir;    // hacia la luz principal, normalizada
            half _FootballLightAmount;  // 0 = sin sombreado (plano), 1 = sombra de dos tonos

            static const half Half = 0.5h;         // umbral de las máscaras: corte neto
            static const float EdgeInset = 0.0005; // margen para no leer la celda vecina del atlas con filtro Point

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            // 1 donde cae una línea fina de la grilla o una marca «+» más gruesa en las intersecciones de la parte alta (hombros y pecho).
            half GridMask(float2 uv)
            {
                float2 f = frac(uv * _GridCells.xy);
                float2 d = min(f, 1.0 - f); // distancia a la línea más cercana, en fracción de celda
                half thin = max(step(d.x, _GridLine * 0.5h), step(d.y, _GridLine * 0.5h));
                half armX = step(d.y, _GridLine) * step(d.x, _MarkLength);
                half armY = step(d.x, _GridLine) * step(d.y, _MarkLength);
                half mark = max(armX, armY) * step(_MarkMinV, uv.y);
                return max(thin, mark);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half cell = UNITY_ACCESS_INSTANCED_PROP(PerKit, _MaskCell);
                float2 atlasUv = float2((floor(cell + 0.5h) + clamp(i.uv.x, EdgeInset, 1.0 - EdgeInset)) / _CellCount, saturate(i.uv.y));
                half3 mask = SAMPLE_TEXTURE2D(_MaskAtlas, sampler_MaskAtlas, atlasUv).rgb;

                half isTrim = step(Half, mask.b);
                half isSecondary = step(Half, mask.g) * (1.0h - isTrim);
                half3 color = UNITY_ACCESS_INSTANCED_PROP(PerKit, _PrimaryColor).rgb;
                color = lerp(color, UNITY_ACCESS_INSTANCED_PROP(PerKit, _SecondaryColor).rgb, isSecondary);
                color = lerp(color, UNITY_ACCESS_INSTANCED_PROP(PerKit, _TrimColor).rgb, isTrim);

                half onBase = (1.0h - isSecondary) * (1.0h - isTrim);
                half grid = step(Half, UNITY_ACCESS_INSTANCED_PROP(PerKit, _GridEnabled)) * onBase * GridMask(i.uv);
                color = lerp(color, _GridColor.rgb, grid);

                // Luz de dos tonos: corte neto, sin degradado ni brillo.
                half lit = step(_ShadeThreshold, dot(normalize(i.normalWS), _FootballLightDir.xyz));
                half shade = lerp(1.0h, lerp(_ShadeFloor, 1.0h, lit), _FootballLightAmount);
                return half4(color * shade, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    Fallback Off
}
