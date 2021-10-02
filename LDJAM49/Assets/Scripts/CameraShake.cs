using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    [Range(0.0f, 1.0f)]
    public float intensity = 0.0f;

    void Awake()
    {
        Instance = this;
    }

    public void Shake(float addIntensity = 1.0f)
    {
        intensity += addIntensity;
    }

    public void ShakeClamped(float addIntensity = 1.0f, float maxIntensity = 10.0f)
    {
        if (intensity < maxIntensity)
        {
            intensity = Mathf.Min(intensity + addIntensity, maxIntensity);
        }

    }

    void Update()
    {
        intensity -= Time.unscaledDeltaTime;
        intensity = Mathf.Clamp01(intensity);

        Quaternion targetRotation = Random.rotationUniform;
        targetRotation = Quaternion.Lerp(Quaternion.identity, targetRotation, intensity);

        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, 10.0f * Time.deltaTime);
    }
}
