using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] int health = 5;
    [SerializeField] float targetingSpeed = 20.0f;
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] bool isMoving;
    [SerializeField] CharacterController characterController;

    float shootTimer;
    [SerializeField] float shootRateMin = 2.0f;
    [SerializeField] float shootRateMax = 3.0f;

    float moveTimer;
    [SerializeField] float moveRateMin = 2.0f;
    [SerializeField] float moveRateMax = 3.0f;
    Vector3 moveDir = Vector3.zero;
    float moveSpeed;
    [SerializeField] float moveSpeedMin = 2.0f;
    [SerializeField] float moveSpeedMax = 3.0f;
    [SerializeField] bool isBoss = false;

    Transform gameParent;

    void Awake()
    {
        gameParent = GameObject.FindGameObjectWithTag("Game").transform;
    }

    public void OnHit(int damage)
    {
        health -= damage;
        if (health <= 0)
        {
            if (isBoss)
            {
                EffectManager.Instance.SpawnLargeExplosion(transform.position);
            }
            else
            {
                EffectManager.Instance.SpawnExplosion(transform.position);
            }
            Destroy(gameObject);
        }
        else
        {
            // AUDIO: Enemy takes damage
        }
    }

    private void Update()
    {
        if (Vector3.Distance(Player.Instance.transform.position, transform.position) <= Player.Instance.cam.farClipPlane)
        {
            Vector3 futurePlayerPos = Player.Instance.transform.position + Player.Instance.characterController.velocity;
            Quaternion targetRotation = Quaternion.LookRotation(futurePlayerPos - transform.position);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * targetingSpeed);

            if (Time.time > shootTimer)
            {
                // AUDIO: Enemy shoot laser
                shootTimer = Time.time + Random.Range(shootRateMin, shootRateMax);
                Instantiate(bulletPrefab, transform.position, targetRotation, gameParent);
                if (isBoss)
                {
                    Instantiate(bulletPrefab, transform.position + transform.right * 0.75f, transform.rotation, gameParent);
                }
            }

            if (isMoving)
            {
                if (Time.time > moveTimer)
                {
                    moveTimer = Time.time + Random.Range(moveRateMin, moveRateMax);
                    moveDir = Random.onUnitSphere;
                    moveSpeed = Random.Range(moveSpeedMin, moveSpeedMax);
                }
                characterController.Move(moveDir * moveSpeed * Time.deltaTime);
            }
        }
    }
}
