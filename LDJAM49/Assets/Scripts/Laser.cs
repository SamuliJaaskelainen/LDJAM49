using UnityEngine;

public class Laser : MonoBehaviour
{
    [SerializeField] float radius = 0.1f;
    [SerializeField] int damage = 1;
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
        float distance = speed * Time.deltaTime;
        Vector3 newPos = transform.position + transform.forward * distance;
        RaycastHit hit;
        if (Physics.CapsuleCast(transform.position, newPos, radius, transform.forward, out hit, distance, hitLayers))
        {
            if (hit.transform.tag != "Untagged")
            {
                hit.transform.SendMessage("OnHit", damage, SendMessageOptions.DontRequireReceiver);
            }
            Hit();
        }
        transform.position = newPos;
    }
}
