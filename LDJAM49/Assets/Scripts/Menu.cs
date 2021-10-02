using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public static bool useEmulator = false;

    public void UseEmulator()
    {
        useEmulator = true;
        SceneManager.LoadScene("Main");
    }

    public void UseScope()
    {
        useEmulator = false;
        SceneManager.LoadScene("Main");
    }
}
