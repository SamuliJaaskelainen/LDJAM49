using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActivateOnDestory : MonoBehaviour
{
    public List<GameObject> activateObjets = new List<GameObject>();

    void OnDestroy()
    {
        foreach (GameObject go in activateObjets)
        {
            if (go)
            {
                go.SetActive(true);
            }
        }
    }
}
