// Comprobaciones de la malla con huesos: triángulos por variante, pesos válidos, costuras al doblar y proporción.
using System;
using System.Linq;
using Football.Game;
using UnityEngine;
static class P
{
    static BodySpec Spec(int hair, int facial, int pattern, bool trousers = false) => new BodySpec
    { Shirt = new Color(0.2f, 0.3f, 0.8f), Secondary = Color.white, Pattern = pattern, Shorts = Color.white, Socks = new Color(0.2f, 0.3f, 0.8f),
      Skin = new Color(0.8f, 0.6f, 0.5f), Hair = new Color(0.1f, 0.1f, 0.1f), Boots = new Color(0.1f, 0.1f, 0.1f), HairStyle = hair, FacialHair = facial, Trousers = trousers, LongSleeves = trousers };

    // Rotación de un punto alrededor del eje X que pasa por c.
    static Vector3 RotX(Vector3 p, Vector3 c, float deg) { float a = deg * Mathf.Deg2Rad; var d = p - c; return c + new Vector3(d.x, d.y * Mathf.Cos(a) - d.z * Mathf.Sin(a), d.y * Mathf.Sin(a) + d.z * Mathf.Cos(a)); }

    static bool IsAncestor(int anc, int b) { while (b >= 0) { if (b == anc) return true; b = SkinnedBodyGeometry.Parents[b]; } return false; }

    // Pose: un hueso girado en X; sus descendientes lo acompañan (piel lineal, como el SkinnedMeshRenderer).
    static Vector3[] Skin(SkinnedBodyGeometry g, int bone, float deg, bool rigid)
    {
        var c = SkinnedBodyGeometry.BindPositions[bone];
        var res = new Vector3[g.Vertices.Count];
        for (int i = 0; i < res.Length; i++)
        {
            var v = g.Vertices[i];
            int main = g.Weight0[i] >= 0.5f ? g.Bone0[i] : g.Bone1[i];
            Vector3 P0 = IsAncestor(bone, rigid ? main : g.Bone0[i]) ? RotX(v, c, deg) : v;
            Vector3 P1 = IsAncestor(bone, g.Bone1[i]) ? RotX(v, c, deg) : v;
            float w = rigid ? 1f : g.Weight0[i];
            res[i] = P0 * w + P1 * (1 - w);
        }
        return res;
    }

    static float MaxStretch(SkinnedBodyGeometry g, Vector3[] posed, Func<Vector3, bool> near)
    {
        float worst = 1f;
        for (int t = 0; t < g.Triangles.Count; t += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = g.Triangles[t + k], b = g.Triangles[t + (k + 1) % 3];
                if (!near(g.Vertices[a])) continue;
                float rest = (g.Vertices[a] - g.Vertices[b]).magnitude, now = (posed[a] - posed[b]).magnitude;
                if (rest > 1e-4f) worst = Math.Max(worst, now / rest);
            }
        return worst;
    }

    static int[] Shells(SkinnedBodyGeometry g)
    {
        var parent = Enumerable.Range(0, g.Vertices.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        for (int t = 0; t < g.Triangles.Count; t += 3) { parent[Find(g.Triangles[t])] = Find(g.Triangles[t + 1]); parent[Find(g.Triangles[t + 1])] = Find(g.Triangles[t + 2]); }
        return Enumerable.Range(0, g.Vertices.Count).Select(Find).ToArray();
    }

    static void Main()
    {
        Console.WriteLine("Huesos: " + SkinnedBodyGeometry.BoneCount + " (" + string.Join(", ", SkinnedBodyGeometry.BoneNames) + ")");
        int min = int.MaxValue, max = 0;
        for (int hair = 0; hair < 6; hair++)
            for (int facial = 0; facial < 3; facial++)
            {
                var g = SkinnedBodyGeometry.Build(Spec(hair, facial, 0));
                min = Math.Min(min, g.TriangleCount); max = Math.Max(max, g.TriangleCount);
            }
        Console.WriteLine($"Triángulos, camiseta lisa (6 peinados x 3 vellos): {min}–{max}");
        for (int pat = 1; pat < 6; pat++) Console.WriteLine($"  diseño {pat}: {SkinnedBodyGeometry.Build(Spec(0, 0, pat)).TriangleCount}");
        Console.WriteLine($"  DT (pantalón y manga larga): {SkinnedBodyGeometry.Build(Spec(1, 1, 0, true)).TriangleCount}");
        var body = SkinnedBodyGeometry.Build(Spec(0, 1, 1));
        Console.WriteLine($"Vértices: {body.Vertices.Count}");
        // Pesos válidos: entre 0 y 1, huesos en rango, el segundo hueso es pariente del primero.
        int bad = 0, blended = 0;
        var used = new bool[20];
        for (int i = 0; i < body.Vertices.Count; i++)
        {
            int b0 = body.Bone0[i], b1 = body.Bone1[i]; float w = body.Weight0[i];
            used[b0] = true; if (w < 1) used[b1] = true;
            if (w < 0 || w > 1 || b0 < 0 || b0 >= 20 || b1 < 0 || b1 >= 20) bad++;
            if (w < 0.999f) { blended++; if (!(IsAncestor(b0, b1) || IsAncestor(b1, b0))) bad++; }
        }
        Console.WriteLine($"Pesos inválidos: {bad}; vértices con dos huesos: {blended}; huesos con vértices: {used.Count(u => u)} (la raíz no lleva malla)");
        // Costuras: en cada articulación, para los vértices de la pieza hija cerca de la articulación, cuánto crece la distancia
        // a la pieza del padre al doblar (hueco que se abre). Con pesos suaves debe quedar en milímetros; con piel rígida
        // (cada vértice solo con su hueso principal) se abre.
        int[] shell = Shells(body);
        var owner = shell.Distinct().ToDictionary(sh => sh, sh => Enumerable.Range(0, body.Vertices.Count).Where(i => shell[i] == sh)
            .GroupBy(i => body.Weight0[i] >= 0.5f ? body.Bone0[i] : body.Bone1[i]).OrderByDescending(gr => gr.Count()).First().Key);
        (string name, int bone, float deg)[] joints = { ("codo", 7, -100f), ("rodilla", 13, 100f), ("hombro", 6, -80f), ("ingle", 12, -80f), ("cabeza", 5, 30f), ("tobillo", 14, 30f) };
        foreach (var j in joints)
        {
            var c = SkinnedBodyGeometry.BindPositions[j.bone];
            int parentBone = SkinnedBodyGeometry.Parents[j.bone];
            var child = Enumerable.Range(0, body.Vertices.Count).Where(i => owner[shell[i]] == j.bone && (body.Vertices[i] - c).magnitude < 0.12f).ToArray();
            var par = Enumerable.Range(0, body.Vertices.Count).Where(i => (owner[shell[i]] == parentBone || (j.bone == 5 && (owner[shell[i]] == 3 || owner[shell[i]] == 2))) && (body.Vertices[i] - c).magnitude < 0.2f).ToArray();
            float Gap(Vector3[] posed) => child.Length == 0 || par.Length == 0 ? -1 : child.Max(i => par.Min(k => (posed[i] - posed[k]).magnitude) - par.Min(k => (body.Vertices[i] - body.Vertices[k]).magnitude));
            float gs = Gap(Skin(body, j.bone, j.deg, false)), gh = Gap(Skin(body, j.bone, j.deg, true));
            Console.WriteLine($"  {j.name,-8} {j.deg,5}°: hueco máx. con pesos {gs * 100:0.0} cm · rígido {gh * 100:0.0} cm  ({child.Length} vért. hijos, {par.Length} del padre)");
        }
        // Proporciones: altura total / alto de la cabeza.
        float top = body.Vertices.Max(v => v.y), chin = SkinnedBodyGeometry.BindPositions[5].y; // el mentón del cráneo está en la articulación
        Console.WriteLine($"Altura {top:0.00} m · cabeza {top - chin:0.000} m · {top / (top - chin):0.0} cabezas");
    }
}
