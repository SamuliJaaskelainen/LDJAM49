using UnityEngine;

public class AutomaticWireframeObjects : MonoBehaviour
{
    [SerializeField] WireframeRenderer.RenderType renderType;

    void Start()
    {
        if (WireframeRenderer.Instance)
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(false);
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (!meshFilter.gameObject.GetComponent<WireframeObject>())
                {
                    meshFilter.gameObject.AddComponent<WireframeObject>();
                    meshFilter.gameObject.GetComponent<WireframeObject>().ChangeRenderType(renderType);
                }
            }
        }
        Destroy(this);
    }
}
