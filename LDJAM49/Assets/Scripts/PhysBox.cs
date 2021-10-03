using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysBox : MonoBehaviour
{
    Rigidbody body;

    public bool thrown = false;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void OnHit()
    {
        body.AddForce(Random.onUnitSphere * 10.0f, ForceMode.Impulse);
        body.AddTorque(Random.onUnitSphere * 80.0f, ForceMode.Impulse);
    }

    private void OnCollisionEnter(Collision other)
    {
        // AUDIO: Generic collision

        if (other.transform.tag == "Door" && thrown)
        {
            other.transform.GetComponent<Door>().Open();
        }
        if (other.transform.tag == "Enemy" && thrown)
        {
            other.transform.GetComponent<Enemy>().OnHit(5);
        }
        thrown = false;
    }
}
