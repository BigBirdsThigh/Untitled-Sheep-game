using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerTest : MonoBehaviour
{
    public float damage = 2f; // Damage dealt per hit
    public float impactForce = 10f; // Max force applied to boids
    public float knockbackRadius = 5f; // How far the knockback affects
    public float knockbackUpwardForce = 5f; // Vertical knock-up force
    public LayerMask boidLayer; // Set this in Unity to only detect boids

    private CharacterController controller;
    private Vector3 velocity;
    public float speed = 5f;
    public float gravity = 9.81f;

    private bool isCharging = false;
    private HashSet<Boid> hitBoids = new HashSet<Boid>();

    private int playerLayer;
    private int boidLayerIndex;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Get player and boid layer indexes
        playerLayer = gameObject.layer;
        boidLayerIndex = LayerMask.NameToLayer("Boid"); // Make sure boids are assigned to this layer
    }

    void Update()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * speed * Time.deltaTime);

        if (!controller.isGrounded)
        {
            velocity.y -= gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
        else
        {
            velocity.y = 0f;
        }

        // Press Space to perform a charge attack
        if (Input.GetKeyDown(KeyCode.Space) && !isCharging)
        {
            StartCoroutine(ChargeAttack());
        }
    }

    IEnumerator ChargeAttack()
    {
        isCharging = true;
        hitBoids.Clear(); // Reset hit boids

        // Disable collisions with boids
        Physics.IgnoreLayerCollision(playerLayer, boidLayerIndex, true);

        float chargeSpeed = speed * 3f;
        float chargeTime = 0.5f;
        float elapsedTime = 0f;

        while (elapsedTime < chargeTime)
        {
            controller.Move(transform.forward * chargeSpeed * Time.deltaTime);
            ApplyKnockback();
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Re-enable collisions with boids after the charge
        Physics.IgnoreLayerCollision(playerLayer, boidLayerIndex, false);

        isCharging = false;
    }

    // ** CALL THESE TO UPGRADE PLAYER STATS **

    
    public void UpgradeDamage()
    {
        damage += 1;
    }

    public void UpgradeRange() // upgrade charge attacks range
    {
        speed += 1;
    }

    // ** END SECTION **

    void ApplyKnockback()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, knockbackRadius, boidLayer);

        foreach (Collider col in hitColliders)
        {
            Boid boid = col.GetComponent<Boid>();
            if (boid != null && !hitBoids.Contains(boid))
            {
                hitBoids.Add(boid);
                boid.TakeDamage(damage);

                Rigidbody boidRb = boid.GetComponent<Rigidbody>();
                if (boidRb != null)
                {
                    Vector3 forceDirection = boid.transform.position - transform.position;
                    forceDirection.y = 0f;
                    forceDirection.Normalize();

                    float distance = Vector3.Distance(transform.position, boid.transform.position);
                    float forceScale = Mathf.Clamp01(1 - (distance / knockbackRadius));

                    Vector3 knockbackForce = (forceDirection * impactForce * forceScale) + (Vector3.up * knockbackUpwardForce);
                    boidRb.AddForce(knockbackForce, ForceMode.VelocityChange);
                }
            }
        }
    }
}