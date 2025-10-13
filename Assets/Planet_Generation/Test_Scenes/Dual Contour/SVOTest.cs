using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;
using DualContour;

public class SVOTest : MonoBehaviour
{
    [SerializeField] private Transform cube;
    [SerializeField] private int getFaceNum;

    [SerializeField] private int dir;

    [SerializeField] private int patchSize;
    [SerializeField] private bool freezeSubdivision = false;
    private bool refreshChunks;
    [SerializeField] private float timeToRefresh;
    [SerializeField] private float elapsedTime;
    [SerializeField] private float nodeSizeLimit;

    [SerializeField] private int vertexLength;
    [SerializeField] private bool blockVoxel;

    HashSet<SVONode> frontier = new HashSet<SVONode>();


    // Start is called before the first frame update
    SVO svo;
    Dual_Contour dualContour;
    void Start()
    {
        refreshChunks = false;

        dualContour = new();
        dualContour.SetBlockVoxel(blockVoxel);
        dualContour.SetRadius(patchSize);
        dualContour.SetDir(dir);
        dualContour.SetCubeAxis();


        SVONode root = new(new Vector3Int(0, 0, 0), patchSize);
        svo = new SVO(root, dualContour, this.gameObject);
        frontier.Add(root);
    }

    // Update is called once per frame
    void Update()
    {
        vertexLength = svo.vertices.Count;
        Vector3 cubeForward = cube.forward.normalized;
        Vector3 cubePos = cube.position;


        List<SVONode> nodesToSubdivide = new List<SVONode>();
        List<SVONode> nodesToCollapse = new List<SVONode>();

        foreach (var node in frontier)
        {
            Vector3 delta = dualContour.CubeToSphere(node.Center, node.position.y) - cubePos;
            float distSq = delta.sqrMagnitude;
            float dot = Vector3.Dot(delta, cubeForward) / Mathf.Sqrt(distSq);

            if (!freezeSubdivision && node.MayContainCrossing() &&
                distSq < (node.size * node.size * 100f) &&
                node.size > nodeSizeLimit)
            {
                nodesToSubdivide.Add(node);
            }
            else if (!freezeSubdivision &&
                    (distSq > node.size * node.size * 400f))
            {
                node.voteToCollapse = true;
                nodesToCollapse.Add(node.parent);
            }
        }

        foreach (var node in nodesToSubdivide)
        {
            node.Subdivide();
            node.GenerateVerticesForLeaves(svo.meshingAlgorithm.SVOVertex);
            svo.MarkChunk(node);
            refreshChunks = true;

            foreach (var child in node.children)
            {
                frontier.Add(child);
            }
            frontier.Remove(node);
        }

        foreach (SVONode node in nodesToCollapse)
        {

            bool collapse = true;
            if (node.children == null) continue;
            foreach (var child in node.children)
            {
                if (!child.voteToCollapse)
                {
                    collapse = false;
                    break;
                }
            }
            if (!collapse) continue;

            frontier.Add(node);
            //if (node.children == null) continue;
            foreach (var child in node.children)
            {
                frontier.Remove(child);
            }
            node.Collapse();
            node.GenerateVerticesForLeaves(svo.meshingAlgorithm.SVOVertex);
            svo.MarkChunk(node);
            refreshChunks = true;
        }

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
        return;
        if (svo == null) return;

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
                Gizmos.color = Color.white;
            }


            Gizmos.DrawWireCube(node.position + 0.5f * node.size * Vector3.one, Vector3.one * node.size);

        }
        svo.TraverseLeaves(action);
    }

    public SVO GetSVO() { return svo; }
    public void SetFreeze(bool b) { freezeSubdivision = b; }
}
