using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;
using DualContour;
using faces;

public class SVOTest : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private PlanetWrapper planetFaceWrapper;
    [SerializeField] private int getFaceNum;

    [SerializeField] private int dir;

    [SerializeField] private int SVOGridSize;
    [SerializeField] private bool freezeSubdivision = false;
    private bool refreshChunks;
    [SerializeField] private float timeToRefresh;
    [SerializeField] private float elapsedTime;
    [SerializeField] private float nodeSizeMin;
    [SerializeField] private float nodeSizeMax;

    [SerializeField] private int vertexLength;
    [SerializeField] private bool blockVoxel;
    [SerializeField] private int faceNum;

    HashSet<SVONode> frontier = new HashSet<SVONode>();
    SVO svo;
    Dual_Contour dualContour;

    private List<SVONode> nodesToSubdivide = new();
    private List<SVONode> nodesToCollapse = new();

    // Start is called before the first frame update
    void Start()
    {
        //Set main camera at start
        player = Camera.main.transform;

        //I dont like this, should be refactored somehow
        dualContour = new();
        dualContour.SetBlockVoxel(blockVoxel);
        dualContour.SetRadius(SVOGridSize / 2);
        //dualContour.SetDir(dir);
        //dualContour.SetCubeAxis(faceNum);


        refreshChunks = false;

        //Define root node of SVO
        SVONode root = new(new Vector3Int(0, 0, 0), SVOGridSize, null, -1, null);
        svo = new SVO(root, dualContour, this.gameObject, planetFaceWrapper.neighbors, faceNum, planetFaceWrapper);
        root.SetSVO(svo);
        frontier.Add(root);
    }

    // Update is called once per frame
    void Update()
    {
        UpdateSVONodes();

    }

    void UpdateSVONodes()
    {

        vertexLength = svo.vertices.Count;
        Vector3 playerForward = player.forward.normalized;
        Vector3 playerPos = player.position;

        //freezeSubdivision = Vector3.Distance(playerPos, transform.position) > SVOGridSize * 4f;

        if (freezeSubdivision) return;

        //Add time delta for update
        elapsedTime += Time.deltaTime;
        if (elapsedTime < timeToRefresh) return;

        nodesToSubdivide.Clear();
        nodesToCollapse.Clear();

        float minDist, maxDist;

        foreach (var node in frontier)
        {
            Vector3 delta = (node.transformedPosition + transform.position) - playerPos;
            float distSq = delta.sqrMagnitude;
            minDist = node.size * node.size * 100f;
            maxDist = node.size * node.size * 400f;

            if (node.MayContainCrossing() &&
                (((distSq < minDist) && node.size > nodeSizeMin) || node.size > nodeSizeMax))
            {
                nodesToSubdivide.Add(node);
            }
            else if (((distSq > maxDist) && node.size < nodeSizeMax))
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

            //get neighbors to mark chunks as well.

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
            foreach (var child in node.children)
            {
                frontier.Remove(child);
            }
            node.Collapse();
            node.GenerateVerticesForLeaves(svo.meshingAlgorithm.SVOVertex);
            svo.MarkChunk(node);
            refreshChunks = true;
        }


        if (refreshChunks)
        {
            svo.GenerateChunks();
        }
        elapsedTime = 0;
        refreshChunks = false;
    }

    public void OnDrawGizmos()
    {
        return;
        Vector3 start = transform.position + Face.Faces[faceNum].normal * SVOGridSize;
        float scale = 0.5f;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(start, start + (Face.Faces[faceNum].normal * SVOGridSize * scale));

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(start, start + (Face.Faces[faceNum].uaxis * SVOGridSize * scale));

        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, start + (Face.Faces[faceNum].vaxis * SVOGridSize * scale));

        Gizmos.DrawRay(
            transform.position + Face.Faces[faceNum].normal * SVOGridSize,
            Face.Faces[faceNum].normal
        );

        void action(SVONode node)
        {
            if (node.size < 1024) return;
            Gizmos.DrawWireCube(node.center, Vector3.one * node.size);
        }
        svo.TraverseNodes(action);

    }



    public SVO GetSVO() { return svo; }
    public void SetFreeze(bool b) { freezeSubdivision = b; }

    public void SetPatchSize(int patchSize_) { SVOGridSize = patchSize_; }
}
