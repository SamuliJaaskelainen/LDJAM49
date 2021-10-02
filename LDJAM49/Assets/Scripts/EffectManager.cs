using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    [SerializeField] Transform gameParent;
    [SerializeField] GameObject smallExplosion;
    [SerializeField] GameObject explosion;

    void Awake()
    {
        Instance = this;
    }

    public void SpawnExplosion(Vector3 pos)
    {
        Instantiate(explosion, pos, Quaternion.identity, gameParent);
    }

    public void SpawnSmallExplosion(Vector3 pos)
    {
        Instantiate(smallExplosion, pos, Quaternion.identity, gameParent);
    }
}
