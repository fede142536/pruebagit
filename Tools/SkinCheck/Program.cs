// Comprobaciones de la malla con huesos: triángulos de LOD0 y LOD1, pesos, continuidad de brazos y piernas al doblar,
// oclusión ambiental pintada, decales (dorsal y escudo), atlas y proporción.
using System;
using System.Collections.Generic;
using System.Linq;
using Football.Game;
using UnityEngine;

static class P
{
    static BodySpec Spec(int hair, int facial, int pattern, bool trousers = false, int number = 10, int crest = 0) => new BodySpec
    {
        Shirt = new Color(0.2f, 0.3f, 0.8f), Secondary = Color.white, Pattern = pattern, Shorts = Color.white, Socks = new Color(0.2f, 0.3f, 0.8f),
        Skin = new Color(0.8f, 0.6f, 0.5f), Hair = new Color(0.1f, 0.1f, 0.1f), Boots = new Color(0.1f, 0.1f, 0.1f), HairStyle = hair, FacialHair = facial,
        Trousers = trousers, LongSleeves = trousers, Number = number, Crest = crest
    };

    static int fails;
    static void Check(bool ok, string what) { if (!ok) { fails++; Console.WriteLine("  FALLA: " + what); } }

    static Vector3 RotX(Vector3 p, Vector3 c, float deg)
    {
        float a = deg * Mathf.Deg2Rad; var d = p - c;
        return c + new Vector3(d.x, d.y * Mathf.Cos(a) - d.z * Mathf.Sin(a), d.y * Mathf.Sin(a) + d.z * Mathf.Cos(a));
    }

    static bool IsAncestor(int anc, int b) { while (b >= 0) { if (b == anc) return true; b = SkinnedBodyGeometry.Parents[b]; } return false; }

    // Piel lineal: el hueso girado y todos sus descendientes (como el SkinnedMeshRenderer con dos huesos por vértice).
    static Vector3[] Skin(SkinnedBodyGeometry g, int bone, float deg, bool rigid)
    {
        var c = SkinnedBodyGeometry.BindPositions[bone];
        var res = new Vector3[g.Vertices.Count];
        for (int i = 0; i < res.Length; i++)
        {
            var v = g.Vertices[i];
            int main = g.Weight0[i] >= 0.5f ? g.Bone0[i] : g.Bone1[i];
            Vector3 p0 = IsAncestor(bone, rigid ? main : g.Bone0[i]) ? RotX(v, c, deg) : v;
            Vector3 p1 = IsAncestor(bone, g.Bone1[i]) ? RotX(v, c, deg) : v;
            float w = rigid ? 1f : g.Weight0[i];
            res[i] = p0 * w + p1 * (1 - w);
        }
        return res;
    }

    static int[] Shells(SkinnedBodyGeometry g)
    {
        var parent = Enumerable.Range(0, g.Vertices.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        for (int t = 0; t < g.Triangles.Count; t += 3) { parent[Find(g.Triangles[t])] = Find(g.Triangles[t + 1]); parent[Find(g.Triangles[t + 1])] = Find(g.Triangles[t + 2]); }
        return Enumerable.Range(0, g.Vertices.Count).Select(Find).ToArray();
    }

    // Mayor cociente entre el largo de una arista doblada y el de reposo, en las aristas del tramo que tocan la articulación.
    static float MaxStretch(SkinnedBodyGeometry g, Vector3[] posed, int group, Vector3 joint, float radius)
    {
        float worst = 1f;
        for (int t = 0; t < g.Triangles.Count; t += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = g.Triangles[t + k], b = g.Triangles[t + (k + 1) % 3];
                if (g.Group[a] != group || g.Group[b] != group || (g.Vertices[a] - joint).magnitude > radius) continue;
                float rest = (g.Vertices[a] - g.Vertices[b]).magnitude, now = (posed[a] - posed[b]).magnitude;
                if (rest > 1e-4f) worst = Math.Max(worst, now / rest);
            }
        return worst;
    }

    // Decal interpolado en el punto (x, y) de la proyección frontal sobre la cara pedida (z<0 espalda, z>0 pecho).
    static Vector3? DecalAt(SkinnedBodyGeometry g, float x, float y, float side)
    {
        for (int t = 0; t < g.Triangles.Count; t += 3)
        {
            int a = g.Triangles[t], b = g.Triangles[t + 1], c = g.Triangles[t + 2];
            if (Math.Sign(g.Vertices[a].z) != Math.Sign(side) || Math.Sign(g.Vertices[b].z) != Math.Sign(side) || Math.Sign(g.Vertices[c].z) != Math.Sign(side)) continue;
            if (g.DecalUv[a].z == 0f || g.DecalUv[b].z == 0f || g.DecalUv[c].z == 0f) continue;
            Vector3 pa = g.Vertices[a], pb = g.Vertices[b], pc = g.Vertices[c];
            float den = (pb.y - pc.y) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.y - pc.y);
            if (Math.Abs(den) < 1e-9f) continue;
            float l1 = ((pb.y - pc.y) * (x - pc.x) + (pc.x - pb.x) * (y - pc.y)) / den;
            float l2 = ((pc.y - pa.y) * (x - pc.x) + (pa.x - pc.x) * (y - pc.y)) / den;
            float l3 = 1f - l1 - l2;
            if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
            return g.DecalUv[a] * l1 + g.DecalUv[b] * l2 + g.DecalUv[c] * l3;
        }
        return null;
    }

    static void Main()
    {
        Console.WriteLine("Huesos: " + SkinnedBodyGeometry.BoneCount + " (" + string.Join(", ", SkinnedBodyGeometry.BoneNames) + ")");

        // 1) Triángulos de los dos niveles, para todos los peinados, vellos y diseños.
        foreach (int lod in new[] { 0, 1 })
        {
            int min = int.MaxValue, max = 0, sum = 0, n = 0;
            for (int hair = 0; hair < 6; hair++)
                for (int facial = 0; facial < 3; facial++)
                    for (int pattern = 0; pattern < 6; pattern++)
                    {
                        int t = SkinnedBodyGeometry.Build(Spec(hair, facial, pattern), lod).TriangleCount;
                        min = Math.Min(min, t); max = Math.Max(max, t); sum += t; n++;
                    }
            int suit = SkinnedBodyGeometry.Build(Spec(1, 1, 0, true), lod).TriangleCount;
            Console.WriteLine($"LOD{lod}: {min}–{max} triángulos (media {sum / n}); DT de traje {suit}");
            if (lod == 0) Check(sum / n >= 2300 && sum / n <= 2750, "LOD0 debería rondar los 2.500 triángulos");
            else Check(max <= 900 && min >= 780, "LOD1 debería estar entre 800 y 900 triángulos");
        }

        var body = SkinnedBodyGeometry.Build(Spec(0, 1, 1), 0);
        var low = SkinnedBodyGeometry.Build(Spec(0, 1, 1), 1);
        Console.WriteLine($"Vértices: LOD0 {body.Vertices.Count}, LOD1 {low.Vertices.Count}");

        // 2) Pesos válidos de dos huesos, en los dos niveles.
        foreach (var g in new[] { body, low })
        {
            int bad = 0, blended = 0; var used = new bool[20];
            for (int i = 0; i < g.Vertices.Count; i++)
            {
                int b0 = g.Bone0[i], b1 = g.Bone1[i]; float w = g.Weight0[i];
                used[b0] = true; if (w < 1) used[b1] = true;
                if (w < 0 || w > 1 || b0 < 0 || b0 >= 20 || b1 < 0 || b1 >= 20) bad++;
                if (w < 0.999f && !(IsAncestor(b0, b1) || IsAncestor(b1, b0))) bad++;
                if (w < 0.999f) blended++;
            }
            Console.WriteLine($"  LOD{g.Lod}: pesos inválidos {bad}; vértices con dos huesos {blended}; huesos con vértices {used.Count(u => u)}/20");
            Check(bad == 0, "pesos inválidos en LOD" + g.Lod);
            Check(used.Count(u => u) >= 19, "casi todos los huesos deben mover vértices (LOD" + g.Lod + ")");
        }

        // 3) Continuidad de brazos y piernas: una sola superficie por tramo y tres anillos de soporte en cada flexión.
        int[] shell = Shells(body);
        (string name, int group, int joint, int[] ends, float deg)[] limbs =
        {
            ("rodilla izq.", 1, 13, new[] { 12, 14 }, 100f), ("rodilla der.", 2, 17, new[] { 16, 18 }, 100f),
            ("codo izq.", 3, 7, new[] { 6, 8 }, -100f), ("codo der.", 4, 10, new[] { 9, 11 }, -100f)
        };
        foreach (var l in limbs)
        {
            var idx = Enumerable.Range(0, body.Vertices.Count).Where(i => body.Group[i] == l.group).ToArray();
            int components = idx.Select(i => shell[i]).Distinct().Count();
            var joint = SkinnedBodyGeometry.BindPositions[l.joint];
            float r = l.group <= 2 ? 0.06f : 0.06f;
            int rings = idx.Where(i => Math.Abs(body.Vertices[i].y - joint.y) <= r + 0.001f).Select(i => (float)Math.Round(body.Vertices[i].y, 3)).Distinct().Count();
            float top = idx.Max(i => body.Vertices[i].y), bottom = idx.Min(i => body.Vertices[i].y);
            float soft = MaxStretch(body, Skin(body, l.joint, l.deg, false), l.group, joint, 0.12f);
            float hard = MaxStretch(body, Skin(body, l.joint, l.deg, true), l.group, joint, 0.12f);
            Console.WriteLine($"  {l.name,-13} superficie única: {components} pieza(s), de y={top:0.00} a {bottom:0.00}; {rings} anillos en ±6 cm; a {Math.Abs(l.deg):0}° estira como mucho {soft:0.00}x (piel rígida {hard:0.00}x)");
            Check(components == 1, l.name + ": debe ser una sola superficie");
            Check(rings >= 3, l.name + ": hacen falta 3 anillos de soporte");
            Check(soft <= hard + 1e-3f, l.name + ": los pesos no deben estirar más que la piel rígida");
        }

        // 3b) Mismo estiramiento medido con el esqueleto doblado en el LOD1.
        foreach (var l in limbs.Take(1).Concat(limbs.Skip(2).Take(1)))
        {
            var j = SkinnedBodyGeometry.BindPositions[l.joint];
            Console.WriteLine($"  LOD1 {l.name,-13} a {Math.Abs(l.deg):0}°: {MaxStretch(low, Skin(low, l.joint, l.deg, false), l.group, j, 0.12f):0.00}x");
        }

        // 4) Oclusión ambiental pintada: rincones entre 15 y 25 %, nunca más del tope.
        float minAo = body.Colors.Select((c, i) => body.BaseColors[i].r > 0.01f ? c.r / body.BaseColors[i].r : 1f).Min();
        int dark = body.Colors.Where((c, i) => body.BaseColors[i].r > 0.01f && c.r / body.BaseColors[i].r < 0.95f).Count();
        Console.WriteLine($"  AO: mínimo {minAo:0.00}, {dark} de {body.Colors.Count} vértices oscurecidos");
        Check(minAo >= 1f - SkinnedBodyGeometry.MaxOcclusion - 1e-3f, "AO por debajo del tope");
        (string zone, Vector3 p)[] zones =
        {
            ("bajo el mentón", new Vector3(0f, 1.52f, 0.05f)), ("axila", new Vector3(0.2f, 1.3f, 0f)), ("entrepierna", new Vector3(0f, 0.85f, 0f)),
            ("borde interno de manga", new Vector3(0.19f, 1.285f, 0f)), ("bajo el ruedo de la camiseta", new Vector3(0f, 0.96f, 0f)),
            ("bajo el ruedo del short", new Vector3(0.11f, 0.7f, 0f)), ("pliegue de la media", new Vector3(0.11f, 0.42f, 0f)), ("espacio abierto (pecho)", new Vector3(0f, 1.3f, 0.2f))
        };
        foreach (var z in zones)
        {
            float ao = SkinnedBodyGeometry.Occlusion(z.p);
            Console.WriteLine($"    {z.zone,-30} {100f * (1f - ao):0}% más oscuro");
            if (z.zone.StartsWith("espacio")) Check(ao > 0.999f, "el pecho abierto no debe oscurecerse");
            else Check(ao <= 0.86f && ao >= 0.75f, z.zone + ": debería oscurecer entre 14 y 25 %");
        }

        // 5) Decales: dorsal de 1 o 2 dígitos y escudo, proyectados sobre la camiseta. UV es lineal en (x, y), así que se
        // comprueba el centro de cada parche buscando el triángulo que lo contiene en la proyección frontal.
        foreach (int number in new[] { 7, 23, 0 })
        {
            var g = SkinnedBodyGeometry.Build(Spec(0, 0, 0, false, number, 2), 0);
            var sample = Enumerable.Range(0, g.Vertices.Count).First(i => g.DecalUv[i].z != 0f);
            var d = g.DecalData[sample];
            Vector3? backUv = DecalAt(g, 0f, 1.285f, -1f), crestUv = DecalAt(g, 0.095f, 1.34f, +1f);
            Console.WriteLine($"  Dorsal {number}: decena {d.x}, unidad {d.y}, escudo {d.z}; centro del parche de la espalda uv=({backUv?.x:0.00},{backUv?.y:0.00}) flag {backUv?.z:0.0}; del escudo uv=({crestUv?.x:0.00},{crestUv?.y:0.00}) flag {crestUv?.z:0.0}");
            Check(backUv.HasValue && Math.Abs(backUv.Value.x - 0.5f) < 0.02f && Math.Abs(backUv.Value.y - 0.5f) < 0.02f && backUv.Value.z < -0.9f, "el centro del dorsal debe caer en uv (0,5; 0,5) con bandera de espalda");
            Check(crestUv.HasValue && Math.Abs(crestUv.Value.x - 0.5f) < 0.02f && Math.Abs(crestUv.Value.y - 0.5f) < 0.02f && crestUv.Value.z > 0.9f, "el centro del escudo debe caer en uv (0,5; 0,5) con bandera de pecho");
            Check(d.y == (number > 0 ? number % 10 : -1) && d.x == (number >= 10 ? number / 10 : -1), "dígitos del decal");
        }

        // 6) Atlas R8 de 256×256.
        byte[] atlas = PlayerAtlasData.Build();
        Check(atlas.Length == 256 * 256, "el atlas debe tener 64 KB");
        var coverage = Enumerable.Range(0, 16).Select(c => Enumerable.Range(0, 64 * 64).Count(k => PlayerAtlasData.Sample(atlas, c, k % 64, k / 64) > 127)).ToArray();
        Console.WriteLine($"  Atlas {atlas.Length / 1024} KB; cobertura por celda (px): {string.Join(" ", coverage)}");
        for (int c = 0; c < 16; c++)
            Check(c < 16 - 0 && (c >= 10 + PlayerAtlasData.CrestCount ? coverage[c] == 0 : coverage[c] > 300), "celda " + c);
        Check(Enumerable.Range(0, 10).Select(d => coverage[d]).Distinct().Count() >= 8, "los dígitos deben ser distintos");

        // 7) Proporciones.
        float topY = body.Vertices.Max(v => v.y), chin = SkinnedBodyGeometry.BindPositions[5].y;
        Console.WriteLine($"  Altura {topY:0.00} m · cabeza {topY - chin:0.000} m · {topY / (topY - chin):0.0} cabezas");
        Console.WriteLine(fails == 0 ? "TODO EN ORDEN" : fails + " COMPROBACIONES FALLARON");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
