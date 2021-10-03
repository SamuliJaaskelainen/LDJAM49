using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

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
    [SerializeField] private bool useLineToTriangleClipping = false;

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

        public void GenerateEdgeAngles(float3[] vertices, int[] triangles)
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

                    float3 a0 = vertices[triangles[triangleA * 3 + 0]];
                    float3 a1 = vertices[triangles[triangleA * 3 + 1]];
                    float3 a2 = vertices[triangles[triangleA * 3 + 2]];

                    float3 b0 = vertices[triangles[triangleB * 3 + 0]];
                    float3 b1 = vertices[triangles[triangleB * 3 + 1]];
                    float3 b2 = vertices[triangles[triangleB * 3 + 2]];

                    float3 normalA = math.cross(a1 - a0, a2 - a0);
                    float3 normalB = math.cross(b1 - b0, b2 - b0);

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

    private class MeshCache : IDisposable
    {
        public Mesh mesh { get; private set; }
        public bool edges { get; private set; }
        public int[] triangles { get; private set; }
        public float3[] vertices { get; private set; }
        public Transform transform { get; private set; }
        public RenderType renderType { get; private set; }
        public int skinIndex { get; private set; }
        public EdgeCache edgeCache { get; private set; }
        public float edgeAngleLimit { get; private set; }
        public int globalVertexOffset { get; private set; }
        private NativeArray<float3> nativeVerticesLocal; // { get; private set; }
        public NativeArray<float4> nativeVerticesClip; // { get; private set; }

        public MeshCache(Mesh mesh, Transform transform, RenderType renderType, int globalVertexOffset, int skinIndex = 0, float edgeAngleLimit = 0.0f)
        {
            this.mesh = mesh;
            vertices = new float3[mesh.vertexCount];
            for (int i = 0; i < mesh.vertexCount; ++i)
            {
                vertices[i] = mesh.vertices[i];
            }
            triangles = mesh.triangles;
            this.transform = transform;
            this.renderType = renderType;
            this.skinIndex = skinIndex;
            this.edgeCache = new EdgeCache(vertices.Length);
            this.edgeAngleLimit = edgeAngleLimit;
            this.globalVertexOffset = globalVertexOffset;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                edgeCache.AddEdge(triangles[i], triangles[i + 1], i / 3);
                edgeCache.AddEdge(triangles[i + 1], triangles[i + 2], i / 3);
                edgeCache.AddEdge(triangles[i + 2], triangles[i], i / 3);
            }
            edgeCache.GenerateEdgeAngles(vertices, triangles);

            nativeVerticesLocal = new NativeArray<float3>(vertices.Length, Allocator.Persistent);
            nativeVerticesClip = new NativeArray<float4>(vertices.Length, Allocator.Persistent);

            for (int i = 0; i < vertices.Length; ++i)
            {
                nativeVerticesLocal[i] = vertices[i];
            }

            // Debug.LogFormat("New MeshCache with {0} vertices", vertices.Length);
        }

        public void Animate(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            skinnedMeshRenderer.BakeMesh(mesh);
            triangles = mesh.triangles;
            for (int i = 0; i < mesh.vertexCount; ++i)
            {
                vertices[i] = mesh.vertices[i];
            }
        }

        public JobHandle UpdateClipVertices(float4x4 localToClip, NativeArray<float4> globalVerticesClip)
        {
            var job = new LocalToClipJob
            {
                localVertices = nativeVerticesLocal,
                localToClip = localToClip,
                // clipVertices = nativeVerticesClip
                destOffset = globalVertexOffset,
                clipVertices = globalVerticesClip
            };
            return job.Schedule();
        }

        public void Dispose()
        {
            nativeVerticesLocal.Dispose();
            nativeVerticesClip.Dispose();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    private struct LineTriangleClipJob : IJob
    {
        [ReadOnly]
        public NativeArray<float4> clipVertices;

        [ReadOnly]
        public NativeArray<int> drawnEdges;

        [ReadOnly]
        public NativeArray<int> triangles;
        // [ReadOnly]
        // public NativeArray<int> jumpEdges;

        // public NativeArray<NativeList<float2>> edgeIntersections;
        // public NativeList<int> edgeIntersectionsOffset;

        public UnsafeList<UnsafeList<float2>> edgeIntersections;

        [ReadOnly]
        public int edgeVertexCount;

        [ReadOnly]
        public int triangleVertexCount;

        public NativeList<float4> clippedEdges;

        private float2 GetXY(float4 v)
        {
            return new float2(v.x / v.w, v.y / v.w);
        }

        private float3 GetXYZ(float4 v)
        {
            return new float3(v.x / v.w, v.y / v.w, v.z / v.w);
        }

        private void LineSegmentIntersection(float2 a0, float2 a1, float2 b0, float2 b1, out float tA, out float tB)
        {
            tA = 0.0f;
            tB = 0.0f;
        }

        float2 intersectST(float2 p0, float2 p1, float2 p2, float2 p3)
        {
            float2 s1 = p1 - p0;
            float2 s2 = p3 - p2;

            float s = (s2.x * (p0.y - p2.y) - s2.y * (p0.x - p2.x)) / (-s2.x * s1.y + s1.x * s2.y);
            float t = (-s1.y * (p0.x - p2.x) + s1.x * (p0.y - p2.y)) / (-s2.x * s1.y + s1.x * s2.y);

            return new float2(s, t);
        }

        float2 intersectST(int iEdge, int iTriangleA, int iTriangleB, ref bool ok)
        {
            float4 a0 = clipVertices[drawnEdges[iEdge]];
            float4 a1 = clipVertices[drawnEdges[iEdge + 1]];

            float4 b0 = clipVertices[triangles[iTriangleA]];
            float4 b1 = clipVertices[triangles[iTriangleB]];

            if(math.all(a0 == b0) || math.all(a0 == b1) || math.all(a1 == b0) || math.all(a1 == b1))
            {
                ok = false;
            }

            return intersectST(GetXY(a0), GetXY(a1), GetXY(b0), GetXY(b1));
        }

        bool pointInTriangle(float2 p, float2 a, float2 b, float2 c)
        {
            var area = 1 / 2 * (-b.y * c.x + a.y * (-b.x + c.x) + a.x * (b.y - c.y) + b.x * c.y);
            var sign = area < 0 ? -1 : 1;
            var s = (a.y * c.x - a.x * c.y + (c.y - a.y) * p.x + (a.x - c.x) * p.y) * sign;
            var t = (a.x * b.y - a.y * b.x + (a.y - b.y) * p.x + (b.x - a.x) * p.y) * sign;
            return s > 0 && t > 0 && (s + t) < 2 * area * sign;
        }

        public struct Float2Comparer : IComparer<float2>
        {
            int IComparer<float2>.Compare(float2 a, float2 b)
            {
                if (a.x == b.x)
                {
                    return a.y.CompareTo(b.y);
                }
                return a.x.CompareTo(b.x);
            }
        }

        bool hitsTriangle(float2 st)
        {
            return (0 < st.y && st.y < 1);
        }

        public void Execute()
        {
            clippedEdges.Clear();
            for (int i = 0; i < edgeVertexCount / 2; ++i)
            {
                // edgeIntersections[i].Clear();
                // edgeIntersections[i].Clear();
            }

            Float2Comparer comparer = new Float2Comparer();
            NativeList<float2> intersections = new NativeList<float2>(Allocator.Temp);
            for (int iEdge = 0; iEdge < edgeVertexCount; iEdge += 2)
            {
                intersections.Clear();
                intersections.Add(new Unity.Mathematics.float2(0.0f, 0.0f));
                intersections.Add(new Unity.Mathematics.float2(1.0f, 0.0f));
                for (int iTriangle = 0; iTriangle < triangleVertexCount; iTriangle += 3)
                {
                    bool ok = true;
                    float2 st0 = intersectST(iEdge, iTriangle + 0, iTriangle + 1, ref ok);
                    float2 st1 = intersectST(iEdge, iTriangle + 1, iTriangle + 2, ref ok);
                    float2 st2 = intersectST(iEdge, iTriangle + 2, iTriangle + 0, ref ok);

                    if(!ok)
                    {
                        continue;
                    }

                    if ((hitsTriangle(st0) ? 1 : 0) + (hitsTriangle(st1) ? 1 : 0) + (hitsTriangle(st2) ? 1 : 0) < 2)
                    {
                        continue;
                    }

                    float2 hitA, hitB;

                    if (!hitsTriangle(st0))
                    {
                        hitA = st1;
                        hitB = st2;
                    }
                    else if (!hitsTriangle(st1))
                    {
                        hitA = st0;
                        hitB = st2;
                    }
                    else
                    {
                        hitA = st0;
                        hitB = st1;
                    }

                    if (hitA.x > hitB.x)
                    {
                        float2 temp = hitA;
                        hitA = hitB;
                        hitB = temp;
                    }

                    // if (!hits)

                    // Nuke line for now.
                    // edgeIntersections[iEdge / 2].Add(new float2(-999.0f, 1.0f));
                    // edgeIntersections[iEdge / 2].Add(new float2(999.0f, -1.0f));

                    // Infinite line and triangle intersect.

                    if(hitA.x > 1 || hitB.x < 0)
                    {
                        // Line segment and triangle do not overlap.
                        continue;
                    }


                    if (hitA.x < 0 && hitB.x > 1)
                    {
                        // Line segment is inside triangle.
                        float3 t0 = GetXYZ(clipVertices[triangles[iTriangle + 0]]);
                        float3 t1 = GetXYZ(clipVertices[triangles[iTriangle + 1]]);
                        float3 t2 = GetXYZ(clipVertices[triangles[iTriangle + 2]]);
                        float3 p = GetXYZ(clipVertices[drawnEdges[iEdge]]);
                        float3 normal = math.cross(t1 - t0, t2 - t0);
                        if (math.dot(normal, p - t0) < 0)
                        {
                            intersections.Add(new float2(-999.0f, 1.0f));
                            intersections.Add(new float2(999.0f, -1.0f));
                            continue;
                        }
                    }
                    else if (hitA.x > 0 && hitB.x < 1)
                    {
                        intersections.Add(new float2(hitA.x, 1.0f));
                        intersections.Add(new float2(hitB.x, -1.0f));
                        // Line segment bisects triangle.
                    }
                    else if (hitA.x < 0)
                    {
                        // Line segment starts inside triangle
                        intersections.Add(new float2(-999.0f, 1.0f));
                        intersections.Add(new float2(hitB.x, -1.0f));
                    }
                    else
                    {
                        // Line segment ends inside triangle
                        intersections.Add(new float2(hitA.x, 1.0f));
                    }

                    /*
                    bool aInside = pointInTriangle(a);
                    bool bInside = pointInTriangle(b);

                    if (aInside || bInside)
                    {
                        if (aInside && aInFront)
                        {
                            // Line segment must be in front of triangle, assuming no intersecting geometry.
                            continue;
                        }

                        if (bInside && bInFront)
                        {
                            continue;
                        }

                        if (aInside && bInside)
                        {
                            // Line segment must be in front of triangle, assuming no intersecting geometry.
                        }

                        if(!aInside)
                        {
                            // SWAP
                        }

                        // Line segment shortened by triangle.
                        continue;
                    }
                    */

                    // Line segment cut in two by triangle or triangle is behind it.

                    //for (int j = 0; j < jumpEdges.Length; j += 2)
                    //{
                    //    float4 a0 = clipVertices[drawnEdges[i + 0]];
                    //    float4 a1 = clipVertices[drawnEdges[i + 1]];

                    //    float4 b0 = clipVertices[drawnEdges[j + 0]];
                    //    float4 b1 = clipVertices[drawnEdges[j + 1]];

                    //    float tA, tB;
                    //    LineSegmentIntersection(GetXY(a0), GetXY(a1), GetXY(b0), GetXY(b1), out tA, out tB);

                    //    if (0.0f < tA && tA < 1.0f && 0.0f < tB && tB < 1.0f)
                    //    {
                    //        float zAInv = (1.0f - tA) * (a0.w / a0.z) + tA * (a0.w / a0.z);
                    //        float zBInv = (1.0f - tB) * (b0.w / b0.z) + tB * (b0.w / b0.z);
                    //        if(zAInv < zBInv) ///////// TODO CHECK SIGN
                    //        {
                    //            edgeIntersections[i].Add(tA);
                    //        }
                    //    }
                    //}
                    //edgeIntersections[i].Sort();
                }

                intersections.Sort(comparer);
                float sum = 0.0f;

                float4 start = clipVertices[drawnEdges[iEdge]];
                float4 end = clipVertices[drawnEdges[iEdge + 1]];

                for (int i = 0; i < intersections.Length - 1; ++i)
                {
                    sum += intersections[i].y;
                    if(sum == 0.0f)
                    {
                        ////////////////////// these interpolation values are in screen space, should be converted !
                        float t0 = intersections[i].x;
                        float t1 = intersections[i + 1].x;
                        clippedEdges.Add((1.0f - t0) * start + t0 * end);
                        clippedEdges.Add((1.0f - t1) * start + t1 * end);
                    }
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    private struct LocalToClipJob : IJob
    {
        [ReadOnly]
        public NativeArray<float3> localVertices;

        [ReadOnly]
        public float4x4 localToClip;

        [ReadOnly]
        public int destOffset;

        [WriteOnly]
        public NativeArray<float4> clipVertices;

        public void Execute()
        {
            for (int i = 0; i < localVertices.Length; ++i)
            {

                float3 offset = 0.0f; //  UnityEngine.Random.onUnitSphere * randomOffset;
                float3 localPoint = localVertices[i] + offset;
                float4 clipPoint = math.mul(localToClip, float4(localPoint.x, localPoint.y, localPoint.z, 1.0f));
                clipVertices[i + destOffset] = clipPoint;
            }
        }
    }

    private List<MeshCache> meshCaches = new List<MeshCache>();
    private bool cacheRequiresUpdate = false;
    private AudioRender.IRenderDevice renderDevice;

    private NativeArray<float4> globalClipVertices;
    private NativeArray<int> globalDrawnEdges;
    private NativeList<float4> globalDrawnEdgesClipped;
    private UnsafeList<UnsafeList<float2>> globalEdgeIntersections;
    // private NativeList<int> globalEdgeIntersectionsOffset;
    private NativeArray<int> globalTriangles;
    private int globalDrawnEdgeCount;
    private int globalTriangleCount;

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

        int maxTriangles = 40000;
        globalClipVertices = new NativeArray<float4>(maxTriangles * 3, Allocator.Persistent); ////// TODO: will crash if this runs out!!!!
        globalTriangles = new NativeArray<int>(maxTriangles, Allocator.Persistent);
        globalDrawnEdges = new NativeArray<int>(maxTriangles * 6, Allocator.Persistent);
        globalDrawnEdgesClipped = new NativeList<float4>(Allocator.Persistent);
        globalEdgeIntersections = new UnsafeList<UnsafeList<float2>>(maxTriangles * 3, AllocatorManager.Persistent);
        globalEdgeIntersections.Resize(maxTriangles * 3, NativeArrayOptions.ClearMemory);
        for(int i = 0; i < globalEdgeIntersections.Length; ++i)
        {
            globalEdgeIntersections[i] = new UnsafeList<float2>(4, AllocatorManager.Persistent);
        }
        // globalEdgeIntersectionsOffset = new NativeList<int>(Allocator.Persistent);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < meshCaches.Count; ++i)
        {
            meshCaches[i].Dispose();
        }
        meshCaches.Clear();
        Instance = null;
        Debug.Log("Stop scope rendering.");
        renderDevice?.Dispose();
        globalClipVertices.Dispose();
        globalDrawnEdges.Dispose();
        globalTriangles.Dispose();
        globalDrawnEdgesClipped.Dispose();
        globalEdgeIntersections.Dispose();
        // globalEdgeIntersectionsOffset.Dispose();
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


        var jobHandles = new NativeArray<JobHandle>(meshCaches.Count, Allocator.Temp);
        for (int i = 0; i < meshCaches.Count; ++i)
        {
            float4x4 localToClip = math.mul(renderCamera.projectionMatrix, math.mul(renderCamera.worldToCameraMatrix, meshCaches[i].transform.localToWorldMatrix));
            jobHandles[i] = meshCaches[i].UpdateClipVertices(localToClip, globalClipVertices);
            jobHandles[i].Complete();
        }
        // JobHandle.ScheduleBatchedJobs();
        // JobHandle.CompleteAll(jobHandles);
        jobHandles.Dispose();

        globalDrawnEdgeCount = 0;
        globalTriangleCount = 0;

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

                case RenderType.Skinned:
                    meshCaches[i].Animate(skinnedObjects[meshCaches[i].skinIndex].skinnedMeshRenderer);
                    break;
            }
        }

        if (useLineToTriangleClipping)
        {
            var job = new LineTriangleClipJob
            {
                clipVertices = globalClipVertices,
                drawnEdges = globalDrawnEdges,
                edgeIntersections = globalEdgeIntersections,
                // edgeIntersectionsOffset = globalEdgeIntersectionsOffset,
                clippedEdges = globalDrawnEdgesClipped,
                triangles = globalTriangles,
                edgeVertexCount = globalDrawnEdgeCount,
                triangleVertexCount = globalTriangleCount
            };
            job.Schedule().Complete();
        }

        GlobalRender();

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
        for (int i = 0; i < meshCaches.Count; ++i)
        {
            meshCaches[i].Dispose();
        }
        meshCaches.Clear();

        int globalVertexOffset = 0;

        foreach (StaticObject staticObject in staticObjects)
        {
            meshCaches.Add(new MeshCache(staticObject.meshFilter.mesh, staticObject.meshFilter.transform, staticObject.renderType, globalVertexOffset, edgeAngleLimit: staticObject.edgeAngleLimit));
            globalVertexOffset += meshCaches[meshCaches.Count - 1].vertices.Length;
        }

        for (int i = 0; i < skinnedObjects.Count; ++i)
        {
            meshCaches.Add(new MeshCache(skinnedObjects[i].skinnedMeshFilter.mesh, skinnedObjects[i].skinnedMeshFilter.transform, RenderType.Skinned, globalVertexOffset, i, edgeAngleLimit: skinnedObjects[i].edgeAngleLimit));
            globalVertexOffset += meshCaches[meshCaches.Count - 1].vertices.Length;
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

    private void AddTriangle(int cacheIndex, int triangleIdx)
    {
        if (globalTriangleCount + 3 > globalTriangles.Length)
        {
            return;
        }
        var meshCache = meshCaches[cacheIndex];
        for (int i = 0; i < 3; ++i)
        {
            int globalVertexIdx = meshCache.triangles[triangleIdx + i] + meshCache.globalVertexOffset;
            globalTriangles[globalTriangleCount++] = globalVertexIdx; 
            if(globalVertexIdx >= globalClipVertices.Length)
            {
                Debug.LogError("Triangle index too high");
            }
        }
    }

    private void AddLine(int cacheIndex, int triangleIdxA, int triangleIdxB)
    {
        if(globalDrawnEdgeCount + 2 > globalDrawnEdges.Length)
        {
            return;
        }
        var meshCache = meshCaches[cacheIndex];
        int indexA = meshCache.triangles[triangleIdxA] + meshCache.globalVertexOffset;
        int indexB = meshCache.triangles[triangleIdxB] + meshCache.globalVertexOffset;
        globalDrawnEdges[globalDrawnEdgeCount++] = indexA;
        globalDrawnEdges[globalDrawnEdgeCount++] = indexB;

        if (indexA >= globalClipVertices.Length || indexB >= globalClipVertices.Length)
        {
            Debug.LogError("Edge index too high");
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
                AddTriangle(cacheIndex, i);
                if (meshCache.edgeCache.GetEdgeAngle(meshCache.triangles[i], meshCache.triangles[i + 1]) >= meshCache.edgeAngleLimit)
                {
                    AddLine(cacheIndex, i + 0, i + 1);
                }
                if (meshCache.edgeCache.GetEdgeAngle(meshCache.triangles[i + 1], meshCache.triangles[i + 2]) >= meshCache.edgeAngleLimit)
                {
                    AddLine(cacheIndex, i + 1, i + 2);
                }
                if (meshCache.edgeCache.GetEdgeAngle(meshCache.triangles[i + 2], meshCache.triangles[i + 0]) >= meshCache.edgeAngleLimit)
                {
                    AddLine(cacheIndex, i + 2, i + 0);
                }
            }
        }
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
        var meshCache = meshCaches[cacheIndex];
        // float4 a = meshCache.nativeVerticesClip[meshCache.triangles[triangleListIdx]];
        // float4 b = meshCache.nativeVerticesClip[meshCache.triangles[triangleListIdx + 1]];
        // float4 c = meshCache.nativeVerticesClip[meshCache.triangles[triangleListIdx + 2]];
        float4 a = globalClipVertices[meshCache.triangles[triangleListIdx + 0] + meshCache.globalVertexOffset];
        float4 b = globalClipVertices[meshCache.triangles[triangleListIdx + 1] + meshCache.globalVertexOffset];
        float4 c = globalClipVertices[meshCache.triangles[triangleListIdx + 2] + meshCache.globalVertexOffset];

        float3 a3 = new float3(a.x / a.w, a.y / a.w, a.z / a.w);
        float3 b3 = new float3(b.x / b.w, b.y / b.w, b.z / b.w);
        float3 c3 = new float3(c.x / c.w, c.y / c.w, c.z / c.w);

        return math.cross(b3 - a3, c3 - a3).z < 0
            && !(a.z < -a.w && b.z < -b.w && c.z < -c.w) && !(a.z > a.w && b.z > b.w && c.z > c.w) // TODO: add proper frustrum cull
            && !(a.y < -a.w && b.y < -b.w && c.y < -c.w) && !(a.y > a.w && b.y > b.w && c.y > c.w) //
            && !(a.x < -a.w && b.x < -b.w && c.x < -c.w) && !(a.x > a.w && b.x > b.w && c.x > c.w);
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

    private void GlobalRender()
    {
        Debug.LogFormat("Performing global render for {0}/{1} edges, {2} triangles", globalDrawnEdgesClipped, globalDrawnEdgeCount / 2, globalTriangleCount / 3);
        if (useLineToTriangleClipping)
        {
            for (int i = 0; i < globalDrawnEdgesClipped.Length; i += 2)
            {
                Vector4 clipFrom = globalDrawnEdgesClipped[i];
                Vector4 clipTo = globalDrawnEdgesClipped[i + 1];

                if (!ClipCylinder(ref clipFrom, ref clipTo))
                {
                    continue;
                }

                renderDevice.SetPoint(ClipToScopePoint(clipFrom));
                renderDevice.DrawLine(ClipToScopePoint(clipTo));
            }
        } else
        {
            for (int i = 0; i < globalDrawnEdgeCount; i += 2)
            {
                Vector4 clipFrom = globalClipVertices[globalDrawnEdges[i]];
                Vector4 clipTo = globalClipVertices[globalDrawnEdges[i + 1]];

                if (!ClipCylinder(ref clipFrom, ref clipTo))
                {
                    continue;
                }

                renderDevice.SetPoint(ClipToScopePoint(clipFrom));
                renderDevice.DrawLine(ClipToScopePoint(clipTo));
            }
        }
    }

    private void DrawLine(int cacheIndex, int triangleListIdxFrom, int triangleListIdxTo)
    {
        MeshCache meshCache = meshCaches[cacheIndex];
        Vector4 clipFrom = meshCache.nativeVerticesClip[meshCache.triangles[triangleListIdxFrom]];
        Vector4 clipTo = meshCache.nativeVerticesClip[meshCache.triangles[triangleListIdxTo]];

        if (!ClipCylinder(ref clipFrom, ref clipTo))
        {
            return;
        }

        renderDevice.SetPoint(ClipToScopePoint(clipFrom));
        renderDevice.DrawLine(ClipToScopePoint(clipTo));
    }
}
