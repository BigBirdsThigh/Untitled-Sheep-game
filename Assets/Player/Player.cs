using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerTest : MonoBehaviour
{    

    [Header("Animation")]
    public Animator animator;

    [Header("Charge Stats")]
    public float chargeDamage = 5f; // Damage dealt per hit
    public float impactForce = 10f; // Max force applied to boids
    public float knockbackRadius = 5f; // How far the knockback affects
    public float knockbackUpwardForce = 5f; // Vertical knock-up force
    public float chargeCoolDown = 4.5f;
    private bool canCharge = true;
    public LayerMask boidLayer; // Layer to detect boids


    private float biteTimer = 0f;
    private float chargeTimer = 0f;



    [Header("Bite Stats")]
    public float biteRadius = 2.5f; // Radius of bite attack
    public float biteDamage = 1f;   // Unique damage for bite attack
    public float biteCoolDown = 0.7f; // Cooldown to prevent spam
    private bool canBite = true;
    public Transform bitePoint; // the point where bite originates

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
        animator = GetComponent<Animator>();

        // Get player and boid layer indexes
        playerLayer = gameObject.layer;
        boidLayerIndex = LayerMask.NameToLayer("Boid"); // Make sure boids are assigned to this layer
        
    }

    public void ApplyUpgrades(){
        biteDamage += UpgradeManager.Instance.biteDmgBonus;
        chargeDamage += UpgradeManager.Instance.chargeDmgBonus;
        knockbackRadius += UpgradeManager.Instance.chargeRadiusBonus;
        chargeCoolDown = GetChargeCoolDown();
    }

    void Update()
    {

        if (GameManager.Instance != null && GameManager.Instance.IsGamePaused()) return; // Prevent rotation when paused

        // Handle Mouse Locking
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;  // Unlock cursor
            Cursor.visible = true;  // Make cursor visible
        }
        else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) 
        {
            Cursor.lockState = CursorLockMode.Locked; // Lock cursor to center
            Cursor.visible = false;  // Hide cursor
        }

        // Update cooldown timers
        if (biteTimer > 0) biteTimer -= Time.deltaTime;
        if (chargeTimer > 0) chargeTimer -= Time.deltaTime;

        UIManager.Instance?.UpdateCooldownUI(biteCoolDown, biteTimer, chargeCoolDown, chargeTimer);

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

        if (Cursor.lockState == CursorLockMode.Locked) // Only rotate if cursor is locked
        {
            float mouseX = Input.GetAxis("Mouse X") * 1.2f; // * is the sens
            transform.Rotate(0, mouseX, 0);
        }

        float speedValue = move.magnitude; // Movement speed value
        animator.SetFloat("Speed", move.magnitude);

        // Press Space to perform a charge attack
        if (Input.GetKeyDown(KeyCode.Space) && !isCharging && canCharge)
        {
            StartCoroutine(ChargeAttack());
        }

        if (Input.GetMouseButtonDown(0) && canBite) // Left Click for bite
        {
            StartCoroutine(BiteAttack());
        }

    }


    IEnumerator BiteAttack()
    {
        canBite = false;

        biteTimer = biteCoolDown;

        // plays bite anim
        animator.SetTrigger("Bite");

        // Detect boids in bite radius
        Collider[] hitBoids = Physics.OverlapSphere(bitePoint.position, biteRadius, boidLayer);

        foreach (Collider hit in hitBoids)
        {
            Boid boid = hit.GetComponent<Boid>();
            if (boid != null)
            {
                boid.TakeDamage(biteDamage);
            }
        }

        Debug.Log($"Bite Attack! Hit {hitBoids.Length} boids.");
 

        yield return new WaitForSeconds(biteCoolDown);
        canBite = true;
    }

    void OnDrawGizmos()
    {
        // Visualize bite radius in editor
        Gizmos.color = Color.red;
        if (bitePoint != null)
            Gizmos.DrawWireSphere(bitePoint.position, biteRadius);
    }



    IEnumerator ChargeAttack()
    {
        isCharging = true;
        canCharge = false;
        hitBoids.Clear(); // Reset hit boids
        chargeTimer = chargeCoolDown; // Start cooldown timer
        animator.SetTrigger("Charge");

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

        // cooldown
        yield return new WaitForSeconds(chargeCoolDown);
        canCharge = true;
    }


    void ApplyKnockback()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, knockbackRadius, boidLayer);

        foreach (Collider col in hitColliders)
        {
            Boid boid = col.GetComponent<Boid>();
            if (boid != null && !hitBoids.Contains(boid))
            {
                hitBoids.Add(boid);
                boid.TakeDamage(chargeDamage);

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


    float GetChargeCoolDown(){
        float reductionFactor = 1f - (UpgradeManager.Instance?.chargeCoolDownBonus / 100f) ?? 0f;
        float newCoolDown = chargeCoolDown * reductionFactor;

        return Mathf.Clamp(newCoolDown, 2f, chargeCoolDown); // Prevent this going below 2s
    }

}