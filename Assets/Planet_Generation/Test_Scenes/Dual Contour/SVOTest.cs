using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;

public class SVOTest : MonoBehaviour
{
    // Start is called before the first frame update
    SVO svo;
    void Start()
    {
        SVONode root = new(new Vector3Int(0, 0, 0), 16);
        svo = new SVO(root);
        root.Subdivide();
        root.children[0].Subdivide();
        root.children[7].Subdivide();
    }

    // Update is called once per frame
    void Update()
    {

    }
    void OnDrawGizmos()
    {
        SVONode start = svo.root.children[0].children[3];
        SVONode neighbor = start.GetNeighborLOD(1); // +z
        Debug.Log("Start Parent: " + start.parent.position.ToString());
        Debug.Log("Neighbor: " + (neighbor != null ? neighbor.position.ToString() : "null"));
        void action(SVONode node)
        {
            if (node == start)
            {
                Gizmos.color = Color.red;
            }
            else if (node == neighbor)
            {
                Gizmos.color = Color.blue;
            }
            else
            {
                Gizmos.color = Color.white;
            }
            Gizmos.DrawWireCube(node.position + Vector3.one * node.size * 0.5f, Vector3.one * node.size);

        }
        svo.TraverseLeaves(action);
    }
}
