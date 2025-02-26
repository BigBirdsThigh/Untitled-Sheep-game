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
    private float nextReevaluationTime = 0f;
    private float reevaluationCooldown = 120f; // re-eval every 2 mins

    [HideInInspector]
    public List<Boid> boids = new List<Boid>();
    private List<GameObject> nodes = new List<GameObject>(); // Store all nodes as graph
    public GameObject roamNode; // Best node for roaming
    public GameObject panicNode; // Best node for panicking

    private bool gameOverTriggered = false; // Game does not initialise as over
    private bool roundActive = false; // so we only do a win or lose check after the round has started


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
        CacheNodes(); // Get all nodes from the scene
        roundActive = true; // round has started
        UpdateBestNodes();
        // SpawnBoids(boidCount); No longer starting the 1st round from the boidmanager   
    }

    public void SetPlayer(GameObject p){
        player = p.transform;
    }

    void FixedUpdate()
    {
        boids.RemoveAll(b => b == null); // Remove destroyed boids

        int regroupingBoids = GetRegroupingCount();
        int totalBoids = boids.Count;

        //Debug.Log($"Total Boids: {totalBoids}, Round Active?: {roundActive}");

        // Win condition - prevent triggering if game is already resetting
        // ** GAME MANAGER SHOULD HANDLE WIN/LOSE CONDITIONS **
        // if (totalBoids == 0 && roundActive)
        // {
        //     roundActive = false;
        //     StartCoroutine(Win());
        // }

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
        }
                
        // **Trigger limited re-evaluations based on time**
        if (Time.time >= nextReevaluationTime)
        {
            nextReevaluationTime = Time.time + reevaluationCooldown;
            Debug.Log("Periodic node reevaluation...");
            UpdateBestNodes();

            // **Reassign targets for all Boids**
            foreach (Boid boid in boids)
            {
                if (boid == null) continue;

                // Update roamers
                if (boid.currentState == BoidState.Roaming && roamNode != null)
                {
                    boid.ChangeTarget(roamNode.transform);
                }

                // Update panicking boids
                if (boid.currentState == BoidState.Panicking && panicNode != null)
                {
                    boid.ChangeTarget(panicNode.transform);
                }
            }
        }


        if (BoidReachedNode(roamNode))
        {
            Debug.Log("Recalculating roam node: A boid reached the target.");
            RecalculateRoamNode();
        }

        if (PlayerReachedNode(roamNode))
        {
            Debug.Log("Recalculating roam node: Player reached the target.");
            RecalculateRoamNode();
        }

        if (BoidReachedNode(panicNode))
        {
            Debug.Log("Recalculating panic node: A boid reached the target.");
            UpdateBestPanicNode();
        }

        if (PlayerReachedNode(panicNode))
        {
            Debug.Log("Recalculating panic node: Player reached the target.");
            UpdateBestPanicNode();
        }

        GameObject previousPanicNode = panicNode;
        //UpdateBestPanicNode(); // Updates `panicNode`

        if (previousPanicNode != panicNode) // Panic node changed
        {
            Debug.Log("Panic node updated, redirecting panicking boids.");
            foreach (Boid boid in boids)
            {
                if (boid.currentState == BoidState.Panicking)
                {
                    boid.ChangeTarget(panicNode.transform);
                }
            }
        }

    }

    private IEnumerator Win()
    {
        yield return new WaitForSeconds(2.5f);
        Debug.Log("Checking for win after delay...");
        GameManager.Instance?.TriggerWin();
    }


    public void ResetRound(int numBoids)
    {
        roundActive = false;
        CacheNodes();

        // Remove existing boids before spawning new ones
        foreach (Boid boid in boids)
        {
            if (boid != null)
            {
                Destroy(boid.gameObject);
            }
        }
        boids.Clear(); // Ensure the list is empty

        // Find a good spawn using Best First Search
        GameObject newStartNode = FindGoodSpawn();
        if (newStartNode != null)
        {
            transform.position = newStartNode.transform.position;
        }

        UpdateBestNodes(); // Reset node states
        StartCoroutine(DelayedSpawn(numBoids)); // Start spawning new boids
    }


    private IEnumerator DelayedSpawn(int numBoids)
    {
        yield return new WaitForSeconds(0.5f); // Small delay for positioning
        SpawnBoids(numBoids);
        UpdateBestNodes();
        roundActive = true;
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

            foreach (GameObject neighbor in nodes)
            {
                if (!visited.Contains(neighbor) &&
                    Vector3.Distance(currentNode.transform.position, neighbor.transform.position) < 7f)
                {
                    openSet.Enqueue(neighbor);
                    visited.Add(neighbor);
                }
            }

            nodesAtCurrentDepth--;
            if (nodesAtCurrentDepth == 0)
            {
                currentDepth++;
                nodesAtCurrentDepth = openSet.Count;
            }
        }

        roamNode = bestNode;  // Make sure we assign a roamNode
        panicNode = GetBestPanicNode(); // Assign a panicNode immediately

        return bestNode;
    }




    // evaluate node quality for new spawn
    float EvaluateNode(GameObject node)
    {
        float distanceToPlayer = Vector3.Distance(node.transform.position, player.position);
        float obstaclePenalty = GetObstacleScore(node.transform.position);
        return distanceToPlayer - obstaclePenalty; // Higher is better
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
            Debug.Log($"[BoidManager] Panic Node set to: {panicNode.name} at {panicNode.transform.position}");
        }
        else
        {
            Debug.LogError("[BoidManager] Panic node is NULL!");
        }
    }



    void CacheNodes()
    {
        GameObject nodesParent = GameObject.Find("Nodes");
        
        if (nodesParent == null)
        {
            Debug.LogError("[BoidManager] No 'Nodes' GameObject found in the scene!");
            return;
        }

        nodes = new List<GameObject>();

        foreach (Transform child in nodesParent.transform)
        {
            if (child.gameObject != null)
            {
                nodes.Add(child.gameObject);
                Debug.Log($"[BoidManager] Node added: {child.gameObject.name}");
            }
        }

        Debug.Log($"[BoidManager] Total nodes cached: {nodes.Count}");
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
        
        return Vector3.Distance(player.position, node.transform.position) < 3f; // 1m threshold
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
        if (nodes.Count == 0)
        {
            Debug.LogError("[Roam Node] No available nodes! Check CacheNodes().");
            return null;
        }

        GameObject selectedNode = nodes.OrderBy(n => Random.value).FirstOrDefault();

        if (selectedNode != null)
        {
            Debug.Log($"[Roam Node] Selected: {selectedNode.name} at {selectedNode.transform.position}");
        }
        else
        {
            Debug.LogError("[Roam Node] No valid node was selected! Assigning fallback node.");
            selectedNode = nodes[0]; // Ensure a node is assigned
        }

        return selectedNode;
    }




    public GameObject GetCurrPanicNode(){
        return panicNode;
    }


    GameObject GetBestPanicNode()
    {
        if (player == null || nodes.Count == 0)
        {
            Debug.LogWarning("[Panic Node] No player or nodes exist!");
            return nodes.Count > 0 ? nodes[0] : null;
        }

        GameObject bestNode = nodes.OrderByDescending(n =>
        {
            float distance = Vector3.Distance(n.transform.position, player.position);
            float obstaclePenalty = GetObstacleScore(n.transform.position);
            float losBonus = GetLOSScore(n.transform.position);

            float score = distance - obstaclePenalty + losBonus;
            Debug.Log($"[Panic Node] Checking {n.name} | Distance: {distance}, Obstacles: {obstaclePenalty}, LOS: {losBonus}, Score: {score}");
            return score;
        }).FirstOrDefault();

        if (bestNode != null)
        {
            Debug.Log($"[Panic Node] Selected: {bestNode.name} at {bestNode.transform.position}");
        }
        else
        {
            Debug.LogWarning("[Panic Node] No valid node was selected!");
        }

        return bestNode ?? nodes[0]; // Ensure fallback
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
            // Debug.LogWarning($"SetNodeColour: Node '{node.name}' at {node.transform.position} has no MeshRenderer.");
            return;
        }

        if (nodeRenderer.material == null)
        {
            // Debug.LogWarning($"SetNodeColour: Node '{node.name}' at {node.transform.position} has no Material assigned.");
            return;
        }

        // Debug.Log($"SetNodeColour: Changing color of '{node.name}' to {color}");
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
            boids.Add(newBoid);
            newBoid.ChangeState(BoidState.Roaming);
            newBoid.ChangeTarget(roamNode.transform);
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
        // ask game manager if we win
        GameManager.Instance?.CheckWin();
    }

    public bool AnyBoidsPanicking()
    {
        return boids.Exists(b => b.currentState == BoidState.Panicking);
    }


}

