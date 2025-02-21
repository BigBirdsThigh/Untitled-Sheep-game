using UnityEngine;
using System.Collections.Generic;


public class Boid : MonoBehaviour
{
    public Rigidbody rb;
    private Vector3 velocity;

    [Header("Boid Settings")]
    public float speed = 3.4f;
    public float rotationSpeed = 5f;
    public float neighborRadius = 12.69f;
    public float avoidanceRadius = 1.56f;
    public List<string> avoidanceTags = new List<string> { "Obstacle", "Wall", "Tree" };

    [Header("Weights")]
    public float alignmentWeight = 0.9f;
    public float cohesionWeight = 0.6f;
    public float separationWeight = 3.2f;
    public float targetFollowWeight = 2.5f;
    public float obstacleAvoidanceWeight = 5.5f;

    [Header("Avoidance Settings")]
    //public float groundCheckDistance = 1.5f;
    public float obstacleCheckDistance = 4f;
    
    [Header("Debugging")]
    public bool debugAvoidance = false; // Enables drawing of raycasts

    public Transform target;
    private BoidManager boidManager;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        boidManager = FindObjectOfType<BoidManager>();
    }

    void FixedUpdate()
    {
        Vector3 acceleration = Vector3.zero;

        // Apply Boid behaviors
        Vector3 alignForce = Align() * alignmentWeight;
        Vector3 cohereForce = Cohere() * cohesionWeight;
        Vector3 separateForce = Separate() * separationWeight;
        Vector3 targetFollowForce = target != null ? SeekTarget() * targetFollowWeight : Vector3.zero;
        Vector3 avoidanceForce = AvoidObstacles() * obstacleAvoidanceWeight;

        // Combine all forces, with avoidance having higher priority when necessary
        acceleration += alignForce + cohereForce + separateForce + targetFollowForce;
        
        if (avoidanceForce != Vector3.zero)
        {
            acceleration = Vector3.Lerp(acceleration, avoidanceForce, 0.8f); // Prioritize avoidance but blend it
        }

        // Apply acceleration to velocity
        velocity += acceleration * Time.fixedDeltaTime;
        velocity = velocity.normalized * speed;

        // Apply velocity to Rigidbody (keeping Y component untouched)
        rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);

        // Rotate only on Y-axis to face movement direction with increased agility
        if (velocity.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(velocity.x, 0, velocity.z));
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, (rotationSpeed + 1.5f) * Time.fixedDeltaTime);
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
            if (boid == this) continue;
            if (Vector3.Distance(transform.position, boid.transform.position) < neighborRadius)
            {
                avgVelocity += boid.rb.velocity;
                count++;
            }
        }

        return count > 0 ? (avgVelocity / count).normalized : Vector3.zero;
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

        int rayCount = 20; // Increased for finer detection
        float maxAngle = 120f; // Wider detection arc
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