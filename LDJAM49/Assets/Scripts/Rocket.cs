using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rocket : MonoBehaviour
{
    public Transform target;

    [SerializeField] float speed = 10.0f;
    [SerializeField] float maxSpeed = 50.0f;
    [SerializeField] float speedIncrease = 10.0f;
    [SerializeField] float homingSpeed = 10.0f;
    [SerializeField] LayerMask hitLayers;

    void Awake()
    {
        Invoke("Hit", 5.0f);
    }

    void Hit()
    {
        CancelInvoke();
        Destroy(gameObject);
    }

    void Update()
    {
        Quaternion targetRotation = Quaternion.LookRotation(target.position - transform.position);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * homingSpeed);

        speed += speedIncrease * Time.deltaTime;
        speed = Mathf.Min(speed, maxSpeed);
        Vector3 newPos = transform.position + transform.forward * speed * Time.deltaTime;
        RaycastHit hit;
        if (Physics.Linecast(transform.position, newPos, out hit, hitLayers))
        {
            if (hit.transform.tag != "Untagged")
            {
                hit.transform.SendMessage("OnHit", SendMessageOptions.DontRequireReceiver);
            }
            Hit();
        }
        transform.position = newPos;
    }
}
