using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    public static Player Instance;
    public static float turnSpeed = 350.0f;

    [SerializeField] Camera cam;
    [SerializeField] float fov = 100.0f;
    [SerializeField] GameObject gameParent;
    [SerializeField] float speed = 10.0f;
    [SerializeField] float turboMultiplier = 2.0f;
    [SerializeField] float spinSpeed = 100.0f;
    [SerializeField] int health = 10;
    [SerializeField] ConfigurableJoint grabJoint;
    [SerializeField] Transform barrel;
    [SerializeField] GameObject laserPrefab;
    [SerializeField] GameObject rocketPrefab;

    public CharacterController characterController;
    bool isLockingTargets = false;
    List<GameObject> lockedTargets = new List<GameObject>();
    float laserTimer;
    float laserRate = 0.33f;
    float rocketTimer;
    float rocketRate = 0.9f;
    Vector3 lastCheckpoint;
    float hpTimer;
    float hpRechargeRate = 1.0f;
    float hpRechargeDelay = 5.0f;
    bool firstDeath = true;
    bool firstMap = true;
    bool firstDamage = true;
    bool firstHeal = true;

    [SerializeField] bool isTurboUnlocked = false;
    [SerializeField] bool isGrabUnlocked = false;
    [SerializeField] bool isRocketUnlocked = false;

    [Header("Menu stuff")]
    [SerializeField] GameObject lockedMenu;
    [SerializeField] GameObject unlockedMenu;
    [SerializeField] GameObject rocketKeyMenu;
    [SerializeField] GameObject grabKeyMenu;
    [SerializeField] GameObject turboKeyMenu;
    [SerializeField] TMPro.TMP_Text hpText;
    [SerializeField] TMPro.TMP_Text missionText;
    [SerializeField] TMPro.TMP_Text hintText;

    void Awake()
    {
        Instance = this;
        characterController = GetComponent<CharacterController>();
        lastCheckpoint = transform.position;
    }

    void Update()
    {
        lockedMenu.SetActive(Cursor.lockState != CursorLockMode.None);
        unlockedMenu.SetActive(Cursor.lockState == CursorLockMode.None);

        if (!gameParent.activeInHierarchy || Cursor.lockState == CursorLockMode.None)
            return;

        Vector3 movement = Vector3.zero;
        bool isTurbo = Input.GetKey(KeyCode.LeftShift) && !isLockingTargets && grabJoint.connectedBody == null && isTurboUnlocked;
        movement += transform.forward * (isTurbo ? turboMultiplier : 0.0f);
        if (Input.GetKeyDown(KeyCode.LeftShift) && isTurbo)
        {
            // AUDIO: Turbo boost start
            CameraShake.Instance.Shake(0.3f);
        }
        if (Input.GetKey(KeyCode.W) && !isTurbo)
        {
            // AUDIO: Turbo boost loop
            movement += transform.forward;
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

        if (Input.GetKey(KeyCode.F1))
        {
            turnSpeed = Mathf.Clamp(turnSpeed + 1000.0f * Time.deltaTime, 10.0f, 1000.0f);
        }
        else if (Input.GetKey(KeyCode.F2))
        {
            turnSpeed = Mathf.Clamp(turnSpeed - 1000.0f * Time.deltaTime, 10.0f, 1000.0f);
        }

        if (Input.GetKey(KeyCode.F3))
        {
            fov = Mathf.Clamp(fov + 100.0f * Time.deltaTime, 50.0f, 160.0f);
        }
        else if (Input.GetKey(KeyCode.F4))
        {
            fov = Mathf.Clamp(fov - 100.0f * Time.deltaTime, 50.0f, 160.0f);
        }
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, isTurbo ? fov * 1.2f : fov, Time.deltaTime * 10.0f);

        if (grabJoint.connectedBody)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.F))
            {
                if (grabJoint.connectedBody)
                {
                    Rigidbody grabbedObject = grabJoint.connectedBody;
                    if (!Input.GetKey(KeyCode.F))
                    {
                        // AUDIO: Grab throw
                        CameraShake.Instance.Shake(0.3f);
                        grabJoint.connectedBody.transform.GetComponent<PhysBox>().thrown = true;
                        grabbedObject.AddForce(transform.forward * 40.0f, ForceMode.Impulse);
                    }
                    else
                    {
                        // AUDIO: Grab drop
                    }
                    grabJoint.connectedBody = null;
                }
            }
        }
        else
        {
            if (Input.GetMouseButton(0) && Time.time > laserTimer)
            {
                // AUDIO: Shoot laser
                laserTimer = Time.time + laserRate;
                GameObject.Instantiate(laserPrefab, barrel.position, barrel.rotation);
            }

            if (Input.GetMouseButtonDown(1) && Time.time > rocketTimer && isRocketUnlocked)
            {
                // AUDIO: Start locking
                rocketTimer = Time.time + rocketRate;
                lockedTargets.Clear();
                isLockingTargets = true;
            }
            else if ((Input.GetMouseButtonUp(1) || lockedTargets.Count >= 3) && isLockingTargets)
            {
                isLockingTargets = false;

                // AUDIO: Shoot rocket
                if (lockedTargets.Count > 0)
                {
                    for (int i = 0; i < lockedTargets.Count; ++i)
                    {
                        GameObject rocket = GameObject.Instantiate(rocketPrefab, barrel.position, barrel.rotation);
                        if (lockedTargets[i])
                        {
                            rocket.GetComponent<Rocket>().target = lockedTargets[i].transform;
                        }
                    }
                    lockedTargets.Clear();
                }
                else
                {
                    GameObject rocket = GameObject.Instantiate(rocketPrefab, barrel.position, barrel.rotation);
                }
            }

            if (isLockingTargets)
            {
                RaycastHit hit;
                if (Physics.SphereCast(transform.position, 0.2f, transform.forward, out hit, cam.farClipPlane))
                {
                    if (hit.transform.tag == "Enemy")
                    {
                        if (!lockedTargets.Contains(hit.transform.gameObject))
                        {
                            // AUDIO: Lock target
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

            if (Input.GetKeyDown(KeyCode.F) && !isLockingTargets && isGrabUnlocked)
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position, transform.forward, out hit, 5.0f))
                {
                    if (hit.transform.tag == "Phys")
                    {
                        // AUDIO: Grab start
                        Debug.DrawLine(transform.position, hit.point, Color.green, 1.0f);
                        hit.transform.position = grabJoint.transform.position;
                        grabJoint.connectedBody = hit.transform.GetComponent<Rigidbody>();
                    }
                    else
                    {
                        // AUDIO: Grab miss
                        Debug.DrawLine(transform.position, hit.point, Color.red, 1.0f);
                    }
                }
                else
                {
                    // AUDIO: Grab miss
                    Debug.DrawLine(transform.position, transform.position + transform.forward * 5.0f, Color.red, 1.0f);
                }
            }

            if (grabJoint.connectedBody)
            {
                // AUDIO: Grab loop
            }
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            TakeDamage(1);
        }

        if (Time.time > hpTimer && health < 10)
        {
            // AUDIO: Heal
            hpTimer = Time.time + hpRechargeRate;
            health++;
            WireframeRenderer.Instance.randomOffset = Mathf.Floor((float)(10 - health)) / 20.0f;
            hpText.text = "Health: ";
            for (int i = 0; i < health; ++i)
            {
                hpText.text += "|";
            }
            if (firstHeal)
            {
                hintText.text = "Hint: You will automatically heal after 10 seconds of safety";
                firstHeal = false;
            }
        }

        if (firstMap && Input.GetKeyDown(KeyCode.Tab))
        {
            hintText.text = "Hint: You are shown in the map as blinking point";
            firstMap = false;
        }
    }

    public void OnHit(int damage)
    {
        TakeDamage(damage);
    }

    public void TakeDamage(int damage)
    {
        if (firstDamage)
        {
            hintText.text = "Hint: Game rendering becomes more unstable the more damaged you are";
            firstDamage = false;
        }

        // AUDIO: Player take damage
        Debug.Log("Take damage! " + damage);
        CameraShake.Instance.Shake(0.5f);
        hpTimer = Time.time + hpRechargeDelay;
        health -= damage;
        WireframeRenderer.Instance.randomOffset = Mathf.Floor((float)(10 - health)) / 20.0f;
        hpText.text = "Health: ";
        for (int i = 0; i < health; ++i)
        {
            hpText.text += "|";
        }

        if (health <= 0)
        {
            Respawn();
        }
    }

    public void Respawn()
    {
        // AUDIO: Player respawn
        Debug.Log("Respawn");
        if (firstDeath)
        {
            hintText.text = "Hint: When you die, you do not lose progress";
            firstDeath = false;
        }
        transform.position = lastCheckpoint;
        health = 10;
        hpText.text = "Health: ";
        for (int i = 0; i < health; ++i)
        {
            hpText.text += "|";
        }
        WireframeRenderer.Instance.randomOffset = 0.0f;
        grabJoint.connectedBody = null;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform.tag == "PickUp")
        {
            if (other.transform.name == "Turbo")
            {
                // AUDIO: Pickup turbo
                lastCheckpoint = transform.position;
                isTurboUnlocked = true;
                turboKeyMenu.SetActive(true);
                hintText.text = "Hint: Hold SHIFT for turbo speed";
                missionText.text = "Next objective: Find tractor beam";
                Debug.Log("Turbo acquired!");
            }
            else if (other.transform.name == "Grab")
            {
                // AUDIO: Pickup grab
                lastCheckpoint = transform.position;
                isGrabUnlocked = true;
                grabKeyMenu.SetActive(true);
                hintText.text = "Hint: Throwing boxes does high damage and breaks doors";
                missionText.text = "Next objective: Find rockets";
                Debug.Log("Grab acquired!");
            }
            else if (other.transform.name == "Rocket")
            {
                // AUDIO: Pickup rocket
                lastCheckpoint = transform.position;
                isRocketUnlocked = true;
                rocketKeyMenu.SetActive(true);
                hintText.text = "Hint: Hold R. MOUSE to lock up to three targets with rockets";
                missionText.text = "Next objective: Kill final boss";
                Debug.Log("Rocket acquired!");
            }
            else if (other.transform.name == "HubTrigger")
            {
                missionText.text = "Next objective: Find turbo booster";
                hintText.text = "Hint: New equipment will unlock new actions";
            }
            Destroy(other.gameObject);
        }
    }
}
