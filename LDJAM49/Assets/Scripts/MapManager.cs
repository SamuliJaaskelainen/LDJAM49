using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    [SerializeField] GameObject mapParent;
    [SerializeField] GameObject gameParent;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (mapParent.activeInHierarchy)
        {
            float horizontal = Input.GetAxis("Mouse X") * 100.0f * Time.deltaTime;
            float vertical = -Input.GetAxis("Mouse Y") * 100.0f * Time.deltaTime;
            mapParent.transform.Rotate(vertical, horizontal, 0.0f, Space.Self);
        }
    }

    public void ShowMap()
    {
        mapParent.SetActive(true);
        gameParent.SetActive(false);
    }

    public void HideMap()
    {
        mapParent.SetActive(false);
        gameParent.SetActive(true);
    }

    public void ToggleMap()
    {
        if (mapParent.activeInHierarchy)
        {
            HideMap();
        }
        else
        {
            ShowMap();
        }
    }
}
