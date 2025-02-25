using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class BoidManager : MonoBehaviour
{
    public Boid boidPrefab;
    public int boidCount = 20;
    public float spawnRadius = 5f;
    public Transform target;
    public Transform player; // Player reference for Panic Node calculations
    public float debugPercentage = 0.25f; // 25% of boids will have debug
    public LayerMask obstacleLayer; // Layer to detect obstacles for panic node scoring

    [Header("Debugging")]
    public bool DebugPlayerDetection = false;
    public bool DebugObstacleAvoidance = false;

    [HideInInspector]
    public List<Boid> boids = new List<Boid>();
    private List<GameObject> nodes = new List<GameObject>(); // Store all nodes as graph
    private GameObject roamNode; // Best node for roaming
    private GameObject panicNode; // Best node for panicking

    private bool gameOverTriggered = false; // Game does not initialise as over


    public static BoidManager Instance { get; private set; } // Singleton Instance


    void Awake()
    {
        if (Instance == null){
            Instance = this;
        }else{
            Destroy(gameObject);
        }
    }

    void Start()
    {
        target = this.transform;
        gameOverTriggered = false;
        CacheNodes(); // Get all nodes from the scene
        ResetRound(boidCount);
        //UpdateBestNodes();
    }

    void FixedUpdate()
    {
        boids.RemoveAll(b => b == null); // Remove destroyed boids

        int regroupingBoids = GetRegroupingCount();
        int totalBoids = boids.Count;

        Debug.Log($"Total Boids: {totalBoids}, gameOverTriggered: {gameOverTriggered}");

        // Win condition - prevent triggering if game is already resetting
        if (totalBoids == 0 && !gameOverTriggered)
        {
            gameOverTriggered = true; // Prevent multiple triggers
            StartCoroutine(CheckForWinAfterDelay()); // Delay before final check
            return;
        }

        if (regroupingBoids >= totalBoids * 0.7f)
        {
            foreach (Boid boid in boids)
            {
                if (boid != null)
                {
                    boid.ChangeState(BoidState.Roaming);
                    boid.ChangeTarget(roamNode.transform);
                }
            }

            RecalculateRoamNode(); // Update roam node
        }
    }

    private IEnumerator CheckForWinAfterDelay()
    {
        yield return new WaitForSeconds(2.5f);
        Debug.Log("Checking for win after delay...");
        CheckForWin();
    }


    public void ResetRound(int numBoids)
    {
        gameOverTriggered = false;

        // Find a good spawn using Best First Search
        GameObject newStartNode = FindGoodSpawn();
        if (newStartNode != null)
        {
            transform.position = newStartNode.transform.position;
        }

        StartCoroutine(DelayedSpawn(numBoids)); // Short delay before spawning

        UpdateBestNodes(); // Reset node states
    }

    private IEnumerator DelayedSpawn(int numBoids)
    {
        yield return new WaitForSeconds(0.5f); // Small delay for positioning
        SpawnBoids(numBoids);
    }
        

    // Best First Search with depth limit
    GameObject FindGoodSpawn()
    {
        if (nodes.Count == 0) return null;

        Queue<GameObject> openSet = new Queue<GameObject>();
        HashSet<GameObject> visited = new HashSet<GameObject>();

        GameObject startNode = nodes[Random.Range(0, nodes.Count)];
        openSet.Enqueue(startNode);
        visited.Add(startNode);

        GameObject bestNode = startNode;
        float bestScore = EvaluateNode(startNode);

        int maxDepth = 3; // Limit search depth
        int currentDepth = 0;
        int nodesAtCurrentDepth = openSet.Count;

        while (openSet.Count > 0 && currentDepth < maxDepth)
        {
            GameObject currentNode = openSet.Dequeue();
            float score = EvaluateNode(currentNode);

            if (score > bestScore)
            {
                bestScore = score;
                bestNode = currentNode;
            }

            // Expand neighbors
            foreach (GameObject neighbor in nodes)
            {
                if (!visited.Contains(neighbor) &&
                    Vector3.Distance(currentNode.transform.position, neighbor.transform.position) < 7f) // Slightly increased threshold
                {
                    openSet.Enqueue(neighbor);
                    visited.Add(neighbor);
                }
            }

            // Track depth
            nodesAtCurrentDepth--;
            if (nodesAtCurrentDepth == 0)
            {
                currentDepth++;
                nodesAtCurrentDepth = openSet.Count;
            }
        }

        return bestNode;
    }



    // evaluate node quality for new spawn
    float EvaluateNode(GameObject node)
    {
        float distanceToPlayer = Vector3.Distance(node.transform.position, player.position);
        float obstaclePenalty = GetObstacleScore(node.transform.position);
        return distanceToPlayer - obstaclePenalty; // Higher is better
    }

    private void CheckForWin()
    {
        if (boids.Count == 0) // Ensure no boids revived during delay
        {
            Debug.Log("Win condition met! Triggering win...");
            GameManager.Instance?.TriggerWin();
            gameOverTriggered = false; // Reset for the next round
        }
        else
        {
            Debug.Log("Win check failed: Boids exist. Resetting gameOverTriggered.");
            gameOverTriggered = false; // Reset since new boids spawned
        }
    }

    

    void UpdateBestPanicNode()
    {
        if (nodes.Count == 0) return;

        GameObject previousPanicNode = panicNode;
        panicNode = GetBestPanicNode();

        // Reset previous panic node to white if it's no longer selected
        if (previousPanicNode != null && previousPanicNode != panicNode)
        {
            SetNodeColour(previousPanicNode, Color.white);
        }

        if (panicNode != null)
        {
            SetNodeColour(panicNode, Color.red);
        }
    }


    void CacheNodes()
    {
        GameObject nodesParent = GameObject.Find("Nodes");
        if (nodesParent != null)
        {
            nodes = new List<GameObject>();
            foreach (Transform child in nodesParent.transform)
            {
                nodes.Add(child.gameObject);
            }
        }
    }

    void UpdateBestNodes()
    {
        if (nodes.Count == 0) return;

        // Cache previous nodes to reset them later
        GameObject previousRoamNode = roamNode;
        GameObject previousPanicNode = panicNode;

        roamNode = GetBestRoamNode();
        panicNode = GetBestPanicNode();

        // Reset previous nodes to white (if they exist and are not the new selected ones)
        if (previousRoamNode != null && previousRoamNode != roamNode)
        {
            SetNodeColour(previousRoamNode, Color.white);
        }
        if (previousPanicNode != null && previousPanicNode != panicNode)
        {
            SetNodeColour(previousPanicNode, Color.white);
        }

        // Apply new colours
        if (roamNode != null)
        {
            SetNodeColour(roamNode, Color.green);
        }
        if (panicNode != null)
        {
            SetNodeColour(panicNode, Color.red);
        }
    }


    bool BoidReachedNode(GameObject node)
    {
        if (node == null) return false;

        foreach (Boid boid in boids)
        {
            if (Vector3.Distance(boid.transform.position, node.transform.position) < 1f) // 1m threshold
            {
                return true;
            }
        }
        return false;
    }


    bool PlayerReachedNode(GameObject node)
    {
        if (node == null || player == null) return false;
        
        return Vector3.Distance(player.position, node.transform.position) < 1f; // 1m threshold
    }



    void RecalculateRoamNode()
    {
        if (nodes.Count == 0) return;

        GameObject previousRoamNode = roamNode;
        roamNode = GetBestRoamNode();

        // Reset previous node if different
        if (previousRoamNode != null && previousRoamNode != roamNode)
        {
            SetNodeColour(previousRoamNode, Color.white);
        }

        if (roamNode != null)
        {
            SetNodeColour(roamNode, Color.green);
        }

        // Assign the new roam node as the target for boids in Roaming state
        foreach (Boid boid in boids)
        {
            if (boid.currentState == BoidState.Roaming)
            {
                boid.ChangeTarget(roamNode.transform);
            }
        }
    }


    GameObject GetBestRoamNode()
    {
        // Placeholder: Select a random node for now
        return nodes.OrderBy(n => Random.value).FirstOrDefault();
    }

    public GameObject GetCurrPanicNode(){
        return panicNode;
    }

    GameObject GetBestPanicNode()
    {
        if (player == null) return null;

        return nodes.OrderByDescending(n =>
        {
            float distance = Vector3.Distance(n.transform.position, player.position); // reward distance from player
            float obstaclePenalty = GetObstacleScore(n.transform.position); // penalise for having lots of obstacles inbetween point and boids (harder to navigate)
            float losBonus = GetLOSScore(n.transform.position); // Bonus if hidden from player

            return distance - obstaclePenalty + losBonus; // Higher value = better panic node
        }).FirstOrDefault();
    }


    float GetLOSScore(Vector3 nodePos)
    {
        Vector3 directionToPlayer = (player.position - nodePos).normalized;
        RaycastHit hit;

        // Ignore Y-axis to keep LOS on the same horizontal level
        Vector3 nodePositionFlat = new Vector3(nodePos.x, player.position.y, nodePos.z);
        Vector3 playerPositionFlat = new Vector3(player.position.x, player.position.y, player.position.z);

        if (Physics.Raycast(nodePositionFlat, directionToPlayer, out hit, Vector3.Distance(nodePositionFlat, playerPositionFlat)))
        {
            if (hit.collider.CompareTag("Player"))
            {
                // If there's LOS to the player, reduce score
                return -5f; // Penalize nodes that can be seen
            }
        }

        // If no direct LOS, increase score
        return 5f; // Reward nodes that are hidden
    }



    float GetObstacleScore(Vector3 nodePos)
    {
        float maxDistance = Vector3.Distance(nodePos, transform.position);
        int obstacleCount = 0;

        Vector3 direction = (transform.position - nodePos).normalized;
        float stepDistance = 1f; // Check every 1 unit along the path

        for (float d = 0; d < maxDistance; d += stepDistance)
        {
            Vector3 checkPos = nodePos + (direction * d);
            if (Physics.Raycast(checkPos, Vector3.down, 2f, obstacleLayer)) // Check ground collisions
            {
                obstacleCount++;
            }
        }

        return obstacleCount * 2f; // Each obstacle reduces the score
    }

    void SetNodeColour(GameObject node, Color color)
{
    if (node == null)
    {
        //Debug.LogWarning("SetNodeColour: Attempted to color a null node.");
        return;
    }

    Renderer nodeRenderer = node.GetComponent<MeshRenderer>();

    if (nodeRenderer == null)
    {
        //Debug.LogWarning($"SetNodeColour: Node '{node.name}' at {node.transform.position} has no MeshRenderer.");
        return;
    }

    if (nodeRenderer.material == null)
    {
        //Debug.LogWarning($"SetNodeColour: Node '{node.name}' at {node.transform.position} has no Material assigned.");
        return;
    }

    //Debug.Log($"SetNodeColour: Changing color of '{node.name}' at {node.transform.position} to {color}");

    // Apply color change
    nodeRenderer.material.color = color;
}



    public int GetRegroupingCount()
    {
        return boids.FindAll(b => b.currentState == BoidState.Regrouping).Count;
    }

    public int GetRegroupingBoidsNearManager()
    {
        float regroupingRadius = 5f;
        return boids.FindAll(b => b.currentState == BoidState.Regrouping &&
                                  Vector3.Distance(b.transform.position, transform.position) <= regroupingRadius).Count;
    }

    public void SpawnBoids(int boidCount)
    {
        for (int i = 0; i < boidCount; i++)
        {
            Vector3 spawnPos = transform.position + Random.insideUnitSphere * spawnRadius;
            spawnPos.y = 1f;
            Boid newBoid = Instantiate(boidPrefab, spawnPos, Quaternion.identity, transform);
            newBoid.target = target;
            boids.Add(newBoid);
        }

        if (DebugPlayerDetection)
        {
            int debugBoids = Mathf.RoundToInt(boidCount * debugPercentage);
            for (int i = 0; i < debugBoids; i++)
            {
                boids[i].debugPlayerDetection = true;
            }
        }
        if (DebugObstacleAvoidance)
        {
            int debugBoids = Mathf.RoundToInt(boidCount * debugPercentage);
            for (int i = 0; i < debugBoids; i++)
            {
                boids[i].debugAvoidance = true;
            }
        }
    }

    public void RemoveBoid(Boid boid)
    {
        boids.Remove(boid);
    }

    public bool AnyBoidsPanicking()
    {
        return boids.Exists(b => b.currentState == BoidState.Panicking);
    }
}
