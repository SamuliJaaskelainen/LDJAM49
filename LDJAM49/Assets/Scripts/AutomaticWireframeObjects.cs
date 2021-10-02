using UnityEngine;

public class AutomaticWireframeObjects : MonoBehaviour
{
    [SerializeField] WireframeRenderer.RenderType renderType;
    [SerializeField] float edgeAngleLimit;

    void Start()
    {
        if (WireframeRenderer.Instance)
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(false);
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (!meshFilter.gameObject.GetComponent<WireframeObject>())
                {
                    var wireFrameObject = meshFilter.gameObject.AddComponent<WireframeObject>();
                    wireFrameObject.ChangeRenderType(renderType, edgeAngleLimit);
                }
            }
        }
        Destroy(this);
    }
}
