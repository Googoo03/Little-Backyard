using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System.Runtime.InteropServices;
using SparseVoxelOctree;
using DualContour;
using faces;
using UnityEngine.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct GPUSVONode
{
    public Vector3 position;
    public float size;
    public float min, max;
    public int edge;
    public Vector3 vertex;

    public GPUSVONode(SVONode node)
    {
        position = node.position;
        size = node.size;
        min = 0;
        max = 0;
        edge = -1;
        vertex = Vector3.zero;

    }
};
public class SVOTest : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private PlanetWrapper planetFaceWrapper;
    [SerializeField] private int getFaceNum;

    [SerializeField] private int dir;

    [SerializeField] private int SVOGridSize;
    [SerializeField] private bool freezeSubdivision = false;
    private bool refreshChunks;
    private bool returnedCompute;
    [SerializeField] private float timeToRefresh;
    [SerializeField] private float elapsedTime;
    [SerializeField] private float nodeSizeMin;
    [SerializeField] private float nodeSizeMax;

    [SerializeField] private int vertexLength;
    [SerializeField] private bool blockVoxel;
    [SerializeField] private int faceNum;
    [SerializeField] private ComputeShader computeVertices;
    ComputeBuffer nodesToGenerateBuffer;
    object lockObj = new();

    HashSet<SVONode> frontier = new();
    SVO svo;
    Dual_Contour dualContour;

    private List<SVONode> nodesToSubdivide = new();
    private List<SVONode> nodesToCollapse = new();
    public List<GPUSVONode> nodesToGenerate = new();
    public List<SVONode> updatedLeaves = new();

    // Start is called before the first frame update
    void Start()
    {
        //Set main camera at start
        player = Camera.main.transform;

        //I dont like this, should be refactored somehow
        dualContour = new();
        dualContour.SetBlockVoxel(blockVoxel);
        dualContour.SetRadius(SVOGridSize / 2);

        refreshChunks = false;
        returnedCompute = true;

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
        if (elapsedTime < timeToRefresh || !returnedCompute) return;

        nodesToSubdivide.Clear();
        nodesToCollapse.Clear();
        nodesToGenerate.Clear();

        updatedLeaves.Clear();

        float minDist, maxDist;
        int count = frontier.Count;

        foreach (SVONode node in frontier)
        {
            Vector3 delta = (node.transformedPosition + transform.position) - playerPos;
            float distSq = delta.sqrMagnitude;
            minDist = node.size * node.size * 100f;
            maxDist = node.size * node.size * 400f;

            if (node.isLeaf && node.MayContainCrossing() &&
                (((distSq < minDist) && node.size > nodeSizeMin) || node.size > nodeSizeMax))
            {
                nodesToSubdivide.Add(node);
            }
            else if ((distSq > maxDist) && node.size < nodeSizeMax)
            {
                node.voteToCollapse = true;
                nodesToCollapse.Add(node.parent);
            }
        }

        foreach (SVONode node in nodesToSubdivide)
        {
            node.Subdivide();
            //node.GenerateVerticesForLeaves(svo.meshingAlgorithm.SVOVertex);
            node.AppendLeavesToBuffer(nodesToGenerate, updatedLeaves);
            svo.MarkChunk(node);

            //get neighbors to mark chunks as well.

            refreshChunks = true;

            for (int i = 0; i < 8; ++i) { frontier.Add(node.children[i]); }
            frontier.Remove(node);
        }

        foreach (SVONode node in nodesToCollapse)
        {
            bool collapse = true;
            if (node.children == null) continue;
            for (int i = 0; i < 8; ++i)
            {
                if (!node.children[i].voteToCollapse)
                {
                    collapse = false;
                    break;
                }
            }
            if (!collapse) continue;

            frontier.Add(node);
            for (int i = 0; i < 8; ++i) { frontier.Remove(node.children[i]); }

            node.Collapse();
            //node.GenerateVerticesForLeaves(svo.meshingAlgorithm.SVOVertex);
            node.AppendLeavesToBuffer(nodesToGenerate, updatedLeaves);
            svo.MarkChunk(node);
            refreshChunks = true;
        }


        if (refreshChunks)
        {
            returnedCompute = false;
            LoadandDispatchComputeShaderData();


            AsyncGPUReadback.Request(nodesToGenerateBuffer, (request) =>
            {
                if (request.hasError)
                {
                    Debug.LogError("GPU readback error");
                    return;
                }


                var temp = request.GetData<GPUSVONode>();
                //load into SVO
                lock (lockObj)
                {
                    foreach (SVONode node in updatedLeaves)
                    {
                        if (node.gpuBufferIndex == -1) continue;

                        GPUSVONode returnedNode = temp[node.gpuBufferIndex];
                        node.vertex = returnedNode.vertex;
                        node.minSDF = returnedNode.min;
                        node.maxSDF = returnedNode.max;
                        node.edge = returnedNode.edge;
                        node.gpuBufferIndex = -1;

                    }
                    temp.Dispose();

                    svo.GenerateChunks();
                    returnedCompute = true;
                }
            });


        }
        elapsedTime = 0;
        refreshChunks = false;
    }

    private void LoadandDispatchComputeShaderData()
    {
        nodesToGenerateBuffer = new(nodesToGenerate.Count, Marshal.SizeOf<GPUSVONode>());
        nodesToGenerateBuffer.SetData(nodesToGenerate.ToArray()); // this is incorrect, GPU node and CPU node are different

        int kernel = computeVertices.FindKernel("CSMain");
        int nodeLimit = nodesToGenerate.Count;
        computeVertices.SetBuffer(kernel, "nodes", nodesToGenerateBuffer);
        computeVertices.SetInt("nodeLimit", nodeLimit);

        //noise data
        Texture3D noiseTexture = Resources.Load("PlanetTexture") as Texture3D;
        computeVertices.SetTexture(kernel, "NoiseTexture", noiseTexture);

        //planet data
        computeVertices.SetFloat("radius", SVOGridSize / 2);

        //dispatch
        int groupX = Mathf.CeilToInt(nodeLimit / 64.0f);
        computeVertices.Dispatch(kernel, groupX, 1, 1);
    }

    public void OnDrawGizmos()
    {/*
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
*/
    }



    public SVO GetSVO() { return svo; }
    public void SetFreeze(bool b) { freezeSubdivision = b; }

    public void SetPatchSize(int patchSize_) { SVOGridSize = patchSize_; }
}
