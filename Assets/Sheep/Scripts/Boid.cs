using UnityEngine;
using UnityEngine.VFX;
using System.Collections.Generic;
using System.Collections;


public enum BoidState { Roaming, Panicking, Regrouping, Dead }
public class Boid : MonoBehaviour
{
    public Rigidbody rb;
    private Vector3 velocity;    

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
    
    // Set needed constraints and settings just incase they are accidentally tweaked in the editor
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        boidManager = FindObjectOfType<BoidManager>();
        boidRenderer = GetComponent<Renderer>(); // Get the renderer component
        boidRenderer.material.color = Color.green; // Normal state colour
    }

    void FixedUpdate()
    {
        Vector3 acceleration = Vector3.zero;

        if (currentState == BoidState.Roaming)
            acceleration += Roam();
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

        if (health <= 0)
        {
            ChangeState(BoidState.Dead);
        }
        else
        {
            ChangeState(BoidState.Panicking);
            AlertNearbyBoids();
        }
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
        separationWeight = 7f;
        cohesionWeight = 0.2f;

        // Increase panic timer
        panicTimer += Time.deltaTime;

        // Ensure boid stays panicked for a minimum duration before checking to exit
        if (panicTimer > panicDuration && Vector3.Distance(transform.position, boidManager.transform.position) > panicRadius)
        {
            ChangeState(BoidState.Regrouping);
        }

        return Align() * alignmentWeight + Cohere() * cohesionWeight + Separate() * separationWeight;
    }




    Vector3 Regroup()
    {
        separationWeight = 2f;
        cohesionWeight = 1.5f;
        target = boidManager.transform; // Move toward the herd center - due to how objects with child objects work, the boidmanager will always be in the centremost position of all boid positions

        if (PlayerIsNear())
        {
            ChangeState(BoidState.Panicking);
        }

        return Align() * alignmentWeight + Cohere() * cohesionWeight + Separate() * separationWeight + SeekTarget() * targetFollowWeight;
    }

    void Die()
    {
        // Remove boid from the manager before proceeding
        if (boidManager != null)
        {
            boidManager.RemoveBoid(this);
        }

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
        int rayCount = 13; // Number of rays for vision cone
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
                    Color rayColor = (hit.collider.CompareTag("Player")) ? Color.green : Color.yellow;
                    Debug.DrawRay(transform.position, directionToPlayer * detectionRange, rayColor, 0.1f);
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
        if (currentState == newState) return; // Prevent redundant state changes

        currentState = newState;
        panicTimer = 0f; // Reset panic timer when changing states

        // ToDo: Remove this colour changer, this is just for visual testing purposes
        // Change color based on the new state
        if (boidRenderer != null)
        {
            switch (newState)
            {
                case BoidState.Roaming:
                    boidRenderer.material.color = Color.green; // Normal state
                    break;
                case BoidState.Panicking:
                    boidRenderer.material.color = Color.red; // Panic mode
                    break;
                case BoidState.Regrouping:
                    boidRenderer.material.color = Color.blue; // Regrouping state
                    break;
                case BoidState.Dead:
                    boidRenderer.material.color = Color.black; // Dead state
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
        RaycastHit hit;
        Vector3 bestDirection = transform.forward; // Default movement
        bool obstacleDetected = false;
        float maxObstacleDistance = 0f;
        Vector3 clearPathDirection = Vector3.zero;
        bool hasClearPath = false;

        int rayCount = 20; // Number of rays
        float maxAngle = 120f; // Wide detection arc for more accurate obstacle detection
        float stepAngle = maxAngle / (rayCount - 1); // Angle step per ray

        float minDistanceToObstacle = float.MaxValue; // Track closest obstacle

        for (int i = 0; i < rayCount; i++)
        {
            float angle = -maxAngle / 2 + (stepAngle * i);
            Vector3 rayDirection = Quaternion.Euler(0, angle, 0) * transform.forward;            

            if (Physics.Raycast(transform.position, rayDirection, out hit, obstacleCheckDistance))
            {
                // Check if hit object has a tag in the avoidance list
                if (!avoidanceTags.Contains(hit.collider.tag)) continue;

                obstacleDetected = true;

                // Track closest obstacle
                if (hit.distance < minDistanceToObstacle)
                    minDistanceToObstacle = hit.distance;

                // Track the farthest detected object
                if (hit.distance > maxObstacleDistance)
                {
                    maxObstacleDistance = hit.distance;
                    bestDirection = rayDirection;
                }

                if (debugAvoidance)
                    Debug.DrawRay(transform.position, rayDirection * hit.distance, Color.red, 0.1f);
            }
            else
            {
                // Found a clear path, prefer this over avoidance
                hasClearPath = true;
                clearPathDirection = rayDirection;

                if (debugAvoidance)
                    Debug.DrawRay(transform.position, rayDirection * obstacleCheckDistance, Color.green, 0.1f);
            }
        }

        if (obstacleDetected)
        {
            float slowDownFactor = Mathf.Clamp01(minDistanceToObstacle / obstacleCheckDistance);
            float newSpeed = Mathf.Lerp(speed, speed * slowDownFactor, 0.5f); // Smooth slowdown
            speed = Mathf.Max(newSpeed, 0.5f); // Prevent stopping entirely

            if (hasClearPath)
            {
                return clearPathDirection.normalized * 2.0f; // Prefer open space
            }

            // If no clear path, steer along the obstacle (lateral movement)
            Vector3 lateralDirection = Vector3.Cross(Vector3.up, bestDirection).normalized; // Perpendicular movement
            return lateralDirection * 2.5f; // Stronger lateral push
        }

        // Restore speed smoothly if no obstacles detected
        speed = Mathf.Lerp(speed, 2f, 0.5f);

        return Vector3.zero; // No change, follow SeekTarget()
    }




}