using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;
using DualContour;
using faces;

public class SVOTest : MonoBehaviour
{
    [SerializeField] private Transform cube;
    [SerializeField] private CubeSphereSVOWrapper planetFaceWrapper;
    [SerializeField] private int getFaceNum;

    [SerializeField] private int dir;

    [SerializeField] private int patchSize;
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
        refreshChunks = false;

        dualContour = new();
        dualContour.SetBlockVoxel(blockVoxel);
        dualContour.SetRadius(patchSize);
        dualContour.SetDir(dir);
        dualContour.SetCubeAxis(faceNum);


        SVONode root = new(new Vector3Int(0, 0, 0), patchSize, null, -1, null);
        svo = new SVO(root, dualContour, this.gameObject, planetFaceWrapper.neighbors, faceNum);
        root.SetSVO(svo);
        frontier.Add(root);
    }

    // Update is called once per frame
    void Update()
    {
        vertexLength = svo.vertices.Count;
        Vector3 cubeForward = cube.forward.normalized;
        Vector3 cubePos = cube.position;


        nodesToSubdivide.Clear();
        nodesToCollapse.Clear();

        if (freezeSubdivision) return;

        foreach (var node in frontier)
        {
            Vector3 delta = node.transformedPosition - cubePos;
            float distSq = delta.sqrMagnitude;


            if (node.MayContainCrossing() &&
                (distSq < (node.size * node.size * 100f)) && node.size > nodeSizeMin
                )
            {
                nodesToSubdivide.Add(node);
            }
            else if ((distSq > node.size * node.size * 400f) && node.size < nodeSizeMax)
            {
                node.voteToCollapse = true;
                nodesToCollapse.Add(node.parent);
            }
        }

        foreach (var node in nodesToSubdivide)
        {
            node.Subdivide(dualContour.CubeToSphere);
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

        //Add time delta for update
        elapsedTime += Time.deltaTime;
        if (elapsedTime < timeToRefresh) return;


        if (refreshChunks)
        {
            svo.GenerateChunks();
        }
        elapsedTime = 0;
        refreshChunks = false;

    }

    public void OnDrawGizmos()
    {

        Vector3 start = transform.position + Face.Faces[faceNum].normal * patchSize;
        float scale = 0.5f;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(start, start + (Face.Faces[faceNum].normal * patchSize * scale));

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(start, start + (Face.Faces[faceNum].uaxis * patchSize * scale));

        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, start + (Face.Faces[faceNum].vaxis * patchSize * scale));

        Gizmos.DrawRay(
            transform.position + Face.Faces[faceNum].normal * patchSize,
            Face.Faces[faceNum].normal
        );

    }



    public SVO GetSVO() { return svo; }
    public void SetFreeze(bool b) { freezeSubdivision = b; }
}
