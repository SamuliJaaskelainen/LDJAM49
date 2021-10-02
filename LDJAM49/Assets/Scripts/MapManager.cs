using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    [SerializeField] GameObject mapParent;
    [SerializeField] Transform mapPlayer;
    [SerializeField] GameObject gameParent;
    float zoomLevel = 1.0f;
    float playerBlinkTimer;
    float playerBlinkDelay = 0.4f;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (mapParent.activeInHierarchy)
        {
            Vector3 movement = Vector3.zero;
            if (Input.GetKey(KeyCode.W))
            {
                movement -= Vector3.forward;
            }
            else if (Input.GetKey(KeyCode.S))
            {
                movement += Vector3.forward;
            }
            if (Input.GetKey(KeyCode.D))
            {
                movement -= Vector3.right;
            }
            else if (Input.GetKey(KeyCode.A))
            {
                movement += Vector3.right;
            }
            if (Input.GetKey(KeyCode.Space))
            {
                movement += Vector3.up;
            }
            else if (Input.GetKey(KeyCode.LeftControl))
            {
                movement -= Vector3.up;
            }
            mapParent.transform.Translate(movement * 10.0f * Time.deltaTime, Space.World);

            float horizontal = Input.GetAxis("Mouse X") * Player.turnSpeed / 4.0f * Time.deltaTime;
            float vertical = -Input.GetAxis("Mouse Y") * Player.turnSpeed / 4.0f * Time.deltaTime;
            mapParent.transform.Rotate(Vector3.up, horizontal, Space.World);
            mapParent.transform.Rotate(Vector3.right, vertical, Space.World);

            float zoom = Input.mouseScrollDelta.y * 0.1f;
            zoomLevel = Mathf.Clamp(zoomLevel + zoom, 0.25f, 3.0f);
            mapParent.transform.localScale = Vector3.one * zoomLevel;

            mapPlayer.localPosition = Player.Instance.transform.position * 0.2f + Vector3.forward * 5.0f;
            mapPlayer.localRotation = Player.Instance.transform.rotation;
            if (playerBlinkTimer < Time.time)
            {
                playerBlinkTimer = Time.time + playerBlinkDelay;
                mapPlayer.gameObject.SetActive(!mapPlayer.gameObject.activeSelf);
            }
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleMap();
        }
    }

    public void ShowMap()
    {
        mapParent.SetActive(true);
        gameParent.SetActive(false);
    }

    public void HideMap()
    {
        mapParent.SetActive(false);
        gameParent.SetActive(true);
    }

    public void ToggleMap()
    {
        if (mapParent.activeInHierarchy)
        {
            HideMap();
        }
        else
        {
            ShowMap();
        }
    }
}
