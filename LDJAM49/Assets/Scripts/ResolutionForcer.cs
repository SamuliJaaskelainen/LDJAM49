using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResolutionForcer : MonoBehaviour
{
    void LateUpdate()
    {
        if (Screen.width != Screen.height)
        {
            int newSize = Mathf.Max(Screen.width, Screen.height);
            Screen.SetResolution(newSize, newSize, FullScreenMode.Windowed);
        }
    }
}
