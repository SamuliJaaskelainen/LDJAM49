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

    public void OnHit(int damage)
    {
        health -= damage;
        if (health <= 0)
        {
            EffectManager.Instance.SpawnExplosion(transform.position);
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (Vector3.Distance(Player.Instance.transform.position, transform.position) < 42.0f)
        {
            Vector3 futurePlayerPos = Player.Instance.transform.position + Player.Instance.characterController.velocity;
            Quaternion targetRotation = Quaternion.LookRotation(futurePlayerPos - transform.position);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * targetingSpeed);

            if (Time.time > shootTimer)
            {
                shootTimer = Time.time + Random.Range(shootRateMin, shootRateMax);
                Instantiate(bulletPrefab, transform.position, transform.rotation);
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
