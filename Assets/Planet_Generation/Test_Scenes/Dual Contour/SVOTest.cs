using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;
using DualContour;

public class SVOTest : MonoBehaviour
{
    [SerializeField] private Transform cube;

    [SerializeField] private int patchSize = 65536;

    // Start is called before the first frame update
    SVO svo;
    Dual_Contour dualContour;
    void Start()
    {
        dualContour = new();
        SVONode root = new(new Vector3Int(0, 0, 0), patchSize);
        svo = new SVO(root, dualContour);
        root.Subdivide();
        root.children[0].Subdivide();
        root.children[0].children[0].Subdivide();
        root.children[7].Subdivide();
    }

    // Update is called once per frame
    void Update()
    {
        svo.TraverseLeaves(node =>
        {
            float distance = (node.GetCenter() - cube.position).magnitude;
            if (distance < node.size * 0.5f && !node.IsEmpty())
            {
                node.Subdivide(); // Just a placeholder

                //GENERATE VERTICES


                svo.GenerateVerticesForLeaves();
            }
        });
    }
    void OnDrawGizmos()
    {
        SVONode start = svo.root.children[0].children[3];
        SVONode destination = svo.TraversePath(new Vector3Int((int)cube.position.x, (int)cube.position.y, (int)cube.position.z));
        SVONode xNeighbor = destination.GetNeighborLOD(4); // +x
        SVONode yNeighbor = destination.GetNeighborLOD(2); // +y
        SVONode zNeighbor = destination.GetNeighborLOD(1); // +z

        void action(SVONode node)
        {
            if (node == start)
            {
                Gizmos.color = Color.red;
            }
            else if (node == xNeighbor)
            {
                Gizmos.color = Color.blue;
            }
            else if (node == yNeighbor)
            {
                Gizmos.color = Color.yellow;
            }
            else if (node == zNeighbor)
            {
                Gizmos.color = Color.magenta;
            }
            else if (node == destination)
            {
                Gizmos.color = Color.green;
            }
            else
            {
                Gizmos.color = Color.white;
            }

            Gizmos.color = node.IsEmpty() ? new Color(1, 0, 0, 0.5f) : new Color(1, 1, 1, 0.5f);
            Gizmos.DrawCube(node.position + Vector3.one * node.size * 0.5f, Vector3.one * node.size);

        }
        svo.TraverseLeaves(action);
    }

    public SVO GetSVO() { return svo; }
}
