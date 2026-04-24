using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RubberClothPhysics : MonoBehaviour
{
    [Header("Attach Points")]
    public Transform startPoint;
    public Transform endPoint;

    [Header("Mesh Resolution")]
    [Range(5, 100)] public int widthSegments = 20;
    [Range(10, 200)] public int lengthSegments = 60;
    public float clothWidth = 0.5f;

    [Header("DUAL FEED CONTROL")]
    public float feedLength = 0f;
    public float startHideLength = 0f;

    [Header("Diagonal Cut Effect")]
    [Range(0f, 1f)] public float cutStartThreshold = 0.5f;

    [Header("NEW - Right Side Cut")]
    public bool useRightSideCut = false;
    [Range(0f, 1f)] public float cutStartThresholdRight = 0.5f;

    [Header("Physics")]
    public float gravity = -9.8f;
    public float damping = 0.985f;
    public int solverIterations = 12;
    public float stiffness = 0.9f;
    public float bendSoftness = 0.15f;

    [Header("Cylinder Wrap")]
    public Transform cylinder;
    public float cylinderRadius = 0.25f;
    public float cylinderOffset = 0.002f;

    [Header("Collision")]
    public LayerMask collisionLayer;
    public float collisionRadius = 0.02f;

    Mesh mesh;
    Vector3[] center;
    Vector3[] prev;
    float[] arcLength;
    float restDistance;
    bool initialized;

    void OnEnable()
    {
        InitMesh();
        InitializeClothImmediate();
    }

    void InitMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mesh == null) mesh = new Mesh();
        mf.sharedMesh = mesh;
    }

    void InitializeClothImmediate()
    {
        if (!startPoint || !endPoint) return;

        center = new Vector3[lengthSegments + 1];
        prev = new Vector3[lengthSegments + 1];
        arcLength = new float[lengthSegments + 1];

        for (int i = 0; i <= lengthSegments; i++)
        {
            float t = (float)i / lengthSegments;
            Vector3 pos = Vector3.Lerp(startPoint.position, endPoint.position, t);
            center[i] = pos;
            prev[i] = pos;
        }

        restDistance = Vector3.Distance(startPoint.position, endPoint.position) / lengthSegments;
        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;
        Simulate();
        BuildMesh();
    }

    void Simulate()
    {
        float dt = Application.isPlaying ? Time.fixedDeltaTime : 0.016f;

        center[0] = startPoint.position;
        center[lengthSegments] = endPoint.position;

        for (int i = 1; i < lengthSegments; i++)
        {
            Vector3 vel = (center[i] - prev[i]) * damping;
            prev[i] = center[i];
            center[i] += vel;
            center[i].y += gravity * dt * dt;
        }

        for (int k = 0; k < solverIterations; k++)
        {
            for (int i = 0; i < lengthSegments; i++) Constrain(i, i + 1);
            for (int i = 1; i < lengthSegments - 1; i++)
            {
                Vector3 mid = (center[i - 1] + center[i + 1]) * 0.5f;
                center[i] = Vector3.Lerp(center[i], mid, bendSoftness);
            }
            if (cylinder != null)
                for (int i = 1; i < lengthSegments; i++) SolveCylinder(i);

            for (int i = 1; i < lengthSegments; i++) SolveCollision(i);

            center[0] = startPoint.position;
            center[lengthSegments] = endPoint.position;
        }
    }

    void Constrain(int a, int b)
    {
        Vector3 delta = center[b] - center[a];
        float dist = delta.magnitude;
        if (dist == 0) return;
        float diff = (dist - restDistance) / dist;
        Vector3 move = delta * diff * 0.5f * stiffness;
        if (a != 0) center[a] += move;
        if (b != lengthSegments) center[b] -= move;
    }

    void SolveCylinder(int i)
    {
        Vector3 axis = cylinder.up;
        Vector3 cPos = cylinder.position;
        Vector3 point = center[i];
        Vector3 toPoint = point - cPos;
        float height = Vector3.Dot(toPoint, axis);
        Vector3 axisPoint = cPos + axis * height;
        Vector3 radial = point - axisPoint;
        float dist = radial.magnitude;
        float target = cylinderRadius + cylinderOffset;
        if (dist < target && dist > 0.0001f) center[i] = axisPoint + radial.normalized * target;
    }

    void SolveCollision(int i)
    {
        Collider[] hits = Physics.OverlapSphere(center[i], collisionRadius, collisionLayer);
        foreach (Collider col in hits)
        {
            Vector3 closest = col.ClosestPoint(center[i]);
            Vector3 dir = center[i] - closest;
            if (dir.magnitude < collisionRadius) center[i] = closest + dir.normalized * collisionRadius;
        }
    }

    void CalculateArcLength()
    {
        arcLength[0] = 0f;
        for (int i = 1; i <= lengthSegments; i++)
            arcLength[i] = arcLength[i - 1] + Vector3.Distance(center[i - 1], center[i]);
    }

    void BuildMesh()
    {
        if (mesh == null || center == null) return;
        CalculateArcLength();

        float totalLength = arcLength[lengthSegments];
        float startCut = Mathf.Clamp(startHideLength, 0f, totalLength);
        float endCut = Mathf.Clamp(feedLength, 0f, totalLength);

        if (endCut <= startCut) { mesh.Clear(); return; }

        List<Vector3> tempCenters = new List<Vector3>();
        List<float> tempTValues = new List<float>();

        for (int i = 1; i <= lengthSegments; i++)
        {
            float segStart = arcLength[i - 1];
            float segEnd = arcLength[i];
            if (segEnd < startCut) continue;
            if (segStart > endCut) break;

            float t0 = Mathf.InverseLerp(segStart, segEnd, startCut);
            float t1 = Mathf.InverseLerp(segStart, segEnd, endCut);

            if (segStart <= startCut && segEnd >= startCut)
            {
                tempCenters.Add(Vector3.Lerp(center[i - 1], center[i], t0));
                tempTValues.Add(Mathf.Lerp((float)(i - 1) / lengthSegments, (float)i / lengthSegments, t0));
            }
            if (segEnd <= endCut && segEnd > startCut)
            {
                tempCenters.Add(center[i]);
                tempTValues.Add((float)i / lengthSegments);
            }
            if (segStart <= endCut && segEnd >= endCut && segStart > startCut)
            {
                tempCenters.Add(Vector3.Lerp(center[i - 1], center[i], t1));
                tempTValues.Add(Mathf.Lerp((float)(i - 1) / lengthSegments, (float)i / lengthSegments, t1));
                break;
            }
        }

        if (tempCenters.Count < 2) { mesh.Clear(); return; }

        Vector3 forward = (endPoint.position - startPoint.position).normalized;
        Vector3 right = Vector3.Cross(transform.up, forward).normalized;

        int l = tempCenters.Count;
        int w = widthSegments + 1;
        Vector3[] verts = new Vector3[w * l];
        Vector2[] uvs = new Vector2[w * l];
        List<int> tris = new List<int>();

        for (int y = 0; y < l; y++)
        {
            float t = tempTValues[y];
            float currentThreshold = useRightSideCut ? cutStartThresholdRight : cutStartThreshold;

            float widthFactor = 1f;
            if (t > currentThreshold)
            {
                widthFactor = 1f - ((t - currentThreshold) / (1f - currentThreshold));
            }

            for (int x = 0; x < w; x++)
            {
                float xf = (float)x / widthSegments;
                float widthPos;
                float uvX; // டெக்ஸ்சருக்கான புதிய X மதிப்பு

                if (!useRightSideCut)
                {
                    // LEFT SIDE FIXED
                    widthPos = (-0.5f * clothWidth) + (xf * clothWidth * widthFactor);
                    // UV calculation: இடது பக்கம் நிலையானது என்பதால், 0-லிருந்து ஆரம்பித்து widthFactor-க்கு ஏற்ப அமையும்.
                    uvX = xf * widthFactor;
                }
                else
                {
                    // RIGHT SIDE FIXED
                    widthPos = (0.5f * clothWidth) - (xf * clothWidth * widthFactor);
                    // UV calculation: வலது பக்கம் நிலையானது என்பதால், 1-லிருந்து பின்னோக்கி அமையும்.
                    uvX = 1f - (xf * widthFactor);
                }

                Vector3 pos = tempCenters[y] + right * widthPos;
                verts[y * w + x] = transform.InverseTransformPoint(pos);

                // FIXED UV: இப்போது அகலம் குறையும் போது டெக்ஸ்சர் கட் ஆகுமே தவிர, சுருங்காது.
                uvs[y * w + x] = new Vector2(uvX, t);
            }
        }

        for (int y = 0; y < l - 1; y++)
        {
            for (int x = 0; x < widthSegments; x++)
            {
                int i = y * w + x;
                tris.Add(i); tris.Add(i + w); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + w); tris.Add(i + w + 1);
            }
        }

        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs;
        mesh.RecalculateNormals();
    }
}