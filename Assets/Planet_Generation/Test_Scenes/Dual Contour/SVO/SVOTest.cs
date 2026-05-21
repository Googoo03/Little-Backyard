using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System.Runtime.InteropServices;
using SparseVoxelOctree;
using DualContour;
using faces;
using UnityEngine.Rendering;
using SignedDistanceFields;
using NoiseComputeDispatch;
using NUnit.Framework.Internal;
using System;
using Voxel_Data;

[StructLayout(LayoutKind.Sequential)]
public struct GPUSVONode
{
    public Vector3 position;
    public float size;
    public float min, max;
    public int edge;
    public Vector3 vertex;
    public int materialIndex;

    public GPUSVONode(SVONode node)
    {
        position = node.position;
        size = node.size;
        min = 0;
        max = 0;
        edge = -1;
        vertex = Vector3.zero;
        materialIndex = 0;
    }
};

public struct GPUSDF
{
    volatile int id;
    volatile int invert;
    Vector3 center;
    Vector3 args;

    public GPUSDF(BISDF sdf)
    {
        id = 0;
        invert = sdf.invert;
        center = sdf.center;
        args = sdf.args;
    }
};
public class SVOTest : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private PlanetWrapper planetFaceWrapper;

    [SerializeField] private int SVOGridSize;
    [SerializeField] private bool freezeSubdivision = false;
    private bool refreshChunks;
    private bool returnedCompute;
    [SerializeField] private float timeToRefresh, elapsedTime;
    [SerializeField] private float nodeSizeMin, nodeSizeMax;

    [SerializeField] private int vertexLength;
    [SerializeField] private bool blockVoxel;
    [SerializeField] private int faceNum;
    [SerializeField] private ComputeShader computeVertices;
    [SerializeField] private int seed;
    ComputeBuffer nodesToGenerateBuffer;
    ComputeBuffer sdfsToInclude;
    object lockObj = new();

    List<SVONode> frontier = new();
    SVO svo;
    Dual_Contour dualContour;

    private List<SVONode> nodesToSubdivide = new();
    private List<SVONode> nodesToCollapse = new();
    public List<GPUSVONode> nodesToGenerate = new();
    public List<SVONode> updatedLeaves = new();

    [SerializeField] private RenderTexture continentNoiseTexture;
    [SerializeField] private RenderTexture mountainNoiseTexture;
    [SerializeField] private RenderTexture octaveNoiseTexture;

    //Each planet will have its own set of biomes. Each biome will have a color palette to choose from.
    //The color palette will affect the grass and greenery.

    // Start is called before the first frame update
    void Start()
    {
        CreateNoiseRenderTextures();

        //Set main camera at start
        player = Camera.main.transform;

        //I dont like this, should be refactored somehow
        dualContour = new();
        dualContour.SetBlockVoxel(blockVoxel);
        dualContour.SetRadius(SVOGridSize / 2);

        refreshChunks = false;
        returnedCompute = true;

        nodesToGenerate.Capacity = 2048;
        updatedLeaves.Capacity = 2048;

        //Define root node of SVO
        SVONode root = new(new Vector3Int(0, 0, 0), SVOGridSize, null, -1, null);
        svo = new SVO(root, dualContour, this.gameObject, planetFaceWrapper.neighbors, faceNum, planetFaceWrapper);
        nodeSizeMax = svo.chunkSize / 2;
        root.SetSVO(svo);
        AddFrontier(root);
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
        Vector3 positionDelta = transform.position - playerPos;

        //freezeSubdivision = Vector3.Distance(playerPos, transform.position) > SVOGridSize * 4f;

        if (freezeSubdivision) return;

        //Add time delta for update
        elapsedTime += Time.deltaTime;
        if (elapsedTime < timeToRefresh || !returnedCompute) return;

        nodesToSubdivide.Clear();
        nodesToCollapse.Clear();

        float minDist, maxDist;
        int count;

        count = frontier.Count;
        for (int n = 0; n < count; ++n)
        {
            SVONode node = frontier[n];
            float deltaX = node.transformedPosition.x + positionDelta.x;
            float deltaY = node.transformedPosition.y + positionDelta.y;
            float deltaZ = node.transformedPosition.z + positionDelta.z;
            float distSq = deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ;
            //Vector3 delta = node.transformedPosition + positionDelta;
            //float distSq = delta.magnitude;
            minDist = node.size * node.size * 25f;
            maxDist = node.size * node.size * 100f;

            if ((node.size > nodeSizeMax) || (node.isLeaf && node.MayContainCrossing() && distSq < minDist && node.size > nodeSizeMin))
            {
                nodesToSubdivide.Add(node);
            }
            else if ((distSq > maxDist) && node.size < nodeSizeMax)
            {
                node.voteToCollapse = true;
                nodesToCollapse.Add(node.parent);
            }
        }

        count = nodesToSubdivide.Count;
        for (int n = 0; n < count; n++)
        {
            SVONode node = nodesToSubdivide[n];


            if (node.edge == -1 && !node.MayContainCrossing())
            {
                RemoveSwap(node.frontierIndex);
                continue;
            }
            node.Subdivide();
            //GatherLeavesBuffer(node);
            svo.MarkChunk(node);

            //get neighbors to mark chunks as well.

            refreshChunks = true;

            for (int i = 0; i < 8; ++i) { AddFrontier(node.children[i]); }
            RemoveSwap(node.frontierIndex);
        }

        count = nodesToCollapse.Count;
        for (int n = 0; n < count; n++)
        {
            SVONode node = nodesToCollapse[n];
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

            AddFrontier(node);
            for (int i = 0; i < 8; ++i) { RemoveSwap(node.children[i].frontierIndex); }

            node.Collapse();
            //GatherLeavesBuffer(node);
            svo.MarkChunk(node);
            refreshChunks = true;
        }


        if (refreshChunks && returnedCompute)
        {
            refreshChunks = false;
            GatherChunkLeaves();
            if (nodesToGenerate.Count > 0)
            {
                returnedCompute = false;
                lock (lockObj) { LoadandDispatchComputeShaderData(); }
                InitiateReadbackRequest();
            }
        }
        elapsedTime = 0;

    }

    void GatherChunkLeaves()
    {
        foreach (SVONode chunk in svo.chunksToProcess)
        {
            GatherLeavesBuffer(chunk, true);
        }
    }

    public void GatherLeavesBuffer(SVONode node, bool forceGenerate = false, BISDF sdf = null)
    {
        lock (lockObj)
        {
            node?.AppendLeavesToBuffer(nodesToGenerate, updatedLeaves, forceGenerate, sdf);
        }
    }

    private void RemoveSwap(int index)
    {
        int lastIndex = frontier.Count - 1;
        frontier[lastIndex].frontierIndex = index;
        frontier[index].frontierIndex = -1;
        frontier[index] = frontier[lastIndex];

        frontier.RemoveAt(lastIndex);
    }

    private void AddFrontier(SVONode node)
    {
        node.frontierIndex = frontier.Count;
        frontier.Add(node);
    }


    public void InitiateReadbackRequest() //just makes sure the readback is called, should still be concurrent.
    {

        AsyncGPUReadback.Request(nodesToGenerateBuffer, (request) =>
            {
                if (request.hasError)
                {
                    Debug.LogError("GPU readback error");
                    return;
                }



                //load into SVO
                lock (lockObj)
                {
                    var temp = request.GetData<GPUSVONode>();
                    foreach (SVONode node in updatedLeaves)
                    {
                        if (node.gpuBufferIndex == -1) continue;
                        GPUSVONode returnedNode = temp[node.gpuBufferIndex];

                        if (float.IsNaN(returnedNode.vertex.x) || float.IsNaN(returnedNode.vertex.y) || float.IsNaN(returnedNode.vertex.z))
                        {
                            Debug.LogError("NaN vertex returned from GPU");
                            continue;
                        }

                        node.vertex = returnedNode.vertex;
                        node.minSDF = returnedNode.min;
                        node.maxSDF = returnedNode.max;
                        node.edge = returnedNode.edge;
                        node.materialIndex = (VOXEL)returnedNode.materialIndex;
                        node.gpuBufferIndex = -1;

                    }
                    temp.Dispose();
                    sdfsToInclude.Dispose();
                    nodesToGenerateBuffer.Dispose();

                    //reset for next iteration
                    nodesToGenerate.Clear();
                    updatedLeaves.Clear();

                    svo.GenerateChunks();
                    returnedCompute = true;
                }
            });
    }

    private void CreateNoiseRenderTextures()
    {
        continentNoiseTexture = new(64, 64, 0)
        {
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Repeat,
            format = RenderTextureFormat.RFloat,
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = 64
        };
        continentNoiseTexture.Create();

        NoiseProperties continentProp = new(NOISETYPE.PERLIN, seed, 8, 0.5f, 4f);
        NoisePipeline.Instance.ComputeNoiseTexture(continentNoiseTexture, continentProp);

        mountainNoiseTexture = new(64, 64, 0)
        {
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Repeat,
            format = RenderTextureFormat.RFloat,
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = 64
        };
        mountainNoiseTexture.Create();

        NoiseProperties mountainProp = new(NOISETYPE.PERLIN, seed * seed, 12, 0.5f, 2f);
        NoisePipeline.Instance.ComputeNoiseTexture(mountainNoiseTexture, mountainProp);

        octaveNoiseTexture = new(128, 128, 0)
        {
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Repeat,
            format = RenderTextureFormat.RFloat,
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = 128
        };
        octaveNoiseTexture.Create();

        NoiseProperties octaveProp = new(NOISETYPE.PERLIN, seed * seed * seed, 12, 0.5f, 256f);
        NoisePipeline.Instance.ComputeNoiseTexture(octaveNoiseTexture, octaveProp);
    }

    public void LoadandDispatchComputeShaderData()
    {
        int kernel = computeVertices.FindKernel("CSMain");

        nodesToGenerateBuffer = new(Mathf.Max(1, nodesToGenerate.Count), Marshal.SizeOf<GPUSVONode>());
        nodesToGenerateBuffer.SetData(nodesToGenerate.ToArray());

        int nodeLimit = nodesToGenerate.Count;
        computeVertices.SetBuffer(kernel, "nodes", nodesToGenerateBuffer);
        computeVertices.SetInt("nodeLimit", nodeLimit);

        sdfsToInclude = new(Mathf.Max(1, svo.sdfEdits.Count), Marshal.SizeOf<GPUSDF>());
        List<GPUSDF> gpuSDFList = new();
        foreach (BISDF sdf in svo.sdfEdits) { gpuSDFList.Add(new GPUSDF(sdf)); }
        sdfsToInclude.SetData(gpuSDFList.ToArray());

        int sdfLimit = sdfsToInclude.count;
        computeVertices.SetBuffer(kernel, "sdfs", sdfsToInclude);
        computeVertices.SetInt("sdfLimit", sdfLimit);

        //noise data
        //Texture3D noiseTexture = Resources.Load("PlanetTexture") as Texture3D;
        computeVertices.SetTexture(kernel, "ContinentTexture", continentNoiseTexture);
        computeVertices.SetTexture(kernel, "MountainTexture", mountainNoiseTexture);
        computeVertices.SetTexture(kernel, "OctaveTexture", octaveNoiseTexture);

        //planet data
        computeVertices.SetFloat("radius", SVOGridSize / 2);

        //dispatch
        int groupX = Mathf.CeilToInt(nodeLimit / 64.0f);
        computeVertices.Dispatch(kernel, groupX, 1, 1);
    }

    public void OnDrawGizmos()
    {
        /*
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

        static void action(SVONode node)
        {
            if (node.size > 1024) return;
            Gizmos.color = Color.green / (node.size / 2);
            Gizmos.DrawWireCube(node.center, Vector3.one * node.size);
        }
        svo.TraverseNodes(action);
        */
    }



    public SVO GetSVO() { return svo; }
    public void SetFreeze(bool b) { freezeSubdivision = b; }

    public void SetPatchSize(int patchSize_) { SVOGridSize = patchSize_; }

    public void FlagRefreshChunks() { refreshChunks = true; }
    public bool GetReturnedCompute() { return returnedCompute; }

    public void SetSeed(int seed_) { seed = seed_; }
}
