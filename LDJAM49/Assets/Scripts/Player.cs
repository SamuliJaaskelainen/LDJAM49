using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] GameObject gameParent;
    [SerializeField] float speed = 10.0f;
    [SerializeField] float turboMultiplier = 2.0f;
    [SerializeField] float turnSpeed = 100.0f;
    [SerializeField] float spinSpeed = 100.0f;
    CharacterController characterController;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (!gameParent.activeInHierarchy)
            return;

        Vector3 movement = Vector3.zero;
        bool isTurbo = Input.GetKey(KeyCode.LeftShift);
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

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            MapManager.Instance.ToggleMap();
        }
    }
}
