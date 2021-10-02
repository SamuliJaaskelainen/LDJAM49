using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    public static Player Instance;
    public static float turnSpeed = 350.0f;

    [SerializeField] Camera cam;
    [SerializeField] GameObject gameParent;
    [SerializeField] float speed = 10.0f;
    [SerializeField] float turboMultiplier = 2.0f;
    [SerializeField] float spinSpeed = 100.0f;
    [SerializeField] ConfigurableJoint grabJoint;
    [SerializeField] Transform barrel;
    [SerializeField] GameObject laserPrefab;
    [SerializeField] GameObject rocketPrefab;

    CharacterController characterController;
    bool isLockingTargets = false;
    List<GameObject> lockedTargets = new List<GameObject>();

    void Awake()
    {
        Instance = this;
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (!gameParent.activeInHierarchy || Cursor.lockState == CursorLockMode.None)
            return;

        Vector3 movement = Vector3.zero;
        bool isTurbo = Input.GetKey(KeyCode.LeftShift) && !isLockingTargets && grabJoint.connectedBody == null;
        if (Input.GetKey(KeyCode.W))
        {
            movement += transform.forward * (isTurbo ? turboMultiplier : 1.0f);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            movement -= transform.forward;
        }
        if (Input.GetKey(KeyCode.D))
        {
            movement += transform.right;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            movement -= transform.right;
        }
        if (Input.GetKey(KeyCode.Space))
        {
            movement += transform.up;
        }
        else if (Input.GetKey(KeyCode.LeftControl))
        {
            movement -= transform.up;
        }
        CollisionFlags flags = characterController.Move(movement * speed * Time.deltaTime);
        if (flags != CollisionFlags.None)
        {
            CameraShake.Instance.ShakeClamped(0.1f, 0.1f);
        }

        float horizontal = Input.GetAxis("Mouse X") * turnSpeed * Time.deltaTime;
        float vertical = -Input.GetAxis("Mouse Y") * turnSpeed * Time.deltaTime;
        float tilt = 0.0f;
        if (Input.GetKey(KeyCode.Q))
        {
            tilt += 1.0f;
        }
        else if (Input.GetKey(KeyCode.E))
        {
            tilt -= 1.0f;
        }
        transform.Rotate(vertical, horizontal, tilt * spinSpeed * Time.deltaTime);

        if (Input.GetKey(KeyCode.PageUp))
        {
            turnSpeed = Mathf.Clamp(turnSpeed + 1000.0f * Time.deltaTime, 10.0f, 1000.0f);
        }
        else if (Input.GetKey(KeyCode.PageDown))
        {
            turnSpeed = Mathf.Clamp(turnSpeed - 1000.0f * Time.deltaTime, 10.0f, 1000.0f);
        }

        if (Input.GetKey(KeyCode.Home))
        {
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView + 100.0f * Time.deltaTime, 50.0f, 160.0f);
        }
        else if (Input.GetKey(KeyCode.End))
        {
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - 100.0f * Time.deltaTime, 50.0f, 160.0f);
        }

        if (grabJoint.connectedBody)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.F))
            {
                if (grabJoint.connectedBody)
                {
                    Rigidbody grabbedObject = grabJoint.connectedBody;
                    grabJoint.connectedBody = null;
                    if (!Input.GetKey(KeyCode.F))
                    {
                        grabbedObject.AddForce(transform.forward * 40.0f, ForceMode.Impulse);
                    }
                }
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                GameObject.Instantiate(laserPrefab, barrel.position, barrel.rotation);
            }

            if (Input.GetMouseButtonDown(1))
            {
                lockedTargets.Clear();
                isLockingTargets = true;
            }
            else if (Input.GetMouseButtonUp(1) || lockedTargets.Count >= 3)
            {
                isLockingTargets = false;

                if (lockedTargets.Count > 0)
                {
                    for (int i = 0; i < lockedTargets.Count; ++i)
                    {
                        GameObject rocket = GameObject.Instantiate(rocketPrefab, barrel.position, barrel.rotation);
                        rocket.GetComponent<Rocket>().target = lockedTargets[i].transform;
                    }
                    lockedTargets.Clear();
                }
            }

            if (isLockingTargets)
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position, transform.forward, out hit, 20.0f))
                {
                    if (hit.transform.tag == "Enemy")
                    {
                        if (!lockedTargets.Contains(hit.transform.gameObject))
                        {
                            Debug.DrawLine(transform.position, hit.point, Color.green, 1.0f);
                            lockedTargets.Add(hit.transform.gameObject);
                        }
                        else
                        {
                            Debug.DrawLine(transform.position, hit.point, Color.yellow);
                        }
                    }
                    else
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.red);
                    }
                }
                else
                {
                    Debug.DrawLine(transform.position, transform.position + transform.forward * 5.0f, Color.red);
                }
            }

            if (Input.GetKeyDown(KeyCode.F) && !isLockingTargets)
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position, transform.forward, out hit, 5.0f))
                {
                    if (hit.transform.tag == "Phys")
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.green, 1.0f);
                        hit.transform.position = grabJoint.transform.position;
                        grabJoint.connectedBody = hit.transform.GetComponent<Rigidbody>();
                    }
                    else
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.red, 1.0f);
                    }
                }
                else
                {
                    Debug.DrawLine(transform.position, transform.position + transform.forward * 5.0f, Color.red, 1.0f);
                }
            }
        }
    }
}
