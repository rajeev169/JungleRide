using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Small mesh builder: every triangle is turned to face the direction of its vertex normals,
// so generated shapes always render from the correct side.
public class MB
{
    public readonly List<Vector3> v = new List<Vector3>();
    public readonly List<Vector3> n = new List<Vector3>();
    public readonly List<int> t = new List<int>();

    public int V(Vector3 p, Vector3 normal)
    {
        v.Add(p);
        n.Add(normal.normalized);
        return v.Count - 1;
    }

    public void Tri(int a, int b, int c)
    {
        Vector3 f = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        Vector3 want = n[a] + n[b] + n[c];
        if (Vector3.Dot(f, want) < 0f) { t.Add(a); t.Add(c); t.Add(b); }
        else { t.Add(a); t.Add(b); t.Add(c); }
    }

    public void Quad(int a, int b, int c, int d)
    {
        Tri(a, b, c);
        Tri(a, c, d);
    }

    public Mesh ToMesh(bool recalcNormals = false)
    {
        var m = new Mesh();
        if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetTriangles(t, 0);
        if (recalcNormals) m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}

public static class MeshKit
{
    static Material baseMat;
    static Mesh cube, cylinder, capsule, sphere;

    public static Mesh Cube { get { if (cube == null) cube = Prim(PrimitiveType.Cube); return cube; } }
    public static Mesh CylinderPrim { get { if (cylinder == null) cylinder = Prim(PrimitiveType.Cylinder); return cylinder; } }
    public static Mesh CapsulePrim { get { if (capsule == null) capsule = Prim(PrimitiveType.Capsule); return capsule; } }
    public static Mesh SpherePrim { get { if (sphere == null) sphere = Prim(PrimitiveType.Sphere); return sphere; } }

    static Mesh Prim(PrimitiveType type)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.position = new Vector3(0f, -500f, 0f);
        var m = go.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(go);
        return m;
    }

    // Materials are copies of a Standard-shader material kept in Resources,
    // so the shader is always included in the Android build.
    public static Material Mat(string name, Color c, float smooth = 0.1f, float metal = 0f)
    {
        if (baseMat == null)
        {
            baseMat = Resources.Load<Material>("JungleBase");
            if (baseMat == null) baseMat = new Material(Shader.Find("Standard"));
        }
        var m = new Material(baseMat);
        m.name = name;
        m.color = c;
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", metal);
        return m;
    }

    // Unit-diameter blob (icosphere) with random bumps, for leaves, bushes and rocks.
    public static Mesh Icosphere(int subdiv, float jitter, int seed)
    {
        float g = (1f + Mathf.Sqrt(5f)) / 2f;
        var pts = new List<Vector3>
        {
            new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
            new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
            new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1)
        };
        for (int i = 0; i < pts.Count; i++) pts[i] = pts[i].normalized;

        var tris = new List<int>
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        for (int s = 0; s < subdiv; s++)
        {
            var cache = new Dictionary<long, int>();
            var nt = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Mid(pts, cache, a, b);
                int bc = Mid(pts, cache, b, c);
                int ca = Mid(pts, cache, c, a);
                nt.Add(a); nt.Add(ab); nt.Add(ca);
                nt.Add(b); nt.Add(bc); nt.Add(ab);
                nt.Add(c); nt.Add(ca); nt.Add(bc);
                nt.Add(ab); nt.Add(bc); nt.Add(ca);
            }
            tris = nt;
        }

        var rng = new System.Random(seed);
        var mb = new MB();
        foreach (var p in pts)
        {
            float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
            mb.V(p * 0.5f * k, p);
        }
        for (int i = 0; i < tris.Count; i += 3) mb.Tri(tris[i], tris[i + 1], tris[i + 2]);
        return mb.ToMesh(true);
    }

    static int Mid(List<Vector3> pts, Dictionary<long, int> cache, int a, int b)
    {
        long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
        int idx;
        if (cache.TryGetValue(key, out idx)) return idx;
        pts.Add(((pts[a] + pts[b]) * 0.5f).normalized);
        idx = pts.Count - 1;
        cache[key] = idx;
        return idx;
    }

    // Upright cylinder/cone, base at y=0. Scale x/z = diameter, y = height.
    public static Mesh Cylinder(int sides, float rBottom, float rTop, float height, bool caps)
    {
        var mb = new MB();
        Vector3 up = Vector3.up * height;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            int b0 = mb.V(d0 * rBottom, d0);
            int b1 = mb.V(d1 * rBottom, d1);
            int t1 = mb.V(d1 * rTop + up, d1);
            int t0 = mb.V(d0 * rTop + up, d0);
            mb.Quad(b0, b1, t1, t0);
            if (caps)
            {
                mb.Tri(mb.V(up, Vector3.up), mb.V(d0 * rTop + up, Vector3.up), mb.V(d1 * rTop + up, Vector3.up));
                mb.Tri(mb.V(Vector3.zero, Vector3.down), mb.V(d0 * rBottom, Vector3.down), mb.V(d1 * rBottom, Vector3.down));
            }
        }
        return mb.ToMesh();
    }

    // Flat round disc facing up, unit diameter (puddles, fallen-leaf patches).
    public static Mesh Disc(int sides)
    {
        var mb = new MB();
        int c = mb.V(Vector3.zero, Vector3.up);
        var ring = new int[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            ring[i] = mb.V(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f, Vector3.up);
        }
        for (int i = 0; i < sides; i++) mb.Tri(c, ring[i], ring[(i + 1) % sides]);
        return mb.ToMesh();
    }

    // Ring around the X axis (used for tyres and rims).
    public static Mesh Torus(float R, float r, int segs, int sides)
    {
        var mb = new MB();
        var idx = new int[segs + 1, sides + 1];
        for (int i = 0; i <= segs; i++)
        {
            float u = i * Mathf.PI * 2f / segs;
            Vector3 e = new Vector3(0f, Mathf.Cos(u), Mathf.Sin(u));
            for (int j = 0; j <= sides; j++)
            {
                float w = j * Mathf.PI * 2f / sides;
                Vector3 nn = e * Mathf.Cos(w) + Vector3.right * Mathf.Sin(w);
                idx[i, j] = mb.V(e * R + nn * r, nn);
            }
        }
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < sides; j++)
                mb.Quad(idx[i, j], idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1]);
        return mb.ToMesh();
    }

    // Big hollow fallen trunk lying along Z. Bark = outside, wood = inside + cut ends.
    public static void HollowLog(float R, float r, float len, int sides, int seed, out Mesh bark, out Mesh wood)
    {
        var rng = new System.Random(seed);
        int rings = 6;
        var bump = new float[sides];
        for (int i = 0; i < sides; i++) bump[i] = 1f + ((float)rng.NextDouble() * 2f - 1f) * 0.06f;

        var ob = new MB();
        var wb = new MB();
        var o = new int[rings + 1, sides + 1];
        var inn = new int[rings + 1, sides + 1];

        for (int k = 0; k <= rings; k++)
        {
            float z = -len / 2f + len * k / rings;
            float swell = 1f + 0.04f * Mathf.Sin(k * 1.7f + seed);
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                float br = bump[i % sides];
                o[k, i] = ob.V(d * R * br * swell + Vector3.forward * z, d);
                inn[k, i] = wb.V(d * r * swell + Vector3.forward * z, -d);
            }
        }
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < sides; i++)
            {
                ob.Quad(o[k, i], o[k + 1, i], o[k + 1, i + 1], o[k, i + 1]);
                wb.Quad(inn[k, i], inn[k + 1, i], inn[k + 1, i + 1], inn[k, i + 1]);
            }

        for (int e = 0; e <= rings; e += rings)
        {
            float z = -len / 2f + len * e / rings;
            float swell = 1f + 0.04f * Mathf.Sin(e * 1.7f + seed);
            Vector3 nz = e == 0 ? Vector3.back : Vector3.forward;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                Vector3 d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                Vector3 zz = Vector3.forward * z;
                int p0 = wb.V(d0 * R * bump[i] * swell + zz, nz);
                int p1 = wb.V(d1 * R * bump[(i + 1) % sides] * swell + zz, nz);
                int p2 = wb.V(d1 * r * swell + zz, nz);
                int p3 = wb.V(d0 * r * swell + zz, nz);
                wb.Quad(p0, p1, p2, p3);
            }
        }
        bark = ob.ToMesh();
        wood = wb.ToMesh();
    }
}

// Merges thousands of plants/rocks into a few big meshes per area, so phones draw them fast.
public class Batcher
{
    readonly float cell;
    readonly Dictionary<Material, Dictionary<int, List<CombineInstance>>> map =
        new Dictionary<Material, Dictionary<int, List<CombineInstance>>>();

    public Batcher(float cellSize) { cell = cellSize; }

    public void Add(Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        int cx = Mathf.FloorToInt(pos.x / cell), cz = Mathf.FloorToInt(pos.z / cell);
        int key = cx * 1000 + cz;
        Dictionary<int, List<CombineInstance>> cells;
        if (!map.TryGetValue(mat, out cells)) { cells = new Dictionary<int, List<CombineInstance>>(); map[mat] = cells; }
        List<CombineInstance> list;
        if (!cells.TryGetValue(key, out list)) { list = new List<CombineInstance>(); cells[key] = list; }
        list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, scale) });
    }

    public void Build(Transform parent, ICollection<Material> noShadow)
    {
        foreach (var mk in map)
        {
            foreach (var ck in mk.Value)
            {
                var m = new Mesh();
                m.indexFormat = IndexFormat.UInt32;
                m.CombineMeshes(ck.Value.ToArray(), true, true);
                m.RecalculateBounds();
                var go = new GameObject("Batch_" + mk.Key.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mk.Key;
                mr.shadowCastingMode = noShadow.Contains(mk.Key) ? ShadowCastingMode.Off : ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
        }
        map.Clear();
    }
}
