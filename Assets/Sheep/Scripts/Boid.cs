using UnityEngine;
using UnityEngine.VFX;
using System.Collections.Generic;
using System.Collections;
using Random=UnityEngine.Random;

public enum BoidState { Roaming, Panicking, Regrouping, Dead }
public class Boid : MonoBehaviour
{
    public Rigidbody rb;
    private Vector3 velocity;    

    [Header("TEST")]
    public MaterialPropertyBlock materialBlock;    

    [Header("Boid Settings")]
    public float speed = 3.4f;
    public float rotationSpeed = 5f;
    public float neighborRadius = 12.69f;
    public VisualEffect deathVFXPrefab; // Reference for death smoke
    public float avoidanceRadius = 1.56f;
    public List<string> avoidanceTags = new List<string> { "Obstacle", "Wall", "Tree" };

    [Header("Weights")]
    public float alignmentWeight = 0.9f;
    public float cohesionWeight = 0.6f;
    public float separationWeight = 3.2f;
    public float targetFollowWeight = 2.5f;
    public float obstacleAvoidanceWeight = 5.5f;
    public BoidState currentState = BoidState.Roaming;
    private float panicTimer = 0f;
    public float panicDuration = 15f; // How long panic lasts
    public float health = 10f; // Basic health system
    public float playerDetectionRange = 9f; // Range to detect player
    private float panicRadius = 7f; // Affects nearby boids when one dies

    private bool isGrounded; // tracks if boid is grounded


    // Interval to limit physics calls for each boid for performance
    private float nextUpdateTime = 0f;
    private float updateInterval = 0.02f;


    [Header("Avoidance Settings")]
    //public float groundCheckDistance = 1.5f;
    public float obstacleCheckDistance = 4f;
    
    [Header("Debugging")]
    public bool debugAvoidance = false;
    public bool debugPlayerDetection = false;
    public bool debugStateChanges = false;

    public Transform target;
    private BoidManager boidManager;
    public Renderer boidRenderer;
    

    public GameObject[] models;

    public float orGoat = 0.5f;
    
    // Set needed constraints and settings just incase they are accidentally tweaked in the editor
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        boidManager = FindObjectOfType<BoidManager>();
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        BecomeSheep(orGoat);             


    }

    void Update()
    {
        if (currentState == BoidState.Dead && !isDying)
        {
            Die();
        }
    }


    void FixedUpdate()
    {
        if (currentState == BoidState.Dead) return; // Stop updates when dead
        if (Time.time < nextUpdateTime) return; // Skip if within interval
        nextUpdateTime = Time.time + Random.Range(0.02f,0.08f); // stagger updates so we don't have each boid calling at once
        Vector3 acceleration = Vector3.zero;

        if (currentState == BoidState.Roaming)
            
            // Skip some calculations on random frames to reduce load
            if (Time.frameCount % 7 == 0)
            {
                acceleration += Roam();
            }        

        else if (currentState == BoidState.Panicking)
            acceleration += Panic();
        else if (currentState == BoidState.Regrouping)
            acceleration += Regroup();
        else if (currentState == BoidState.Dead)
        {
            Die();
            return; // Exit early since it's ded
        }

        Vector3 avoidanceForce = AvoidObstacles() * obstacleAvoidanceWeight;
        if (avoidanceForce != Vector3.zero)
        {
            acceleration = Vector3.Lerp(acceleration, avoidanceForce, 0.8f);
        }

        velocity += acceleration * Time.fixedDeltaTime;
        velocity = velocity.normalized * speed;

        rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);

        if (velocity.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(velocity.x, 0, velocity.z));
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    // Pick one of the boid models at random
    public void BecomeSheep(float orGoat)
    {
        if (models == null || models.Length == 0)
        {
            Debug.LogWarning($"Boid {name}: No models assigned in the inspector!");
            return;
        }

        // Select a random model from the list
        int randomIndex = Random.Range(0, models.Length);
        GameObject selectedModel = models[randomIndex];

        // Instantiate the new model as a child
        GameObject newModel = Instantiate(selectedModel, transform);

        // Ensure proper positioning & rotation
        newModel.transform.localPosition = Vector3.zero;
        newModel.transform.localRotation = Quaternion.identity;

        // Scale the model to be bigger
        newModel.transform.localScale = Vector3.one * 2f;

        // Spawned model needs moved down by half the cube's height
        float cubeHeight = GetComponent<BoxCollider>()?.size.y ?? 1f; // Default to 1 if no collider
        newModel.transform.localPosition -= new Vector3(0, cubeHeight / 2, 0);

        // Get the MeshRenderer of the new model
        boidRenderer = newModel.GetComponent<MeshRenderer>();

        if (boidRenderer != null)
        {
            // Ensure boid has a material instance
            boidRenderer.material = new Material(boidRenderer.material);

            // Set default RedBoost to 0 (ensures no red tint)
            boidRenderer.material.SetFloat("_RedBoost", 0f);
        }
    }

    // Seek towards target position
    Vector3 SeekTarget()
    {
        return (target.position - transform.position).normalized;
    }


    // Align with nearby boids
    Vector3 Align()
    {
        Vector3 avgVelocity = Vector3.zero;
        int count = 0;

        foreach (var boid in boidManager.boids)
        {
            if (boid == this || boid.currentState == BoidState.Dead) continue; // Skip dead boids
            if (Vector3.Distance(transform.position, boid.transform.position) < neighborRadius)
            {
                avgVelocity += boid.rb.velocity;
                count++;
            }
        }

        return count > 0 ? (avgVelocity / count).normalized : Vector3.zero;
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        Debug.Log("Took Damage");

        if (health <= 0)
        {
            ChangeState(BoidState.Dead);
        }
        else
        {
            ChangeState(BoidState.Panicking);
            AlertNearbyBoids();
            StartCoroutine(FlashRedEffect());
        }
    }

    private IEnumerator FlashRedEffect()
    {
        if (boidRenderer == null) yield break;

        Debug.Log("Flashing Red");

        Material mat = boidRenderer.material;
        
        // Increase red boost
        mat.SetFloat("_RedBoost", 1.1f);

        yield return new WaitForSeconds(0.3f); // Flash red for 0.3s

        // Smoothly fade back to normal
        float duration = 0.5f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float newRedBoost = Mathf.Lerp(1.5f, 0f, elapsedTime / duration);
            mat.SetFloat("_RedBoost", newRedBoost);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        mat.SetFloat("_RedBoost", 0f); // Ensure reset
    }

    public void ChangeTarget(Transform new_target){
        target = new_target;
    }


    // **STATE DEFINITIONS**
    Vector3 Roam()
    {
        // Default flocking behavior
        float defaultSeparation = 3.2f;
        float defaultCohesion = 0.6f;
        separationWeight = defaultSeparation;
        cohesionWeight = defaultCohesion;        

        if (PlayerIsNear())
        {
            ChangeState(BoidState.Panicking);
        }

        return Align() * alignmentWeight + Cohere() * cohesionWeight + Separate() * separationWeight + SeekTarget() * targetFollowWeight;
    }

    Vector3 Panic()
    {
        float safeDistance = 10f; // Distance from player where panic starts decreasing
        float minSeparation = 2.7f; // Lowest separation value
        float maxSeparation = 9f; // Highest separation value
        
        obstacleAvoidanceWeight = 11f;

        speed = 6f;

         GameObject panicNode = boidManager.GetCurrPanicNode();

        if (panicNode != null)
        {
            target = panicNode.transform;
        }

        // Distance check for reducing panic effect
        float distanceFromPlayer = Vector3.Distance(transform.position, boidManager.player.position);

        if (distanceFromPlayer > safeDistance)
        {
            panicTimer -= Time.deltaTime * 1.2f; // Slow cooldown when safe
            panicTimer = Mathf.Clamp(panicTimer, 0f, panicDuration); // Prevent negative panic
        }
        else
        {
            panicTimer += Time.deltaTime; // Keep panicking if close to player
        }

        // Gradually reduce separation based on remaining panic time
        float panicFactor = panicTimer / panicDuration; // Ranges from 0 to 1
        separationWeight = Mathf.Lerp(minSeparation, maxSeparation, panicFactor);

        // Exit panic mode when fully calmed down
        if (panicTimer <= 0)
        {
            speed = 3.4f;
            obstacleAvoidanceWeight = 5.5f;
            ChangeState(BoidState.Regrouping);
        }     
        // Move toward panic node while still applying some flocking behaviors
        Vector3 moveToPanicNode = SeekTarget() * 3f;
        return moveToPanicNode + Align() * alignmentWeight + Cohere() * cohesionWeight + Separate() * separationWeight;
    }



    Vector3 Regroup()
    {
        separationWeight = 2f;
        cohesionWeight = 2.5f; // Make them come back together faster
        target = boidManager.transform; // Move toward the herd center - due to how objects with child objects work, the boidmanager will always be in the centremost position of all boid positions

        if (PlayerIsNear())
        {
            ChangeState(BoidState.Panicking);
        }

        return Align() * alignmentWeight + Cohere() * cohesionWeight + Separate() * separationWeight + SeekTarget() * targetFollowWeight;
    }


    private bool isDying = false;
    void Die()
    {

        if (isDying) return;
        isDying = true;
        
        // Stop the boid from moving
        rb.velocity = Vector3.zero;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        // Ensure it's not referenced further
        currentState = BoidState.Dead;        

        // Start the death animation
        StartCoroutine(DeathAnimation());
    }



    // **END STATE DEFINITIONS**



    // Coroutine to handle the death animation
    private IEnumerator DeathAnimation()
    {
        float duration = 0.4f; // Total animation time
        float elapsedTime = 0f;

        Vector3 startPos = transform.position;
        Vector3 controlPoint = startPos + new Vector3(Random.Range(-1f, 1f), 1f, Random.Range(-1f, 1f)); // Random arc
        Vector3 endPos = startPos + new Vector3(Random.Range(-1f, 1f), 2f, Random.Range(-1f, 1f)); // Up + Slight Curve

        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(0, 1800f, 0); // a lot of spins

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            float easedT = Linear(t);  // Linear movement

            transform.position = Vector3.Lerp(startPos, endPos, easedT);
            transform.rotation = Quaternion.Slerp(startRot, endRot, easedT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure the object still exists before instantiating the VFX
        if (this != null)
        {
            VisualEffect smoke = Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
            smoke.Play(); // Start the VFX

            gameObject.SetActive(false);
            Destroy(smoke.gameObject, 3.8f);
            Destroy(gameObject, 3f);
        }

        // Remove boid from the manager before proceeding
        if (boidManager != null)
        {
            boidManager.RemoveBoid(this);
        }
        TimeManager.Instance?.AddTime(10f);
        UIManager.Instance?.AddTimeEffect(10f);
    }


    // ** CURVE FUNCTIONS - TESTING THESE ON DEATH ANIM **

    // Quadratic Bezier Curve function (used to be used in death function)
    private Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float oneMinusT = 1f - t;
        return (oneMinusT * oneMinusT * a) + (2f * oneMinusT * t * b) + (t * t * c);
    }


    float Linear(float x)
    {
        return x; // No easing, moves at a constant rate.
    }


    float PlateauEasing(float x)
    {
        if(x < 1f){
            return 1f - Mathf.Pow(1f -x, 5f); // Faster start, smoother end
        }
        else{
            return 1f; // Plateau
        }
    }

    // ** END CURVE FUNCTIONS **

    void AlertNearbyBoids()
    {
        foreach (Boid boid in boidManager.boids)
        {
            if (boid == this) continue; // Don't alert self

            float distance = Vector3.Distance(transform.position, boid.transform.position);
            if (distance < panicRadius) // If within panic radius, enter panic
            {
                boid.ChangeState(BoidState.Panicking);
            }
        }
    }

    bool PlayerIsNear()
    {
        int rayCount = 9; // Number of rays for vision cone
        float maxAngle = 90f; // Full field of view angle
        float stepAngle = maxAngle / (rayCount - 1); // Angle step per ray
        float detectionRange = playerDetectionRange;
        RaycastHit hit;
        bool playerDetected = false;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = -maxAngle / 2 + (stepAngle * i); // Distribute rays evenly
            Vector3 directionToPlayer = Quaternion.Euler(0, angle, 0) * transform.forward; // Rotate ray

            if (Physics.Raycast(transform.position, directionToPlayer, out hit, detectionRange))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    playerDetected = true;
                }

                // Debug Raycast - Green if player detected, Yellow if blocked
                if (debugPlayerDetection)
                {
                    Color rayColour = (hit.collider.CompareTag("Player")) ? Color.green : Color.yellow;
                    Debug.DrawRay(transform.position, directionToPlayer * detectionRange, rayColour, 0.1f);
                }
            }
            else
            {
                // Debugging: Draw empty space detection in blue
                if (debugPlayerDetection)
                {
                    Debug.DrawRay(transform.position, directionToPlayer * detectionRange, Color.blue, 0.1f);
                }
            }
        }

        return playerDetected;
    }



    // Move towards the center of nearby boids
    Vector3 Cohere()
    {
        Vector3 center = Vector3.zero;
        int count = 0;

        foreach (var boid in boidManager.boids)
        {
            if (boid == this) continue;
            if (Vector3.Distance(transform.position, boid.transform.position) < neighborRadius)
            {
                center += boid.transform.position;
                count++;
            }
        }

        return count > 0 ? ((center / count) - transform.position).normalized : Vector3.zero;
    }


    public void ChangeState(BoidState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        panicTimer = 0f;

        if (newState == BoidState.Panicking)
        {
            GameObject panicNode = boidManager.GetCurrPanicNode();
            if (panicNode != null)
            {
                ChangeTarget(panicNode.transform);
            }
        }
        
        // Debugging visual changes
        if (boidRenderer != null)
        {
            switch (newState)
            {
                case BoidState.Roaming:
                    boidRenderer.material.color = Color.green;
                    break;
                case BoidState.Panicking:
                    boidRenderer.material.color = Color.red;
                    break;
                case BoidState.Regrouping:
                    boidRenderer.material.color = Color.blue;
                    break;
                case BoidState.Dead:
                    boidRenderer.material.color = Color.black;
                    break;
            }
        }
    }




    // Move away from close boids to avoid crowding
    Vector3 Separate()
    {
        Vector3 avoidance = Vector3.zero;
        int count = 0;

        foreach (var boid in boidManager.boids)
        {
            if (boid == this) continue;
            float distance = Vector3.Distance(transform.position, boid.transform.position);
            if (distance < avoidanceRadius)
            {
                avoidance += (transform.position - boid.transform.position).normalized / distance;
                count++;
            }
        }

        return count > 0 ? avoidance.normalized : Vector3.zero;
    }


    Vector3 AvoidObstacles()
    {
        if (Time.frameCount % 5 != 0) return Vector3.zero; // Skip check every few frames

        float checkRadius = obstacleCheckDistance;
        Collider[] hits = Physics.OverlapSphere(transform.position, checkRadius, LayerMask.GetMask("Obstacle"));

        if (hits.Length == 0)
        {
            if (debugAvoidance)
            {
                Debug.DrawRay(transform.position, transform.forward * checkRadius, Color.green, 0.1f);
            }
            return Vector3.zero; // No obstacles detected
        }

        Vector3 avoidanceDir = Vector3.zero;
        int count = 0;

        foreach (Collider hit in hits)
        {
            if (!avoidanceTags.Contains(hit.tag)) continue;

            Vector3 awayFromObstacle = (transform.position - hit.transform.position).normalized;
            avoidanceDir += awayFromObstacle;
            count++;

            if (debugAvoidance)
            {
                Debug.DrawRay(transform.position, awayFromObstacle * checkRadius, Color.red, 0.1f);
            }
        }

        if (count > 0)
        {
            avoidanceDir /= count;
            return avoidanceDir.normalized * 2.5f;
        }

        return Vector3.zero;
    }




}