using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the whole jungle when the game starts: mud road loop, trees, bushes, ferns,
// grass, rocks, puddles and big hollow trunks to ride through. Also runs the camera.
public class JungleGame : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindObjectOfType<JungleGame>() != null) return;
        new GameObject("JungleGame").AddComponent<JungleGame>();
    }

    public const int RoadSamples = 900;
    public const float RoadHalf = 2.3f;
    public static Vector3[] road;
    public static Vector3[] roadDir;

    Camera cam;
    BikeRider rider;
    System.Random rng;

    // logs (hollow trunks) so plants are not placed inside them
    readonly List<Vector3> logCenters = new List<Vector3>();
    readonly List<float> logReach = new List<float>();

    // shared meshes
    Mesh trunkMesh, branchMesh, bladeMesh, discMesh;
    Mesh[] leafMeshes, bushMeshes, rockMeshes, frondMeshes;

    // materials
    Material[] barkMats, leafMats, bushMats, litterMats;
    Material fernMat, grassMat, rockMat, mossMat, vineMat, puddleMat, logBarkMat, logWoodMat;

    void Start()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 45f;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.antiAliasing = 2;

        rng = new System.Random(20261026);
        SetupAtmosphere();
        BuildRoad();
        BuildWorld();
        SpawnRider();
    }

    float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    Quaternion RandYaw() { return Quaternion.Euler(0f, Rand(0f, 360f), 0f); }

    // ---------------------------------------------------------------- atmosphere

    void SetupAtmosphere()
    {
        Color fog = new Color(0.56f, 0.64f, 0.56f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.017f;
        RenderSettings.fogColor = fog;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.68f, 0.62f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.44f, 0.32f);
        RenderSettings.ambientGroundColor = new Color(0.17f, 0.2f, 0.12f);

        cam = Camera.main;
        if (cam == null)
        {
            var cgo = new GameObject("Main Camera");
            cgo.tag = "MainCamera";
            cam = cgo.AddComponent<Camera>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = fog;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 220f;
        cam.fieldOfView = 62f;

        Light sun = null;
        foreach (var l in FindObjectsOfType<Light>())
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null)
        {
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        sun.color = new Color(1f, 0.94f, 0.82f);
        sun.intensity = 1.15f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        RenderSettings.sun = sun;
        DynamicGI.UpdateEnvironment();
    }

    // ---------------------------------------------------------------- road

    static Vector3 RoadPoint(float t)
    {
        float r = 105f + 22f * Mathf.Sin(3f * t) + 9f * Mathf.Sin(5f * t + 1.3f);
        return new Vector3(Mathf.Cos(t) * r, 0f, Mathf.Sin(t) * r);
    }

    void BuildRoad()
    {
        int N = RoadSamples;
        road = new Vector3[N];
        roadDir = new Vector3[N];
        for (int i = 0; i < N; i++) road[i] = RoadPoint(i * Mathf.PI * 2f / N);
        for (int i = 0; i < N; i++) roadDir[i] = (road[(i + 1) % N] - road[(i - 1 + N) % N]).normalized;

        MakeStrip("RoadEdge", RoadHalf + 1.1f, 0.03f, 0f, MeshKit.Mat("MudEdge", new Color(0.26f, 0.22f, 0.13f), 0.12f));
        MakeStrip("Road", RoadHalf, 0.04f, 0f, MeshKit.Mat("Mud", new Color(0.37f, 0.26f, 0.17f), 0.3f));
        var rut = MeshKit.Mat("Ruts", new Color(0.24f, 0.16f, 0.1f), 0.5f);
        MakeStrip("RutLeft", 0.22f, 0.05f, -0.85f, rut);
        MakeStrip("RutRight", 0.22f, 0.05f, 0.85f, rut);
    }

    void MakeStrip(string name, float half, float y, float offset, Material mat)
    {
        int N = RoadSamples;
        var mb = new MB();
        var L = new int[N + 1];
        var R = new int[N + 1];
        for (int i = 0; i <= N; i++)
        {
            int k = i % N;
            Vector3 side = Vector3.Cross(Vector3.up, roadDir[k]).normalized;
            float wob = 1f + 0.08f * Mathf.Sin(k * 0.37f) + 0.05f * Mathf.Sin(k * 1.13f);
            Vector3 c = road[k] + side * offset + Vector3.up * y;
            L[i] = mb.V(c - side * half * wob, Vector3.up);
            R[i] = mb.V(c + side * half * wob, Vector3.up);
        }
        for (int i = 0; i < N; i++) mb.Quad(L[i], L[i + 1], R[i + 1], R[i]);

        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    public static float RoadDistance(Vector3 p, out int index)
    {
        index = 0;
        if (road == null) return 0f;
        float best = float.MaxValue;
        for (int i = 0; i < RoadSamples; i += 3)
        {
            float dx = road[i].x - p.x, dz = road[i].z - p.z;
            float d = dx * dx + dz * dz;
            if (d < best) { best = d; index = i; }
        }
        int c = index;
        for (int j = -3; j <= 3; j++)
        {
            int i = (c + j + RoadSamples) % RoadSamples;
            float dx = road[i].x - p.x, dz = road[i].z - p.z;
            float d = dx * dx + dz * dz;
            if (d < best) { best = d; index = i; }
        }
        return Mathf.Sqrt(best);
    }

    Vector3 RandomNearRoad(float minOff, float maxOff)
    {
        int i = rng.Next(RoadSamples);
        Vector3 side = Vector3.Cross(Vector3.up, roadDir[i]);
        float off = Mathf.Lerp(minOff, maxOff, Mathf.Pow((float)rng.NextDouble(), 1.6f));
        if (rng.NextDouble() < 0.5) off = -off;
        return road[i] + side * off + roadDir[i] * Rand(-2f, 2f);
    }

    bool InsideLog(Vector3 p)
    {
        for (int i = 0; i < logCenters.Count; i++)
        {
            Vector3 d = p - logCenters[i];
            d.y = 0f;
            if (d.magnitude < logReach[i]) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- world

    void BuildWorld()
    {
        // meshes
        trunkMesh = MeshKit.Cylinder(9, 0.5f, 0.3f, 1f, false);
        branchMesh = MeshKit.Cylinder(6, 0.5f, 0.25f, 1f, false);
        bladeMesh = MeshKit.Cylinder(3, 0.5f, 0f, 1f, false);
        discMesh = MeshKit.Disc(16);
        leafMeshes = new Mesh[4];
        for (int i = 0; i < 4; i++) leafMeshes[i] = MeshKit.Icosphere(1, 0.2f, 11 + i);
        bushMeshes = new Mesh[3];
        for (int i = 0; i < 3; i++) bushMeshes[i] = MeshKit.Icosphere(1, 0.14f, 31 + i);
        rockMeshes = new Mesh[3];
        for (int i = 0; i < 3; i++) rockMeshes[i] = MeshKit.Icosphere(1, 0.28f, 51 + i);
        frondMeshes = new Mesh[2];
        for (int i = 0; i < 2; i++) frondMeshes[i] = MeshKit.Icosphere(0, 0.1f, 71 + i);

        // materials
        barkMats = new[]
        {
            MeshKit.Mat("Bark1", new Color(0.29f, 0.22f, 0.16f), 0.08f),
            MeshKit.Mat("Bark2", new Color(0.36f, 0.31f, 0.24f), 0.08f),
            MeshKit.Mat("Bark3", new Color(0.22f, 0.19f, 0.15f), 0.08f)
        };
        leafMats = new[]
        {
            MeshKit.Mat("Leaf1", new Color(0.13f, 0.32f, 0.09f), 0.18f),
            MeshKit.Mat("Leaf2", new Color(0.2f, 0.4f, 0.12f), 0.18f),
            MeshKit.Mat("Leaf3", new Color(0.09f, 0.25f, 0.1f), 0.18f)
        };
        bushMats = new[]
        {
            MeshKit.Mat("Bush1", new Color(0.16f, 0.35f, 0.1f), 0.15f),
            MeshKit.Mat("Bush2", new Color(0.24f, 0.42f, 0.13f), 0.15f)
        };
        litterMats = new[]
        {
            MeshKit.Mat("Litter1", new Color(0.26f, 0.23f, 0.12f), 0.05f),
            MeshKit.Mat("Litter2", new Color(0.17f, 0.24f, 0.1f), 0.05f)
        };
        fernMat = MeshKit.Mat("Fern", new Color(0.22f, 0.46f, 0.14f), 0.2f);
        grassMat = MeshKit.Mat("Grass", new Color(0.3f, 0.48f, 0.15f), 0.1f);
        rockMat = MeshKit.Mat("Rock", new Color(0.4f, 0.4f, 0.37f), 0.2f);
        mossMat = MeshKit.Mat("Moss", new Color(0.24f, 0.4f, 0.12f), 0.1f);
        vineMat = MeshKit.Mat("Vine", new Color(0.2f, 0.27f, 0.12f), 0.1f);
        puddleMat = MeshKit.Mat("Puddle", new Color(0.12f, 0.1f, 0.08f), 0.93f);
        logBarkMat = MeshKit.Mat("LogBark", new Color(0.3f, 0.25f, 0.18f), 0.06f);
        logWoodMat = MeshKit.Mat("LogWood", new Color(0.45f, 0.32f, 0.2f), 0.1f);

        // ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(60f, 1f, 60f);
        var gr = ground.GetComponent<MeshRenderer>();
        gr.sharedMaterial = MeshKit.Mat("Ground", new Color(0.18f, 0.26f, 0.11f), 0.05f);
        gr.shadowCastingMode = ShadowCastingMode.Off;

        var root = new GameObject("Jungle").transform;
        var colRoot = new GameObject("TreeColliders").transform;
        var batch = new Batcher(60f);

        // hollow trunks over the road first, so nothing grows inside them
        for (int k = 0; k < 5; k++) BuildHollowLog(batch, (RoadSamples * k / 5 + 45) % RoadSamples, k);

        // giant trees close to the road
        int placed = 0, tries = 0;
        while (placed < 45 && tries < 2000)
        {
            tries++;
            Vector3 p = RandomNearRoad(10f, 30f);
            int idx;
            if (RoadDistance(p, out idx) < 9.5f || InsideLog(p)) continue;
            AddTree(batch, colRoot, p, true, false);
            placed++;
        }
        // normal trees, dense near the road
        placed = 0; tries = 0;
        while (placed < 560 && tries < 8000)
        {
            tries++;
            Vector3 p = RandomNearRoad(6f, 55f);
            int idx;
            if (RoadDistance(p, out idx) < 6.5f || InsideLog(p)) continue;
            AddTree(batch, colRoot, p, false, false);
            placed++;
        }
        // far trees seen through the fog
        placed = 0; tries = 0;
        while (placed < 220 && tries < 3000)
        {
            tries++;
            Vector3 p = RandomNearRoad(55f, 110f);
            int idx;
            if (RoadDistance(p, out idx) < 40f) continue;
            AddTree(batch, colRoot, p, false, true);
            placed++;
        }

        // bushes
        for (int i = 0; i < 1100; i++)
        {
            Vector3 p = RandomNearRoad(3.6f, 40f);
            int idx;
            if (RoadDistance(p, out idx) < 3.6f || InsideLog(p)) continue;
            float s = Rand(1.2f, 3f);
            p.y = Rand(0.05f, 0.3f);
            batch.Add(bushMeshes[rng.Next(bushMeshes.Length)], bushMats[rng.Next(bushMats.Length)], p, RandYaw(),
                new Vector3(s, s * Rand(0.5f, 0.8f), s * Rand(0.8f, 1.2f)));
        }

        // ferns (fronds spreading out from a centre)
        for (int i = 0; i < 420; i++)
        {
            Vector3 p = RandomNearRoad(3.2f, 30f);
            int idx;
            if (RoadDistance(p, out idx) < 3.2f || InsideLog(p)) continue;
            AddFern(batch, p, Rand(0.7f, 1.4f));
        }

        // grass tufts, many along the road edges
        var noShadow = new HashSet<Material> { grassMat, puddleMat, litterMats[0], litterMats[1] };
        for (int i = 0; i < 950; i++)
        {
            Vector3 p = RandomNearRoad(2.9f, 22f);
            int idx;
            if (RoadDistance(p, out idx) < 2.9f || InsideLog(p)) continue;
            int blades = rng.Next(4, 8);
            for (int b = 0; b < blades; b++)
            {
                Quaternion q = Quaternion.Euler(Rand(-22f, 22f), Rand(0f, 360f), Rand(-22f, 22f));
                Vector3 off = new Vector3(Rand(-0.15f, 0.15f), 0f, Rand(-0.15f, 0.15f));
                batch.Add(bladeMesh, grassMat, p + off, q, new Vector3(0.07f, Rand(0.45f, 1.0f), 0.07f));
            }
        }

        // rocks
        for (int i = 0; i < 160; i++)
        {
            Vector3 p = RandomNearRoad(3.1f, 25f);
            int idx;
            if (RoadDistance(p, out idx) < 3.1f || InsideLog(p)) continue;
            float s = Rand(0.25f, 1.4f);
            p.y = s * 0.12f;
            batch.Add(rockMeshes[rng.Next(rockMeshes.Length)], rockMat, p, Random3(),
                new Vector3(s, s * Rand(0.5f, 0.8f), s * Rand(0.8f, 1.2f)));
        }

        // fallen leaves patches on the jungle floor
        for (int i = 0; i < 260; i++)
        {
            Vector3 p = RandomNearRoad(4.5f, 40f);
            int idx;
            if (RoadDistance(p, out idx) < 4.5f) continue;
            p.y = 0.012f;
            float s = Rand(3f, 7f);
            batch.Add(discMesh, litterMats[rng.Next(2)], p, RandYaw(), new Vector3(s, 1f, s * Rand(0.6f, 1f)));
        }

        // small dead branches lying beside the road
        for (int i = 0; i < 70; i++)
        {
            Vector3 p = RandomNearRoad(3.4f, 14f);
            int idx;
            if (RoadDistance(p, out idx) < 3.4f || InsideLog(p)) continue;
            float len = Rand(1.5f, 4f), d = Rand(0.1f, 0.25f);
            Quaternion q = RandYaw() * Quaternion.Euler(90f, 0f, 0f);
            batch.Add(branchMesh, barkMats[rng.Next(barkMats.Length)], p + Vector3.up * d * 0.4f, q, new Vector3(d, len, d));
        }

        // wet mud puddles on the road
        for (int i = 0; i < 45; i++)
        {
            int k = rng.Next(RoadSamples);
            Vector3 side = Vector3.Cross(Vector3.up, roadDir[k]);
            Vector3 p = road[k] + side * Rand(-1.2f, 1.2f) + Vector3.up * 0.055f;
            if (InsideLog(p)) continue;
            Quaternion q = Quaternion.LookRotation(roadDir[k]) * Quaternion.Euler(0f, Rand(-25f, 25f), 0f);
            batch.Add(discMesh, puddleMat, p, q, new Vector3(Rand(0.9f, 2f), 1f, Rand(1.2f, 3f)));
        }

        batch.Build(root, noShadow);
    }

    Quaternion Random3() { return Quaternion.Euler(Rand(0f, 360f), Rand(0f, 360f), Rand(0f, 360f)); }

    void AddSegment(Batcher b, Mesh mesh, Material mat, Vector3 from, Vector3 to, float diameter)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return;
        b.Add(mesh, mat, from, Quaternion.FromToRotation(Vector3.up, d / len), new Vector3(diameter, len, diameter));
    }

    void AddTree(Batcher b, Transform colRoot, Vector3 p, bool giant, bool far)
    {
        float h = giant ? Rand(24f, 32f) : Rand(9f, 17f);
        float d = giant ? Rand(2.4f, 3.2f) : Rand(0.45f, 0.95f);
        Material bark = barkMats[rng.Next(barkMats.Length)];
        Quaternion tilt = Quaternion.Euler(Rand(-3f, 3f), 0f, Rand(-3f, 3f)) * RandYaw();
        Vector3 basePos = p - Vector3.up * 0.2f;
        b.Add(trunkMesh, bark, basePos, tilt, new Vector3(d, h, d));
        Vector3 axis = tilt * Vector3.up;

        // roots / buttresses at the base
        int roots = giant ? rng.Next(5, 8) : (far ? 0 : rng.Next(2, 4));
        for (int i = 0; i < roots; i++)
        {
            float a = (i + Rand(-0.3f, 0.3f)) * Mathf.PI * 2f / roots;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 from = p + dir * d * 0.25f + Vector3.up * (giant ? Rand(2f, 4f) : Rand(0.4f, 0.9f));
            Vector3 to = p + dir * d * (giant ? Rand(1.3f, 1.9f) : Rand(1f, 1.5f)) - Vector3.up * 0.25f;
            AddSegment(b, branchMesh, bark, from, to, d * (giant ? 0.3f : 0.35f));
        }

        // branches and leaf clusters
        Vector3 top = basePos + axis * h;
        int clusters = giant ? rng.Next(7, 10) : rng.Next(3, 6);
        float spread = giant ? 7.5f : 2f + d * 1.6f;
        for (int c = 0; c < clusters; c++)
        {
            Vector3 off = new Vector3(Rand(-1f, 1f), Rand(-0.45f, 0.5f), Rand(-1f, 1f)) * spread;
            Vector3 cp = top + off;
            float s = giant ? Rand(6f, 10f) : Rand(3f, 5.5f);
            b.Add(leafMeshes[rng.Next(leafMeshes.Length)], leafMats[rng.Next(leafMats.Length)], cp, RandYaw(),
                new Vector3(s, s * Rand(0.6f, 0.85f), s * Rand(0.85f, 1.15f)));
            Vector3 start = basePos + axis * h * Rand(0.62f, 0.9f);
            if (!far) AddSegment(b, branchMesh, bark, start, cp, d * Rand(0.25f, 0.4f));

            // hanging vines from giant trees
            if (giant && rng.NextDouble() < 0.7)
            {
                Vector3 vtop = cp - Vector3.up * s * 0.25f + new Vector3(Rand(-1.5f, 1.5f), 0f, Rand(-1.5f, 1.5f));
                Vector3 vbot = new Vector3(vtop.x + Rand(-0.4f, 0.4f), Rand(1.2f, 4f), vtop.z + Rand(-0.4f, 0.4f));
                AddSegment(b, branchMesh, vineMat, vbot, vtop, Rand(0.05f, 0.1f));
            }
        }

        // moss on giant trunks
        if (giant)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f);
                Vector3 mp = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d * 0.48f + Vector3.up * Rand(0.5f, 5f);
                b.Add(bushMeshes[0], mossMat, mp, RandYaw(), new Vector3(d * 0.35f, Rand(0.6f, 1.5f), d * 0.35f));
            }
        }

        // trunk collider so the bike cannot ride through the tree
        if (far) return;
        var go = new GameObject("TreeCol");
        go.transform.SetParent(colRoot, false);
        go.transform.position = p + Vector3.up * 2.5f;
        var cc = go.AddComponent<CapsuleCollider>();
        cc.radius = d * 0.5f;
        cc.height = 5f + d;
    }

    void AddFern(Batcher b, Vector3 p, float size)
    {
        int fronds = rng.Next(5, 8);
        float yaw0 = Rand(0f, 360f);
        for (int i = 0; i < fronds; i++)
        {
            float yaw = yaw0 + i * 360f / fronds + Rand(-12f, 12f);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-Rand(18f, 35f), 0f, 0f);
            Vector3 center = p + q * (Vector3.forward * 0.55f * size) + Vector3.up * 0.15f;
            b.Add(frondMeshes[rng.Next(frondMeshes.Length)], fernMat, center, q, new Vector3(0.32f * size, 0.06f, 1.25f * size));
        }
    }

    void BuildHollowLog(Batcher b, int index, int seed)
    {
        float R = Rand(3.3f, 3.8f);
        float r = R - 0.5f;
        float len = Rand(9f, 12f);
        Vector3 dir = roadDir[index];
        Vector3 center = road[index] + Vector3.up * (R * 0.6f);
        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, Rand(0f, 360f));

        Mesh bark, wood;
        MeshKit.HollowLog(R, r, len, 22, 100 + seed, out bark, out wood);

        var go = new GameObject("HollowTrunk_" + seed);
        go.transform.SetPositionAndRotation(center, rot);
        go.AddComponent<MeshFilter>().sharedMesh = bark;
        go.AddComponent<MeshRenderer>().sharedMaterial = logBarkMat;
        go.AddComponent<MeshCollider>().sharedMesh = bark;

        var inner = new GameObject("Inside");
        inner.transform.SetParent(go.transform, false);
        inner.AddComponent<MeshFilter>().sharedMesh = wood;
        inner.AddComponent<MeshRenderer>().sharedMaterial = logWoodMat;
        inner.AddComponent<MeshCollider>().sharedMesh = wood;

        // moss and ferns growing on top of the trunk
        Quaternion flat = Quaternion.LookRotation(dir);
        for (int i = 0; i < 9; i++)
        {
            float z = Rand(-len * 0.45f, len * 0.45f);
            float a = Rand(-50f, 50f) * Mathf.Deg2Rad;
            Vector3 local = new Vector3(Mathf.Sin(a) * R, Mathf.Cos(a) * R, z);
            Vector3 wp = road[index] + Vector3.up * (R * 0.6f) + flat * local;
            float s = Rand(1f, 2.4f);
            b.Add(bushMeshes[rng.Next(bushMeshes.Length)], mossMat, wp, RandYaw(), new Vector3(s, s * 0.35f, s));
        }
        for (int i = 0; i < 3; i++)
        {
            Vector3 local = new Vector3(Rand(-0.8f, 0.8f), R * 0.98f, Rand(-len * 0.4f, len * 0.4f));
            AddFern(b, road[index] + Vector3.up * (R * 0.6f) + flat * local, Rand(0.6f, 0.9f));
        }

        logCenters.Add(road[index]);
        logReach.Add(len * 0.5f + R + 0.5f);
    }

    // ---------------------------------------------------------------- rider + camera

    void SpawnRider()
    {
        var go = new GameObject("Rider");
        go.transform.position = road[0] + Vector3.up * 0.05f;
        go.transform.rotation = Quaternion.LookRotation(roadDir[0]);
        rider = go.AddComponent<BikeRider>();

        Vector3 f = roadDir[0];
        cam.transform.position = go.transform.position - f * 5.2f + Vector3.up * 2.4f;
        cam.transform.rotation = Quaternion.LookRotation(go.transform.position + Vector3.up * 1.2f + f * 2.5f - cam.transform.position);
    }

    void LateUpdate()
    {
        if (rider == null) return;
        Transform t = rider.transform;
        Vector3 fwd = t.forward;
        Vector3 desired = t.position - fwd * 5.2f + Vector3.up * 2.4f;
        cam.transform.position = Vector3.Lerp(cam.transform.position, desired, 1f - Mathf.Exp(-5f * Time.deltaTime));
        Vector3 look = t.position + Vector3.up * 1.2f + fwd * 2.5f;
        Quaternion want = Quaternion.LookRotation(look - cam.transform.position);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, 1f - Mathf.Exp(-8f * Time.deltaTime));
    }
}
