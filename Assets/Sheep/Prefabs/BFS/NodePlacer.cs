using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class NodePlacer : MonoBehaviour
{
    [Header("Grid Settings")]
    public int rows = 5; // Number of rows
    public int columns = 5; // Number of columns
    public float spacing = 2f; // Spacing between nodes

    [Header("Area Settings")]
    public Vector3 areaSize = new Vector3(10, 1, 10); // Defines the size of the square
    public LayerMask groundLayer; // Layer to detect ground

    [Header("Debugging")]
    public bool showDebug = true; // Toggle for debug rendering
    public bool renderNodes = true; // Toggle to show/hide nodes
    private Transform nodesParent; // Parent container for nodes

    private void Start()
    {
        GenerateNodes();
    }

    private void OnEnable()
    {
        SetNodeRendering(true); // Ensure nodes are visible when NodePlacer is enabled
    }

    private void OnDisable()
    {
        SetNodeRendering(false); // Hide nodes when NodePlacer is deleted or disabled
    }

    public void GenerateNodes()
    {
        FindOrCreateParent(); // Ensure we have a "Nodes" parent
        ClearExistingNodes(); // Remove previous nodes

        Vector3 startPos = transform.position - new Vector3(areaSize.x / 2, 0, areaSize.z / 2);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector3 nodePosition = startPos + new Vector3(col * spacing, 0, row * spacing);
                nodePosition = GetSurfacePosition(nodePosition); // Adjust to ground height

                if (nodePosition != Vector3.zero)
                {
                    CreateDebugNode(nodePosition);
                }
            }
        }

        SetNodeRendering(renderNodes); // Apply rendering toggle
    }

    private void FindOrCreateParent()
    {
        GameObject existingParent = GameObject.Find("Nodes");
        if (existingParent)
        {
            nodesParent = existingParent.transform;
        }
        else
        {
            GameObject newParent = new GameObject("Nodes");
            nodesParent = newParent.transform;
        }
    }

    private void ClearExistingNodes()
    {
        if (nodesParent == null) return;

        for (int i = nodesParent.childCount - 1; i >= 0; i--)
        {
        #if UNITY_EDITOR
            DestroyImmediate(nodesParent.GetChild(i).gameObject);
        #else
            Destroy(nodesParent.GetChild(i).gameObject);
        #endif
        }
    }

    private Vector3 GetSurfacePosition(Vector3 position)
    {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(position.x, transform.position.y + 10, position.z), Vector3.down, out hit, Mathf.Infinity, groundLayer))
        {
            return hit.point;
        }
        return Vector3.zero; // Invalid placement
    }

    private void CreateDebugNode(Vector3 position)
    {
        GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        node.transform.position = position;
        node.transform.localScale = Vector3.one * 0.3f;

        Renderer renderer = node.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            if (urpMaterial == null)
            {
                Debug.LogWarning("CreateDebugNode: URP Shader not found! Make sure you are using the Universal Render Pipeline.");
            }
            else
            {
                urpMaterial.color = Color.white;
            #if UNITY_EDITOR
                renderer.sharedMaterial = urpMaterial;
            #else
                renderer.material = urpMaterial;
            #endif
            }
        }

        DestroyImmediate(node.GetComponent<Collider>()); // Remove collision for debug spheres
        node.transform.parent = nodesParent; // Parent under "Nodes"
    }


    public void SetNodeRendering(bool state)
    {
        if (nodesParent == null) return;

        foreach (Transform node in nodesParent)
        {
            Renderer nodeRenderer = node.GetComponent<Renderer>();
            if (nodeRenderer != null)
            {
                nodeRenderer.enabled = state;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebug) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, areaSize); // Draw area boundary

        Gizmos.color = Color.blue;
        if (nodesParent != null)
        {
            foreach (Transform node in nodesParent)
            {
                Gizmos.DrawSphere(node.position, 0.2f);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(NodePlacer))]
public class NodePlacerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        NodePlacer script = (NodePlacer)target;
        if (GUILayout.Button("Generate Nodes"))
        {
            script.GenerateNodes();
        }

        if (GUILayout.Button("Toggle Node Rendering"))
        {
            script.renderNodes = !script.renderNodes;
            script.SetNodeRendering(script.renderNodes);
        }
    }
}
#endif
