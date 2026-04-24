using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class StableStickerPeel : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;
    public Transform surface;

    public int lengthSegments = 40;
    public int widthSegments = 10;
    public float width = 0.03f;

    public float peelHeight = 0.1f;
    public float peelLength = 0.25f;

    [Tooltip("Z-fighting thadukka surface-la irunthu chinna offset")]
    public float surfaceOffset = 0.001f;

    [Header("Peel Event")]
    public UnityEvent onFullyPeeled;

    Mesh mesh;
    Vector3[] baseLine;
    bool eventTriggered = false;

    void OnEnable()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        mesh = new Mesh();
        mesh.name = "StickerMesh";
        mf.sharedMesh = mesh;

        InitializeBaseLine();
        eventTriggered = false;
    }

    // Baseline-ai initial-aa calculate panni fixed-aa vachukkurom
    void InitializeBaseLine()
    {
        baseLine = new Vector3[lengthSegments + 1];
        for (int i = 0; i <= lengthSegments; i++)
        {
            float t = (float)i / lengthSegments;
            Vector3 worldPos = Vector3.Lerp(startPoint.position, endPoint.position, t);

            if (surface)
            {
                // Surface-oda plane-la points-ai align pannrom
                float distance = Vector3.Dot(surface.up, worldPos - surface.position);
                baseLine[i] = worldPos - surface.up * distance + (surface.up * surfaceOffset);
            }
            else
            {
                baseLine[i] = worldPos;
            }
        }
    }

    // 🔥 FIX: VR-la jitter thadukka LateUpdate thaan best
    void LateUpdate()
    {
        if (startPoint == null || endPoint == null) return;
        BuildMesh();
    }

    void BuildMesh()
    {
        int w = widthSegments + 1;
        int l = lengthSegments + 1;

        Vector3[] verts = new Vector3[w * l];
        Vector2[] uvs = new Vector2[w * l];
        List<int> tris = new List<int>();

        Vector3 up = surface ? surface.up : Vector3.up;

        // BaseLine update: Surface move aanaal kooda sticker surface-looda otti irukkum
        Vector3 dir = (baseLine[l - 1] - baseLine[0]).normalized;
        Vector3 right = Vector3.Cross(up, dir).normalized;

        float pullDist = Vector3.Distance(startPoint.position, baseLine[0]);
        float peelT = Mathf.Clamp01(pullDist / peelLength);

        if (peelT >= 1f && !eventTriggered)
        {
            eventTriggered = true;
            onFullyPeeled?.Invoke();
        }

        for (int y = 0; y < l; y++)
        {
            float t = (float)y / lengthSegments;
            Vector3 basePos;

            if (t < peelT)
            {
                float localT = t / peelT;
                Vector3 p0 = startPoint.position;
                Vector3 p2 = baseLine[y];
                Vector3 p1 = (p0 + p2) * 0.5f + up * peelHeight;

                basePos = Bezier(p0, p1, p2, localT);
            }
            else
            {
                basePos = baseLine[y];
            }

            for (int x = 0; x < w; x++)
            {
                float xf = (float)x / widthSegments;
                float offset = (-0.5f + xf) * width;
                Vector3 pos = basePos + right * offset;

                // Local space conversion
                verts[y * w + x] = transform.InverseTransformPoint(pos);
                uvs[y * w + x] = new Vector2(xf, t);
            }
        }

        for (int y = 0; y < l - 1; y++)
        {
            for (int x = 0; x < widthSegments; x++)
            {
                int i = y * w + x;
                tris.Add(i);
                tris.Add(i + w);
                tris.Add(i + 1);

                tris.Add(i + 1);
                tris.Add(i + w);
                tris.Add(i + w + 1);
            }
        }

        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds(); // Culling issues thadukka
    }

    Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        return Mathf.Pow(1 - t, 2) * a +
               2 * (1 - t) * t * b +
               Mathf.Pow(t, 2) * c;
    }
}