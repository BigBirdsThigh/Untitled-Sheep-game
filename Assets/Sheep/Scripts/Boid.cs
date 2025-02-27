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
    public GameObject newModel;

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
    public float timeOnKill;

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
        timeOnKill = 10f + UpgradeManager.Instance.extraTimeOnKill; // Base + upgrade
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
        if (currentState == BoidState.Roaming && newModel.layer != LayerMask.NameToLayer("BoidRoam")){            
            newModel.layer = LayerMask.NameToLayer("BoidRoam");
        }
        
        if (Time.time < nextUpdateTime) return; // Skip if within interval
        nextUpdateTime = Time.time + Random.Range(0.02f, 0.08f); // Stagger updates

        Vector3 acceleration = Vector3.zero;

        if (currentState == BoidState.Roaming)
        {
            if (Time.frameCount % 7 == 0)
            {
                acceleration += Roam();
            }
        }
        else if (currentState == BoidState.Panicking)
        {
            acceleration += Panic(); // Ensure Panic updates speed properly
        }
        else if (currentState == BoidState.Regrouping)
        {
            acceleration += Regroup();
        }
        else if (currentState == BoidState.Dead)
        {
            Die();
            return;
        }

        Vector3 avoidanceForce = AvoidObstacles() * obstacleAvoidanceWeight;
        if (avoidanceForce != Vector3.zero)
        {
            acceleration = Vector3.Lerp(acceleration, avoidanceForce, 0.8f);
        }

        // **Ensure the speed update is applied properly**
        velocity += acceleration * Time.fixedDeltaTime;
        velocity = velocity.normalized * speed; // Use the updated speed

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
        newModel = Instantiate(selectedModel, transform);

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

        if(newModel.layer != LayerMask.NameToLayer("BoidRoam")){
            newModel.layer = LayerMask.NameToLayer("BoidRoam");
        }

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
        float minSeparation = 2.7f;
        float maxSeparation = 9f;

        obstacleAvoidanceWeight = 11f;

        // increase speed and rotation speed
        speed = 6f;
        rotationSpeed = 9f;

        GameObject panicNode = boidManager.GetCurrPanicNode();
        if (panicNode != null)
        {
            target = panicNode.transform;
        }

        float distanceFromPlayer = Vector3.Distance(transform.position, boidManager.player.position);

        if (distanceFromPlayer > safeDistance)
        {
            panicTimer -= Time.deltaTime * 1.2f; // Slow cooldown when safe
            panicTimer = Mathf.Clamp(panicTimer, 0f, panicDuration);
        }
        else
        {
            panicTimer += Time.deltaTime;
        }

        float panicFactor = panicTimer / panicDuration;
        separationWeight = Mathf.Lerp(minSeparation, maxSeparation, panicFactor);

        if (panicTimer <= 0)
        {
            speed = 3.4f; // Reset speed when panic ends
            rotationSpeed = 5f;
            obstacleAvoidanceWeight = 5.5f;
            ChangeState(BoidState.Regrouping);
        }

        Vector3 moveToPanicNode = SeekTarget() * 5f;
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
        TimeManager.Instance?.AddTime(timeOnKill);
        UIManager.Instance?.AddTimeEffect(timeOnKill);
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
        int rayCount = 11; // Number of rays for vision cone
        float maxAngle = 110f; // Full field of view angle
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

        if (newState == BoidState.Roaming){
            newModel.layer = LayerMask.NameToLayer("BoidRoam");
        }else{
            newModel.layer = LayerMask.NameToLayer("Boid");
        }

        // Debugging visual changes
        if (boidRenderer != null)
        {
            Material boidMaterial = boidRenderer.material;

            switch (newState)
            {
                case BoidState.Roaming:
                    boidMaterial.color = Color.green;                    
                    break;
                case BoidState.Panicking:
                    boidMaterial.color = Color.red;                    
                    break;
                case BoidState.Regrouping:
                    boidMaterial.color = Color.blue;                    
                    break;
                case BoidState.Dead:
                    boidMaterial.color = Color.black;                    
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
        if (Time.frameCount % 3 != 0) return Vector3.zero; // Skip some frames for performance

        int rayCount = 21; // More rays than PlayerIsNear for better coverage
        float maxAngle = 160f; // Wider field of view for better avoidance
        float stepAngle = maxAngle / (rayCount - 1); // Angle step between rays
        float detectionRange = obstacleCheckDistance; // Distance to check for obstacles

        Vector3 avoidanceDir = Vector3.zero;
        int validHits = 0;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = -maxAngle / 2 + (stepAngle * i); // Spread rays evenly
            Vector3 rayDirection = Quaternion.Euler(0, angle, 0) * transform.forward; // Rotate ray from forward direction
            RaycastHit hit;

            if (Physics.Raycast(transform.position, rayDirection, out hit, detectionRange, LayerMask.GetMask("Obstacle")))
            {
                if (!avoidanceTags.Contains(hit.collider.tag)) continue; // Only avoid specific obstacles

                Vector3 awayFromObstacle = (transform.position - hit.point).normalized; // Move away from hit point
                avoidanceDir += awayFromObstacle; // Accumulate avoidance direction
                validHits++;

                // Debugging: Red if hitting an obstacle
                if (debugAvoidance)
                {
                    Debug.DrawRay(transform.position, rayDirection * hit.distance, Color.red, 0.1f);
                }
            }
            else
            {
                // Debugging: Green if clear path
                if (debugAvoidance)
                {
                    Debug.DrawRay(transform.position, rayDirection * detectionRange, Color.green, 0.1f);
                }
            }
        }

        if (validHits > 0)
        {
            avoidanceDir /= validHits; // Average direction
            return avoidanceDir.normalized * 2.5f; // Adjust strength of avoidance
        }

        return Vector3.zero;
    }


    public void SetHealth(float startHealth){
        this.health = startHealth;
    }




}