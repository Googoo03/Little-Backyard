using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;
using DualContour;

public class SVOTest : MonoBehaviour
{
    [SerializeField] private Transform cube;
    [SerializeField] private int getFaceNum;

    [SerializeField] private int patchSize = 65536;
    [SerializeField] private bool freezeSubdivision = false;
    private bool refreshChunks;
    [SerializeField] private float timeToRefresh;
    [SerializeField] private float elapsedTime;

    // Start is called before the first frame update
    SVO svo;
    Dual_Contour dualContour;
    void Start()
    {
        refreshChunks = false;

        dualContour = new();
        SVONode root = new(new Vector3Int(0, 0, 0), patchSize);
        svo = new SVO(root, dualContour);
        root.Subdivide();
        root.children[0].Subdivide();
        root.children[0].children[0].Subdivide();
        root.children[5].Subdivide();
        root.children[5].children[0].Subdivide();
        root.children[7].Subdivide();
    }

    // Update is called once per frame
    void Update()
    {
        svo.TraverseLeaves(node =>
        {
            float distance = (node.GetCenter() - cube.position).magnitude;
            if (!freezeSubdivision && !node.IsEmpty() && distance < node.size * 5f && node.size > 1)
            {
                node.Subdivide(); // Just a placeholder
                refreshChunks = true;
                //GENERATE VERTICES
            }
            else if (!freezeSubdivision && distance > node.size * 10f)
            {
                node.voteToCollapse = true;
            }
        });

        svo.TraverseNodes(node =>
        {
            if (node.isLeaf) return;
            bool collapse = true;

            foreach (SVONode child in node.children)
            {
                collapse &= child.voteToCollapse;
            }
            if (collapse)
            {
                node.Collapse();
            }
        });



        svo.GenerateVerticesForLeaves();
        elapsedTime += Time.deltaTime;
        if (elapsedTime > timeToRefresh)
        {

            if (refreshChunks)
            {
                svo.GenerateChunks();
            }
            elapsedTime = 0;
            refreshChunks = false;
        }
    }
    void OnDrawGizmos()
    {
        SVONode start = svo.root.children[0].children[3];
        SVONode destination = svo.TraversePath(new Vector3Int((int)cube.position.x, (int)cube.position.y, (int)cube.position.z));
        SVONode xNeighbor = destination.GetNeighborLOD(4); // +x

        SVONode zNeighbor = destination.GetNeighborLOD(1); // +z
        SVONode zyNeighbor = zNeighbor.GetNeighborLOD(2); // +y

        List<SVONode> xface = xNeighbor.GetFace(getFaceNum);
        List<SVONode> zface = zNeighbor.GetFace(1);

        void action(SVONode node)
        {

            Gizmos.color = node.IsEmpty() ? new Color(1, 0, 0, 0.0f) : new Color(1, 1, 1, 0.5f);
            if (node == start)
            {
                //Gizmos.color = Color.red;
            }
            else if (xface.Contains(node))
            {
                Gizmos.color = Color.blue;
            }
            else if (node == zyNeighbor)
            {
                Gizmos.color = Color.yellow;
            }
            else if (zface.Contains(node))
            {
                Gizmos.color = Color.magenta;
            }
            else if (node == destination)
            {
                Gizmos.color = Color.green;
            }
            else
            {
                //Gizmos.color = Color.white;
            }


            Gizmos.DrawWireCube(node.position + 0.5f * node.size * Vector3.one, Vector3.one * node.size);

        }
        svo.TraverseLeaves(action);
    }

    public SVO GetSVO() { return svo; }
    public void SetFreeze(bool b) { freezeSubdivision = b; }
}
