using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WireframeObject : MonoBehaviour
{
    [Header("Skinned mesh to bake into mesh filter. Leave empty if object is not animated.")]
    [SerializeField] SkinnedMeshRenderer skinnedMeshRenderer;

    [Header("Mesh render method. Ignore skinned type, it is automatic.")]
    [SerializeField] WireframeRenderer.RenderType renderType = WireframeRenderer.RenderType.Triangle;

    MeshFilter meshFilter;

    private void OnEnable()
    {
        if (!meshFilter)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (skinnedMeshRenderer)
        {
            if (WireframeRenderer.Instance)
            {
                WireframeRenderer.Instance.AddSkinnedMesh(skinnedMeshRenderer, meshFilter);
            }
        }
        else
        {
            if (WireframeRenderer.Instance)
            {
                WireframeRenderer.Instance.AddMesh(renderType, meshFilter);
            }
        }
    }

    private void OnDisable()
    {
        if (skinnedMeshRenderer)
        {
            if (WireframeRenderer.Instance)
            {
                WireframeRenderer.Instance.RemoveSkinnedMesh(skinnedMeshRenderer, meshFilter);
            }
        }
        else
        {
            if (WireframeRenderer.Instance)
            {
                WireframeRenderer.Instance.RemoveMesh(renderType, meshFilter);
            }
        }
    }

    public void ChangeRenderType(WireframeRenderer.RenderType newRenderType)
    {
        if (!skinnedMeshRenderer)
        {
            if (WireframeRenderer.Instance)
            {
                WireframeRenderer.Instance.RemoveMesh(renderType, meshFilter);
            }

            renderType = newRenderType;

            if (WireframeRenderer.Instance)
            {
                WireframeRenderer.Instance.AddMesh(renderType, meshFilter);
            }
        }
    }
}
