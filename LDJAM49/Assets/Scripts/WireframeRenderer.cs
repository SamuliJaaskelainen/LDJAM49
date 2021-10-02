using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using UnityEngine;

public class WireframeRenderer : MonoBehaviour
{
    public enum RenderType
    {
        Line,
        Triangle,
        Quad,
        Skinned
    }

    public static WireframeRenderer Instance;

    [Header("Camera to render lines from. Use square aspect for improved preview.")]
    [SerializeField] private bool useAudioRender = false;
    [SerializeField] private Camera renderCamera;
    public float randomOffset = 0.0f;
    [SerializeField] private float intensity = 0.35f;

    private class RenderObject
    {
        public float edgeAngleLimit { get; private set; }

        public RenderObject(float edgeAngleLimit)
        {
            this.edgeAngleLimit = edgeAngleLimit;
        }
    }

    private class StaticObject : RenderObject
    {
        public RenderType renderType;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer; // Optional, used for occlusion culling

        public StaticObject(RenderType renderType, MeshFilter meshFilter, MeshRenderer meshRenderer, float edgeAngleLimit)
            : base(edgeAngleLimit)
        {
            this.renderType = renderType;
            this.meshFilter = meshFilter;
            this.meshRenderer = meshRenderer;
        }
    }
    private ObservableCollection<StaticObject> staticObjects = new ObservableCollection<StaticObject>();

    private class SkinnedObject : RenderObject
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public MeshFilter skinnedMeshFilter;

        public SkinnedObject(SkinnedMeshRenderer skinnedMeshRenderer, MeshFilter skinnedMeshFilter, float edgeAngleLimit)
            : base(edgeAngleLimit)
        {
            this.skinnedMeshRenderer = skinnedMeshRenderer;
            this.skinnedMeshFilter = skinnedMeshFilter;
        }
    }
    private ObservableCollection<SkinnedObject> skinnedObjects = new ObservableCollection<SkinnedObject>();

    private class EdgeCache
    {
        private Dictionary<Tuple<int, int>, int> vertexPairToEdge;
        private List<Tuple<int, int>> edgeTriangles;
        private float[] edgeAngles;

        public EdgeCache(int maxEdgeCount)
        {
            this.edgeTriangles = new List<Tuple<int, int>>(maxEdgeCount);
            this.vertexPairToEdge = new Dictionary<Tuple<int, int>, int>(maxEdgeCount);
        }

        public void AddEdge(int vertexA, int vertexB, int triangle)
        {
            var vertexTuple = GetVertexTuple(vertexA, vertexB);
            int edge = -1;
            if (!vertexPairToEdge.TryGetValue(vertexTuple, out edge))
            {
                edge = edgeTriangles.Count;
                vertexPairToEdge[vertexTuple] = edge;
                edgeTriangles.Add(new Tuple<int, int>(triangle, -1));
                // Debug.LogFormat("AddEdge {0} {1} new", vertexA, vertexB);
            }
            else
            {
                edgeTriangles[edge] = new Tuple<int, int>(edgeTriangles[edge].Item1, triangle);
                // Debug.LogFormat("AddEdge {0} {1} found", vertexA, vertexB);
            }
        }

        public void GenerateEdgeAngles(Vector3[] vertices, int[] triangles)
        {
            edgeAngles = new float[edgeTriangles.Count];
            for (int i = 0; i < edgeTriangles.Count; ++i)
            {
                if (edgeTriangles[i].Item2 == -1)
                {
                    edgeAngles[i] = 360.0f;
                }
                else
                {
                    int triangleA = edgeTriangles[i].Item1;
                    int triangleB = edgeTriangles[i].Item2;

                    Vector3 a0 = vertices[triangles[triangleA * 3 + 0]];
                    Vector3 a1 = vertices[triangles[triangleA * 3 + 1]];
                    Vector3 a2 = vertices[triangles[triangleA * 3 + 2]];

                    Vector3 b0 = vertices[triangles[triangleB * 3 + 0]];
                    Vector3 b1 = vertices[triangles[triangleB * 3 + 1]];
                    Vector3 b2 = vertices[triangles[triangleB * 3 + 2]];

                    Vector3 normalA = Vector3.Cross(a1 - a0, a2 - a0);
                    Vector3 normalB = Vector3.Cross(b1 - b0, b2 - b0);

                    edgeAngles[i] = Vector3.Angle(normalA, normalB);
                    // Debug.LogFormat("{0}, {1}, {2}, {3}, {4}", edgeAngles[i].ToString("0.00000"), triangleA, triangleB, normalA, normalB);
                }
            }
        }

        public float GetEdgeAngle(int edge)
        {
            return edgeAngles[edge];
        }

        public float GetEdgeAngle(int vertexA, int vertexB)
        {
            var tuple = GetVertexTuple(vertexA, vertexB);
            int edge = vertexPairToEdge[tuple];
            float angle = edgeAngles[edge];
            // Debug.LogFormat("GetEdgeAngle({0}, {1}) -> edge={2} -> angle={3}", vertexA, vertexB, edge, angle);
            return angle;
        }

        private Tuple<int, int> GetVertexTuple(int vertexA, int vertexB)
        {
            return new Tuple<int, int>(Math.Min(vertexA, vertexB), Math.Max(vertexA, vertexB));
        }
    }

    private class MeshCache
    {
        public Mesh mesh { get; private set; }
        public bool edges { get; private set; }
        public int[] triangles { get; private set; }
        public Vector3[] vertices { get; private set; }
        public Transform transform { get; private set; }
        public RenderType renderType { get; private set; }
        public int skinIndex { get; private set; }
        public EdgeCache edgeCache { get; private set; }
        public float edgeAngleLimit { get; private set; }

        public MeshCache(Mesh mesh, Transform transform, RenderType renderType, int skinIndex = 0, float edgeAngleLimit = 0.0f)
        {
            this.mesh = mesh;
            vertices = mesh.vertices;
            triangles = mesh.triangles;
            this.transform = transform;
            this.renderType = renderType;
            this.skinIndex = skinIndex;
            this.edgeCache = new EdgeCache(vertices.Length);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                edgeCache.AddEdge(triangles[i], triangles[i + 1], i / 3);
                edgeCache.AddEdge(triangles[i + 1], triangles[i + 2], i / 3);
                edgeCache.AddEdge(triangles[i + 2], triangles[i], i / 3);
            }
            edgeCache.GenerateEdgeAngles(vertices, triangles);

            // Debug.LogFormat("New MeshCache with {0} vertices", vertices.Length);
        }

        public void Animate(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            skinnedMeshRenderer.BakeMesh(mesh);
            triangles = mesh.triangles;
            vertices = mesh.vertices;
        }
    }

    private List<MeshCache> meshCaches = new List<MeshCache>();
    private bool cacheRequiresUpdate = false;
    private AudioRender.IRenderDevice renderDevice;
    private Matrix4x4 frameMatrix;

    public void AddMesh(RenderType renderType, MeshFilter meshFilter, MeshRenderer meshRenderer, float edgeAngleLimit)
    {
        staticObjects.Add(new StaticObject(renderType, meshFilter, meshRenderer, edgeAngleLimit));
    }

    public void AddSkinnedMesh(SkinnedMeshRenderer skinnedMeshRenderer, MeshFilter meshFilter, float edgeAngleLimit)
    {
        skinnedObjects.Add(new SkinnedObject(skinnedMeshRenderer, meshFilter, edgeAngleLimit));
    }

    public void RemoveMesh(RenderType renderType, MeshFilter meshFilter)
    {
        foreach (StaticObject staticObject in staticObjects)
        {
            if (staticObject.renderType == renderType && staticObject.meshFilter == meshFilter)
            {
                staticObjects.Remove(staticObject);
                return;
            }
        }
    }

    public void RemoveSkinnedMesh(SkinnedMeshRenderer skinnedMeshRenderer, MeshFilter meshFilter)
    {
        foreach (SkinnedObject skinnedObject in skinnedObjects)
        {
            if (skinnedObject.skinnedMeshRenderer == skinnedMeshRenderer && skinnedObject.skinnedMeshFilter == meshFilter)
            {
                skinnedObjects.Remove(skinnedObject);
                return;
            }
        }
    }

    public void ClearAllMeshes()
    {
        staticObjects.Clear();
        skinnedObjects.Clear();
    }

    private void Awake()
    {
        Instance = this;

        if (!renderCamera)
        {
            renderCamera = Camera.main;
        }

        staticObjects.CollectionChanged += NotifyCacheForUpdate;
        skinnedObjects.CollectionChanged += NotifyCacheForUpdate;
    }

    private void Start()
    {
        // TODO: This won't work in standalone builds.
        if (useAudioRender)
        {
            Debug.Log("Initializing AudioRenderDevice");
            renderDevice = new AudioRender.AudioRenderDevice(new Vector2(-2.0f, -2.0f));
            Debug.Log("AudioRenderDevice initialized");
        }
        else
        {
            Debug.Log("Initializing ScreenRenderDevice");
            renderDevice = new AudioRender.ScreenRenderDevice(Application.streamingAssetsPath + "/ScopeBackground.jpg", true, true);
            Debug.Log("ScreenRenderDevice initialized");
        }
    }

    private void OnDestroy()
    {
        Instance = null;
        Debug.Log("Stop scope rendering.");
        renderDevice?.Dispose();
    }

    private void Update()
    {
        //Debug.Log("Starting WireframeRenderer update");

        renderDevice.Begin();
        renderDevice.SetIntensity(intensity);
        // renderDevice.SetPoint(Vector2.zero);
        // renderDevice.DrawCircle(0.5f);

        if (cacheRequiresUpdate)
        {
            UpdateCache();
        }

        frameMatrix = renderCamera.projectionMatrix * renderCamera.worldToCameraMatrix;

        for (int i = 0; i < meshCaches.Count; ++i)
        {
            switch (meshCaches[i].renderType)
            {
                case RenderType.Line:
                    DrawLines(i);
                    break;

                case RenderType.Triangle:
                    DrawTriangles(i);
                    break;

                case RenderType.Quad:
                    DrawQuads(i);
                    break;

                case RenderType.Skinned:
                    meshCaches[i].Animate(skinnedObjects[meshCaches[i].skinIndex].skinnedMeshRenderer);
                    break;
            }
        }

        renderDevice.WaitSync();
        renderDevice.Submit();

        //Debug.Log("Ending WireframeRenderer update");
    }

    private void NotifyCacheForUpdate(object sender = null, NotifyCollectionChangedEventArgs e = null)
    {
        cacheRequiresUpdate = true;
    }

    private void UpdateCache()
    {
        Debug.Log("Update mesh cache.");
        meshCaches.Clear();

        foreach (StaticObject staticObject in staticObjects)
        {
            meshCaches.Add(new MeshCache(staticObject.meshFilter.mesh, staticObject.meshFilter.transform, staticObject.renderType, edgeAngleLimit: staticObject.edgeAngleLimit));
        }

        for (int i = 0; i < skinnedObjects.Count; ++i)
        {
            meshCaches.Add(new MeshCache(skinnedObjects[i].skinnedMeshFilter.mesh, skinnedObjects[i].skinnedMeshFilter.transform, RenderType.Skinned, i, edgeAngleLimit: skinnedObjects[i].edgeAngleLimit));
        }
        cacheRequiresUpdate = false;
    }

    private void DrawLines(int cacheIndex)
    {
        if (meshCaches[cacheIndex].mesh && meshCaches[cacheIndex].transform)
        {
            for (int i = 0; i < meshCaches[cacheIndex].vertices.Length; i += 2)
            {
                DrawLine(cacheIndex, i + 0, i + 1);
            }
        }
    }

    private void DrawTriangles(int cacheIndex)
    {
        if (!meshCaches[cacheIndex].mesh && !meshCaches[cacheIndex].transform)
        {
            return;
        }
        if (staticObjects[cacheIndex].meshRenderer && !staticObjects[cacheIndex].meshRenderer.isVisible)
        {
            // Occlusion culling
            return;
        }
        var meshCache = meshCaches[cacheIndex];
        for (int i = 0; i < meshCache.triangles.Length; i += 3)
        {
            if (TriangleFacesCamera(cacheIndex, i))
            {
                if(meshCache.edgeCache.GetEdgeAngle(meshCache.triangles[i], meshCache.triangles[i + 1]) >= meshCache.edgeAngleLimit)
                {
                    DrawLine(cacheIndex, i + 0, i + 1);
                }
                if (meshCache.edgeCache.GetEdgeAngle(meshCache.triangles[i + 1], meshCache.triangles[i + 2]) >= meshCache.edgeAngleLimit)
                {
                    DrawLine(cacheIndex, i + 1, i + 2);
                }
                if (meshCache.edgeCache.GetEdgeAngle(meshCache.triangles[i + 2], meshCache.triangles[i + 0]) >= meshCache.edgeAngleLimit)
                {
                    DrawLine(cacheIndex, i + 2, i + 0);
                }
            }
        }
    }

    private void DrawQuads(int cacheIndex)
    {
        if (meshCaches[cacheIndex].mesh && meshCaches[cacheIndex].transform)
        {
            for (int i = 0; i < meshCaches[cacheIndex].triangles.Length; i += 6)
            {
                if (TriangleFacesCamera(cacheIndex, i))
                {
                    DrawLine(cacheIndex, i + 0, i + 1);
                    DrawLine(cacheIndex, i + 1, i + 2);
                }

                if (TriangleFacesCamera(cacheIndex, i + 3))
                {
                    DrawLine(cacheIndex, i + 4, i + 5);
                    DrawLine(cacheIndex, i + 5, i + 3);
                }
            }
        }
    }

    private Vector4 GetClipPoint(int cacheIndex, int triangleListIdx)
    {
        Vector3 offset = UnityEngine.Random.onUnitSphere * randomOffset;
        Vector3 localPoint = meshCaches[cacheIndex].vertices[meshCaches[cacheIndex].triangles[triangleListIdx]];
        Vector3 worldPoint = meshCaches[cacheIndex].transform.TransformPoint(localPoint) + offset;
        Vector4 clipPoint = frameMatrix * new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, 1.0f);
        return clipPoint;
    }

    private Vector2 ClipToScopePoint(Vector4 clipPoint)
    {
        Vector3 ndcPoint = clipPoint / clipPoint.w;
        Vector2 scopePoint = new Vector2(0.5f, -0.5f) * ndcPoint;

        float aspectRatio = Screen.width / (float)Screen.height;
        scopePoint.x *= aspectRatio;

        return scopePoint;
    }

    private Vector3 GetScreenPoint(int cacheIndex, int triangleListIdx)
    {
        Vector3 localPoint = meshCaches[cacheIndex].vertices[meshCaches[cacheIndex].triangles[triangleListIdx]];
        Vector3 screenPoint = renderCamera.WorldToScreenPoint(meshCaches[cacheIndex].transform.TransformPoint(localPoint));
        return screenPoint;
    }

    private Vector2 GetScopePoint(int cacheIndex, int triangleListIdx)
    {
        Vector2 screenPoint = GetScreenPoint(cacheIndex, triangleListIdx);
        Vector2 scopePoint = (screenPoint / new Vector2(Screen.width, Screen.height) - new Vector2(0.5f, 0.5f)) * new Vector2(1.0f, -1.0f);

        float aspectRatio = Screen.width / (float)Screen.height;
        scopePoint.x *= aspectRatio;

        return scopePoint;
    }

    private bool TriangleFacesCamera(int cacheIndex, int triangleListIdx)
    {
        Vector3 a = GetScreenPoint(cacheIndex, triangleListIdx);
        Vector3 b = GetScreenPoint(cacheIndex, triangleListIdx + 1);
        Vector3 c = GetScreenPoint(cacheIndex, triangleListIdx + 2);
        return Vector3.Cross(b - a, c - a).z < 0;
    }

    private void SetPoint(int cacheIndex, int triangleListIdx)
    {
        Vector2 target = GetScopePoint(cacheIndex, triangleListIdx);
        renderDevice.SetPoint(target);
    }

    private bool ClipCylinder(ref Vector4 a, ref Vector4 b)
    {
        // Swap so that a.w <= b.w
        if (a.w > b.w)
        {
            Vector4 temp = a;
            a = b;
            b = temp;
        }

        // Segment completely outside near/far plane.
        if (a.z > a.w || b.z < -b.w)
        {
            return false;
        }

        // Find C = (1 - t) * a + t * b such that C_x^2 + C_y^2 = C_w^2
        // First solve for t by formulating the problem as quadratic equation t^2*qa + t*qb + qc = 0
        // Coefficients for solving the quadratic equation: FullSimplify[MonomialList[((t-1)*Subscript[A, x]+t*Subscript[B,x])^2+((t-1)*Subscript[A, y]+t*Subscript[B,y])^2-((t-1)*Subscript[A, w]+t*Subscript[B,w])^2, t]]
        float qa = (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) - (a.w - b.w) * (a.w - b.w);
        float qb = 2.0f * ((b.x - a.x) * a.x + (b.y - a.y) * a.y + a.w * a.w - a.w * b.w);
        float qc = a.x * a.x + a.y * a.y - a.w * a.w;
        float det = qb * qb - 4 * qa * qc;

        // Cull if no intersection between line and cylinder.
        if (Mathf.Abs(qa) < Mathf.Epsilon || det < 0.0f)
        {
            return false;
        }

        // Solve intersection with cylinder.
        float tA = (-qb - Mathf.Sqrt(det)) / (2.0f * qa);
        float tB = (-qb + Mathf.Sqrt(det)) / (2.0f * qa);

        // Swap so that tA <= tB
        if (tA > tB)
        {
            float temp = tA;
            tA = tB;
            tB = temp;
        }

        // Solve intersection with near and far plane.
        float tNear = (a.z + a.w) / (a.z + a.w - b.z - b.w);
        float tFar = (a.z - a.w) / (b.w - a.w + a.z - b.z);

        // Solve intersection points.
        Vector4 pNear = (1 - tNear) * a + tNear * b;
        Vector4 pFar = (1 - tFar) * a + tFar * b;
        Vector4 pA = (1 - tA) * a + tA * b;
        Vector4 pB = (1 - tB) * a + tB * b;

        bool lineIntersectsWithNearCircle = pNear.x * pNear.x + pNear.y * pNear.y < pNear.w * pNear.w;
        bool lineIntersectsWithFarCircle = pFar.x * pFar.x + pFar.y * pFar.y < pFar.w * pFar.w;

        // Solve intersection combinations.
        if (lineIntersectsWithFarCircle)
        {
            tA = lineIntersectsWithNearCircle ? tNear : (tA > tNear ? tA : tB);
            tB = tFar;
        }
        else
        {
            if (lineIntersectsWithNearCircle)
            {
                if (pB.z < -pB.w)
                {
                    // Furthest cylinder intersection is behind near plane, cull.
                    return false;
                }
                else
                {
                    // Clip to near plane.
                    tA = tNear;
                }
            }
            else if (pA.z < -pA.w)
            {
                // Ray does not intersect with near and far circle, AND does not lay between the circles because it hits the cylinder behind the near plane, cull.
                return false;
            }
        }

        // Clip to the original line segment.
        tA = Mathf.Max(0.0f, tA);
        tB = Mathf.Min(1.0f, tB);

        // The line segment inside the frustrum does not overlap with the line segment being clipped.
        if (tA > 1.0f || tB < 0.0f)
        {
            return false;
        }

        // Interpolate and output results.
        Vector4 clippedA = (1 - tA) * a + tA * b;
        Vector4 clippedB = (1 - tB) * a + tB * b;
        a = clippedA;
        b = clippedB;

        return true;
    }

    private void DrawLine(int cacheIndex, int triangleListIdxFrom, int triangleListIdxTo)
    {
        Vector4 a = Vector4.zero;
        Vector4 b = Vector4.zero;

        Vector4 clipFrom = GetClipPoint(cacheIndex, triangleListIdxFrom);
        Vector4 clipTo = GetClipPoint(cacheIndex, triangleListIdxTo);

        if (!ClipCylinder(ref clipFrom, ref clipTo))
        {
            return;
        }

        renderDevice.SetPoint(ClipToScopePoint(clipFrom));
        renderDevice.DrawLine(ClipToScopePoint(clipTo));
    }
}
