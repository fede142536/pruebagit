// Material del público, los bancos y los fotógrafos: es el cuerpo del shader de jugadores (Football/Player) sin la sombra proyectada.
// Reutiliza esa pasada con UsePass para no duplicar el código; las propiedades se repiten tal cual porque el agrupado SRP exige que
// coincidan con el CBUFFER de la pasada. No lleva decales (sus mallas no traen esos canales).
Shader "Football/Crowd"
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

        UsePass "Football/Player/BODY"
    }

    Fallback Off
}
