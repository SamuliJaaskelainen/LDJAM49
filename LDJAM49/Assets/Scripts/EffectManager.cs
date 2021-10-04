using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    [SerializeField] Transform gameParent;
    [SerializeField] GameObject smallExplosion;
    [SerializeField] GameObject explosion;
    [SerializeField] GameObject largeExplosion;

    void Awake()
    {
        Instance = this;
    }

    public void SpawnExplosion(Vector3 pos)
    {
        // AUDIO: Enemy dies
        Instantiate(explosion, pos, Quaternion.identity, gameParent);
    }

    public void SpawnLargeExplosion(Vector3 pos)
    {
        // AUDIO: Enemy dies
        Instantiate(largeExplosion, pos, Quaternion.identity, gameParent);
    }

    public void SpawnSmallExplosion(Vector3 pos)
    {
        // AUDIO: Laser hits wall
        Instantiate(smallExplosion, pos, Quaternion.identity, gameParent);
    }
}
