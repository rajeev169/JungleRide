using UnityEngine;

// The bicycle and its rider: riding physics, touch controls, pedaling animation
// (legs follow the pedals, hands hold the handlebar, foot goes down when stopped).
[RequireComponent(typeof(CharacterController))]
public class BikeRider : MonoBehaviour
{
    const float WheelR = 0.345f;
    static readonly Vector3 HipC = new Vector3(0f, 1.04f, -0.22f);
    static readonly Vector3 ShoulderC = new Vector3(0f, 1.44f, 0.05f);

    public float maxSpeed = 9.5f;     // about 34 km/h on the mud road
    public float accel = 2.4f;
    public float brakeDecel = 7f;
    public float turnRate = 70f;

    CharacterController cc;
    float speed, yaw, vy, steerInput, steer, lean, footDown, swayBlend;
    float wheelAngle, crankAngle, distance;
    bool pedal, brake, left, right;

    Transform leanPivot, frontAssembly, frontWheel, rearWheel, crank, pedalPivotL, pedalPivotR;
    Transform handL, handR, torso, neck, headRoot, shoeL, shoeR;
    Limb legL, legR, armL, armR;

    Mesh tyreMesh, rimMesh;
    Material skin, jersey, shorts, shoeMat, glove, helmetMat, frameMat, rubber, metal, dark, eyeMat;

    Rect leftBtn, rightBtn, pedalBtn, brakeBtn;
    GUIStyle btnStyle, labelStyle, shadowStyle;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.95f, 0f);
        cc.slopeLimit = 45f;
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.05f;
        yaw = transform.eulerAngles.y;

        BuildMaterials();
        BuildBike();
        BuildRider();
    }

    // ---------------------------------------------------------------- build helpers

    static Transform Part(Transform parent, Mesh mesh, Material mat, Vector3 lpos, Quaternion lrot, Vector3 scale, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lpos;
        go.transform.localRotation = lrot;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    static Transform Rod(Transform parent, Vector3 a, Vector3 b, float diameter, Material mat)
    {
        Vector3 d = b - a;
        float len = Mathf.Max(d.magnitude, 0.001f);
        return Part(parent, MeshKit.CylinderPrim, mat, (a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d / len),
            new Vector3(diameter, len * 0.5f, diameter), "Rod");
    }

    static Transform Pivot(Transform parent, Vector3 lpos, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lpos;
        return go.transform;
    }

    void BuildMaterials()
    {
        skin = MeshKit.Mat("Skin", new Color(0.55f, 0.38f, 0.27f), 0.35f);
        jersey = MeshKit.Mat("Jersey", new Color(0.1f, 0.36f, 0.62f), 0.3f);
        shorts = MeshKit.Mat("Shorts", new Color(0.07f, 0.07f, 0.09f), 0.2f);
        shoeMat = MeshKit.Mat("Shoes", new Color(0.9f, 0.9f, 0.88f), 0.3f);
        glove = MeshKit.Mat("Gloves", new Color(0.06f, 0.06f, 0.06f), 0.2f);
        helmetMat = MeshKit.Mat("Helmet", new Color(0.82f, 0.12f, 0.1f), 0.75f);
        frameMat = MeshKit.Mat("Frame", new Color(0.88f, 0.45f, 0.06f), 0.7f, 0.15f);
        rubber = MeshKit.Mat("Rubber", new Color(0.05f, 0.05f, 0.05f), 0.15f);
        metal = MeshKit.Mat("Metal", new Color(0.72f, 0.72f, 0.74f), 0.7f, 0.25f);
        dark = MeshKit.Mat("DarkParts", new Color(0.12f, 0.12f, 0.13f), 0.4f);
        eyeMat = MeshKit.Mat("Eyes", new Color(0.03f, 0.03f, 0.03f), 0.8f);
        tyreMesh = MeshKit.Torus(0.31f, 0.036f, 28, 8);
        rimMesh = MeshKit.Torus(0.285f, 0.013f, 28, 6);
    }

    Transform BuildWheel(Transform parent, Vector3 lpos)
    {
        var w = Pivot(parent, lpos, "Wheel");
        Part(w, tyreMesh, rubber, Vector3.zero, Quaternion.identity, Vector3.one, "Tyre");
        Part(w, rimMesh, metal, Vector3.zero, Quaternion.identity, Vector3.one, "Rim");
        for (int i = 0; i < 16; i++)
        {
            Quaternion q = Quaternion.Euler(i * 22.5f, 0f, 0f);
            float side = (i % 2 == 0) ? 0.022f : -0.022f;
            Rod(w, new Vector3(side, 0f, 0f), q * (Vector3.up * 0.28f), 0.006f, metal);
        }
        Part(w, MeshKit.CylinderPrim, dark, Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new Vector3(0.05f, 0.05f, 0.05f), "Hub");
        return w;
    }

    void BuildBike()
    {
        leanPivot = Pivot(transform, Vector3.zero, "LeanPivot");

        Vector3 RA = new Vector3(0f, WheelR, -0.52f);
        Vector3 BB = new Vector3(0f, 0.30f, -0.04f);
        Vector3 SC = new Vector3(0f, 0.86f, -0.18f);
        Vector3 HT = new Vector3(0f, 0.93f, 0.34f);
        Vector3 HB = new Vector3(0f, 0.76f, 0.38f);

        rearWheel = BuildWheel(leanPivot, RA);

        // frame tubes
        Rod(leanPivot, BB, SC, 0.045f, frameMat);
        Rod(leanPivot, SC, HT, 0.042f, frameMat);
        Rod(leanPivot, BB, HB, 0.05f, frameMat);
        Rod(leanPivot, HB, HT, 0.055f, frameMat);
        foreach (float x in new[] { -0.06f, 0.06f })
        {
            Rod(leanPivot, BB + Vector3.right * x * 0.5f, RA + Vector3.right * x, 0.025f, frameMat);
            Rod(leanPivot, SC + Vector3.right * x * 0.3f, RA + Vector3.right * x, 0.022f, frameMat);
        }

        // seat post and saddle
        Vector3 seatTop = new Vector3(0f, 0.98f, -0.225f);
        Rod(leanPivot, SC, seatTop, 0.03f, metal);
        Part(leanPivot, MeshKit.CapsulePrim, dark, seatTop + new Vector3(0f, 0.025f, -0.01f), Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.13f, 0.14f, 0.06f), "Saddle");

        // crank, chainring and pedals
        crank = Pivot(leanPivot, BB, "Crank");
        Part(crank, MeshKit.CylinderPrim, metal, new Vector3(0.065f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f),
            new Vector3(0.2f, 0.004f, 0.2f), "Chainring");
        Rod(crank, new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0f), 0.03f, dark);
        Rod(crank, new Vector3(0.09f, 0f, 0f), new Vector3(0.09f, -0.17f, 0f), 0.022f, dark);
        Rod(crank, new Vector3(-0.09f, 0f, 0f), new Vector3(-0.09f, 0.17f, 0f), 0.022f, dark);
        pedalPivotR = Pivot(crank, new Vector3(0.14f, -0.17f, 0f), "PedalR");
        Part(pedalPivotR, MeshKit.Cube, dark, Vector3.zero, Quaternion.identity, new Vector3(0.09f, 0.02f, 0.09f), "Pedal");
        pedalPivotL = Pivot(crank, new Vector3(-0.14f, 0.17f, 0f), "PedalL");
        Part(pedalPivotL, MeshKit.Cube, dark, Vector3.zero, Quaternion.identity, new Vector3(0.09f, 0.02f, 0.09f), "Pedal");

        // chain and rear cog
        Rod(leanPivot, BB + new Vector3(0.065f, 0.1f, 0f), RA + new Vector3(0.065f, 0.04f, 0f), 0.012f, dark);
        Rod(leanPivot, BB + new Vector3(0.065f, -0.1f, 0f), RA + new Vector3(0.065f, -0.04f, 0f), 0.012f, dark);
        Part(leanPivot, MeshKit.CylinderPrim, metal, RA + new Vector3(0.065f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f),
            new Vector3(0.08f, 0.004f, 0.08f), "Cog");

        // steering: fork, front wheel, stem, handlebar, grips
        frontAssembly = Pivot(leanPivot, new Vector3(0f, 0f, 0.43f), "Steering");
        Vector3 FA = new Vector3(0f, WheelR, 0.09f);
        frontWheel = BuildWheel(frontAssembly, FA);
        Vector3 hbL = HB - new Vector3(0f, 0f, 0.43f);
        foreach (float x in new[] { -0.055f, 0.055f })
            Rod(frontAssembly, hbL + Vector3.right * x * 0.6f, FA + Vector3.right * x, 0.028f, metal);
        Vector3 stemA = new Vector3(0f, 0.95f, -0.09f);
        Vector3 stemB = new Vector3(0f, 1.0f, -0.02f);
        Rod(frontAssembly, hbL, stemA, 0.03f, metal);
        Rod(frontAssembly, stemA, stemB, 0.035f, dark);
        Rod(frontAssembly, stemB + Vector3.left * 0.31f, stemB + Vector3.right * 0.31f, 0.026f, dark);
        Part(frontAssembly, MeshKit.CylinderPrim, rubber, stemB + Vector3.left * 0.27f, Quaternion.Euler(0f, 0f, 90f),
            new Vector3(0.035f, 0.055f, 0.035f), "GripL");
        Part(frontAssembly, MeshKit.CylinderPrim, rubber, stemB + Vector3.right * 0.27f, Quaternion.Euler(0f, 0f, 90f),
            new Vector3(0.035f, 0.055f, 0.035f), "GripR");
        handL = Part(frontAssembly, MeshKit.SpherePrim, glove, stemB + new Vector3(-0.27f, 0.015f, 0f), Quaternion.identity,
            new Vector3(0.09f, 0.075f, 0.11f), "HandL");
        handR = Part(frontAssembly, MeshKit.SpherePrim, glove, stemB + new Vector3(0.27f, 0.015f, 0f), Quaternion.identity,
            new Vector3(0.09f, 0.075f, 0.11f), "HandR");
    }

    void BuildRider()
    {
        // hips and body
        Part(leanPivot, MeshKit.CapsulePrim, shorts, HipC, Quaternion.Euler(0f, 0f, 90f), new Vector3(0.21f, 0.16f, 0.2f), "Pelvis");
        torso = Part(leanPivot, MeshKit.CapsulePrim, jersey, Vector3.zero, Quaternion.identity, Vector3.one, "Torso");
        neck = Part(leanPivot, MeshKit.CylinderPrim, skin, Vector3.zero, Quaternion.identity, Vector3.one, "Neck");

        // head with face and helmet
        headRoot = Pivot(leanPivot, Vector3.zero, "Head");
        Part(headRoot, MeshKit.SpherePrim, skin, Vector3.zero, Quaternion.identity, new Vector3(0.19f, 0.22f, 0.21f), "Skull");
        Part(headRoot, MeshKit.SpherePrim, helmetMat, new Vector3(0f, 0.055f, -0.015f), Quaternion.Euler(-8f, 0f, 0f),
            new Vector3(0.25f, 0.19f, 0.28f), "Helmet");
        Part(headRoot, MeshKit.Cube, dark, new Vector3(0f, 0.075f, 0.125f), Quaternion.Euler(-15f, 0f, 0f),
            new Vector3(0.2f, 0.015f, 0.06f), "Visor");
        Part(headRoot, MeshKit.SpherePrim, eyeMat, new Vector3(-0.038f, 0.012f, 0.094f), Quaternion.identity, Vector3.one * 0.026f, "EyeL");
        Part(headRoot, MeshKit.SpherePrim, eyeMat, new Vector3(0.038f, 0.012f, 0.094f), Quaternion.identity, Vector3.one * 0.026f, "EyeR");
        Part(headRoot, MeshKit.SpherePrim, skin, new Vector3(0f, -0.018f, 0.105f), Quaternion.identity, new Vector3(0.03f, 0.045f, 0.04f), "Nose");
        Part(headRoot, MeshKit.Cube, dark, new Vector3(0f, -0.058f, 0.093f), Quaternion.identity, new Vector3(0.04f, 0.006f, 0.01f), "Mouth");
        Part(headRoot, MeshKit.SpherePrim, skin, new Vector3(-0.098f, 0f, -0.005f), Quaternion.identity, new Vector3(0.03f, 0.055f, 0.04f), "EarL");
        Part(headRoot, MeshKit.SpherePrim, skin, new Vector3(0.098f, 0f, -0.005f), Quaternion.identity, new Vector3(0.03f, 0.055f, 0.04f), "EarR");

        // legs (shorts + bare skin) and arms (sleeves + skin)
        legL = new Limb(transform, shorts, skin, skin, 0.48f, 0.47f, 0.075f, 0.055f);
        legR = new Limb(transform, shorts, skin, skin, 0.48f, 0.47f, 0.075f, 0.055f);
        armL = new Limb(transform, jersey, skin, skin, 0.32f, 0.3f, 0.05f, 0.04f);
        armR = new Limb(transform, jersey, skin, skin, 0.32f, 0.3f, 0.05f, 0.04f);

        shoeL = Part(transform, MeshKit.Cube, shoeMat, Vector3.zero, Quaternion.identity, new Vector3(0.095f, 0.075f, 0.25f), "ShoeL");
        shoeR = Part(transform, MeshKit.Cube, shoeMat, Vector3.zero, Quaternion.identity, new Vector3(0.095f, 0.075f, 0.25f), "ShoeR");
    }

    // ---------------------------------------------------------------- riding

    void Update()
    {
        float dt = Time.deltaTime;
        ReadInput();

        int idx;
        float rd = JungleGame.RoadDistance(transform.position, out idx);
        bool onRoad = rd < JungleGame.RoadHalf + 1.2f;
        float top = onRoad ? maxSpeed : maxSpeed * 0.55f;

        if (brake)
        {
            if (speed > 0.15f) speed = Mathf.MoveTowards(speed, 0f, brakeDecel * dt);
            else speed = Mathf.MoveTowards(speed, -1.8f, 1.5f * dt);   // hold brake to roll back slowly
        }
        else if (pedal)
        {
            if (speed < 0f) speed = Mathf.MoveTowards(speed, 0f, brakeDecel * dt);
            else if (speed < top) speed = Mathf.Min(top, speed + accel * (1.15f - speed / top) * dt);
            else speed = Mathf.MoveTowards(speed, top, 2f * dt);
        }
        else
        {
            float drag = (onRoad ? 0.35f : 1.4f) + Mathf.Abs(speed) * 0.04f;
            speed = Mathf.MoveTowards(speed, 0f, drag * dt);
        }

        steer = Mathf.MoveTowards(steer, steerInput, 3.2f * dt);
        float turnFactor = Mathf.Clamp01(Mathf.Abs(speed) / 2.5f);
        yaw += steer * turnRate * turnFactor * Mathf.Sign(speed) * dt;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cc.isGrounded) vy = -1f;
        else vy -= 20f * dt;
        CollisionFlags flags = cc.Move(transform.forward * speed * dt + Vector3.up * vy * dt);
        if ((flags & CollisionFlags.Sides) != 0 && Mathf.Abs(speed) > 0.8f) speed *= 0.35f;
        distance += Mathf.Abs(speed) * dt;

        if (transform.position.y < -5f) ResetToRoad();

        // wheels, cranks, steering and lean
        wheelAngle = (wheelAngle + speed / WheelR * Mathf.Rad2Deg * dt) % 360f;
        rearWheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
        frontWheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
        if (pedal && speed > 0.2f) crankAngle = (crankAngle + speed / WheelR / 2.3f * Mathf.Rad2Deg * dt) % 360f;
        crank.localRotation = Quaternion.Euler(crankAngle, 0f, 0f);
        pedalPivotL.localRotation = Quaternion.Euler(-crankAngle, 0f, 0f);
        pedalPivotR.localRotation = Quaternion.Euler(-crankAngle, 0f, 0f);
        frontAssembly.localRotation = Quaternion.Euler(0f, steer * 22f * (1f - 0.6f * Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed)), 0f);

        bool stopped = Mathf.Abs(speed) < 0.35f && !pedal;
        footDown = Mathf.MoveTowards(footDown, stopped ? 1f : 0f, 3f * dt);
        swayBlend = Mathf.MoveTowards(swayBlend, (pedal && speed > 0.2f) ? 1f : 0f, 3f * dt);
        float targetLean = -steer * Mathf.Clamp01(Mathf.Abs(speed) / 5f) * 20f + footDown * 7f;
        lean = Mathf.Lerp(lean, targetLean, 1f - Mathf.Exp(-6f * dt));
        leanPivot.localRotation = Quaternion.Euler(0f, 0f, lean);
    }

    void ResetToRoad()
    {
        int i;
        JungleGame.RoadDistance(transform.position, out i);
        cc.enabled = false;
        transform.position = JungleGame.road[i] + Vector3.up * 0.1f;
        yaw = Quaternion.LookRotation(JungleGame.roadDir[i]).eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cc.enabled = true;
        speed = 0f;
        vy = 0f;
    }

    // body pose is solved after movement so limbs stick to pedals and handlebar
    void LateUpdate()
    {
        if (torso == null) return;
        float c = crankAngle * Mathf.Deg2Rad;
        Vector3 sway = new Vector3(Mathf.Sin(c) * 0.018f, Mathf.Abs(Mathf.Cos(c)) * 0.008f, 0f) * swayBlend;
        Vector3 hip = HipC;
        Vector3 sh = ShoulderC + sway;

        Vector3 body = sh - hip;
        torso.localPosition = (hip + sh) * 0.5f;
        torso.localRotation = Quaternion.FromToRotation(Vector3.up, body.normalized);
        torso.localScale = new Vector3(0.36f, body.magnitude * 0.5f + 0.08f, 0.24f);

        Vector3 headC = sh + new Vector3(0f, 0.2f, 0.07f);
        Vector3 n0 = sh + new Vector3(0f, 0.04f, 0f), n1 = headC - new Vector3(0f, 0.08f, 0f);
        Vector3 nd = n1 - n0;
        neck.localPosition = (n0 + n1) * 0.5f;
        neck.localRotation = Quaternion.FromToRotation(Vector3.up, nd.normalized);
        neck.localScale = new Vector3(0.09f, nd.magnitude * 0.5f, 0.09f);
        headRoot.localPosition = headC;
        headRoot.localRotation = Quaternion.Euler(-6f, steer * 18f, -lean * 0.5f);

        // legs to pedals (left foot goes to the ground when stopped)
        Vector3 up = leanPivot.up, fwd = leanPivot.forward;
        Vector3 hipL = leanPivot.TransformPoint(hip + new Vector3(-0.1f, 0f, 0f));
        Vector3 hipR = leanPivot.TransformPoint(hip + new Vector3(0.1f, 0f, 0f));
        Vector3 ankleL = pedalPivotL.position + up * 0.075f - fwd * 0.04f;
        Vector3 ankleR = pedalPivotR.position + up * 0.075f - fwd * 0.04f;
        Vector3 groundL = leanPivot.TransformPoint(new Vector3(-0.33f, 0.09f, -0.08f));
        ankleL = Vector3.Lerp(ankleL, groundL, footDown);
        legL.Solve(hipL, ankleL, leanPivot.TransformDirection(new Vector3(-0.1f, 0.25f, 1f)));
        legR.Solve(hipR, ankleR, leanPivot.TransformDirection(new Vector3(0.1f, 0.25f, 1f)));
        Quaternion shoeRot = leanPivot.rotation;
        shoeL.SetPositionAndRotation(legL.End + leanPivot.TransformDirection(new Vector3(0f, -0.045f, 0.06f)), shoeRot);
        shoeR.SetPositionAndRotation(legR.End + leanPivot.TransformDirection(new Vector3(0f, -0.045f, 0.06f)), shoeRot);

        // arms to the handlebar grips
        Vector3 shL = leanPivot.TransformPoint(sh + new Vector3(-0.19f, -0.03f, 0f));
        Vector3 shR = leanPivot.TransformPoint(sh + new Vector3(0.19f, -0.03f, 0f));
        armL.Solve(shL, handL.position - fwd * 0.03f, leanPivot.TransformDirection(new Vector3(-0.6f, -0.5f, -0.3f)));
        armR.Solve(shR, handR.position - fwd * 0.03f, leanPivot.TransformDirection(new Vector3(0.6f, -0.5f, -0.3f)));
    }

    // Two-bone limb (thigh+shin or upper arm+forearm) solved with simple IK.
    class Limb
    {
        readonly Transform upper, lower, joint;
        readonly float l1, l2, r1, r2;
        public Vector3 End { get; private set; }

        public Limb(Transform root, Material m1, Material m2, Material jm, float l1, float l2, float r1, float r2)
        {
            this.l1 = l1; this.l2 = l2; this.r1 = r1; this.r2 = r2;
            upper = Part(root, MeshKit.CapsulePrim, m1, Vector3.zero, Quaternion.identity, Vector3.one, "Upper");
            lower = Part(root, MeshKit.CapsulePrim, m2, Vector3.zero, Quaternion.identity, Vector3.one, "Lower");
            joint = Part(root, MeshKit.SpherePrim, jm, Vector3.zero, Quaternion.identity, Vector3.one * r2 * 2.1f, "Joint");
        }

        public void Solve(Vector3 a, Vector3 target, Vector3 hint)
        {
            Vector3 to = target - a;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.0001f ? to / dist : Vector3.down;
            float d = Mathf.Clamp(dist, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.001f);
            float x = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(l1 * l1 - x * x, 0f));
            Vector3 bend = Vector3.ProjectOnPlane(hint, dir);
            if (bend.sqrMagnitude < 0.000001f) bend = Vector3.Cross(dir, Vector3.right);
            bend.Normalize();
            Vector3 j = a + dir * x + bend * h;
            Vector3 e = a + dir * d;
            Place(upper, a, j, r1);
            Place(lower, j, e, r2);
            joint.position = j;
            End = e;
        }

        static void Place(Transform s, Vector3 a, Vector3 b, float r)
        {
            Vector3 d = b - a;
            float len = Mathf.Max(d.magnitude, 0.001f);
            s.position = (a + b) * 0.5f;
            s.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
            s.localScale = new Vector3(r * 2f, len * 0.5f + r * 0.4f, r * 2f);
        }
    }

    // ---------------------------------------------------------------- controls

    void LayoutButtons()
    {
        float s = Screen.height * 0.22f;
        float m = Screen.height * 0.04f;
        float big = s * 1.15f;
        leftBtn = new Rect(m, Screen.height - s - m, s, s);
        rightBtn = new Rect(m * 2f + s, Screen.height - s - m, s, s);
        pedalBtn = new Rect(Screen.width - big - m, Screen.height - big - m, big, big);
        brakeBtn = new Rect(Screen.width - big - s - m * 2f, Screen.height - s - m, s, s);
    }

    void ReadInput()
    {
        LayoutButtons();
        bool l = false, r = false, p = false, b = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
            Vector2 gp = new Vector2(t.position.x, Screen.height - t.position.y);
            if (leftBtn.Contains(gp)) l = true;
            if (rightBtn.Contains(gp)) r = true;
            if (pedalBtn.Contains(gp)) p = true;
            if (brakeBtn.Contains(gp)) b = true;
        }
        if (Input.touchCount == 0 && Input.GetMouseButton(0))
        {
            Vector2 gp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (leftBtn.Contains(gp)) l = true;
            if (rightBtn.Contains(gp)) r = true;
            if (pedalBtn.Contains(gp)) p = true;
            if (brakeBtn.Contains(gp)) b = true;
        }
        l |= Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        r |= Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        p |= Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
        b |= Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Space);

        left = l; right = r; pedal = p; brake = b;
        steerInput = (r ? 1f : 0f) - (l ? 1f : 0f);
    }

    void OnGUI()
    {
        if (btnStyle == null)
        {
            btnStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            btnStyle.normal.textColor = Color.white;
            labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.white;
            shadowStyle = new GUIStyle(labelStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        }
        btnStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.045f);
        labelStyle.fontSize = shadowStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.05f);

        LayoutButtons();
        DrawButton(leftBtn, "<", left);
        DrawButton(rightBtn, ">", right);
        DrawButton(brakeBtn, "BRAKE", brake);
        DrawButton(pedalBtn, "PEDAL", pedal);

        float m = Screen.height * 0.04f;
        string hud = string.Format("{0:0} km/h    {1:0.00} km", Mathf.Abs(speed) * 3.6f, distance / 1000f);
        Rect rr = new Rect(m, m, Screen.width * 0.6f, Screen.height * 0.1f);
        GUI.Label(new Rect(rr.x + 2f, rr.y + 2f, rr.width, rr.height), hud, shadowStyle);
        GUI.Label(rr, hud, labelStyle);
    }

    void DrawButton(Rect r, string text, bool on)
    {
        Color old = GUI.color;
        GUI.color = on ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.55f);
        GUI.Box(r, text, btnStyle);
        GUI.color = old;
    }
}
