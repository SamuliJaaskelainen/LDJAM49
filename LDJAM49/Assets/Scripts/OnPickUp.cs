using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnPickUp : MonoBehaviour
{
    public GameObject target;
    public string functionName;

    void OnDestroy()
    {
        if (target)
        {
            target.SendMessage(functionName);
        }
    }
}
