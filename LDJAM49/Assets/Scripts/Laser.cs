using UnityEngine;

public class Laser : MonoBehaviour
{
    [SerializeField] float speed = 10.0f;
    [SerializeField] LayerMask hitLayers;

    void Awake()
    {
        Invoke("Hit", 5.0f);
    }

    void Hit()
    {
        EffectManager.Instance.SpawnSmallExplosion(transform.position);
        CancelInvoke();
        Destroy(gameObject);
    }

    void Update()
    {
        Vector3 newPos = transform.position + transform.forward * speed * Time.deltaTime;
        RaycastHit hit;
        if (Physics.Linecast(transform.position, newPos, out hit, hitLayers))
        {
            if (hit.transform.tag != "Untagged")
            {
                hit.transform.SendMessage("OnHit", 1, SendMessageOptions.DontRequireReceiver);
            }
            Hit();
        }
        transform.position = newPos;
    }
}
