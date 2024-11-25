using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using DualContour;
using System;
using UnityEngine.Rendering;
using Unity.Collections;

public class DC_Chunk : MonoBehaviour
{
    private Dual_Contour dc;
    [SerializeField] private Vector3Int scale; //how much voxel resolution each chunk receives
    [SerializeField] private int length; //how long in units each chunk is
    [SerializeField] private bool block_voxel;

    [SerializeField] private Material mat;

    //MESH DETAILS
    private Mesh m;
    private MeshFilter mf;
    private MeshRenderer rend;
    private List<Vector3> vertices = new List<Vector3>();
    private Vector3[] normals;
    private Vector2[] uvs;
    private List<int> indices = new List<int>();
    [SerializeField] private List<int> ind_serial_arr = new List<int>();

    //COMPUTE SHADER
    [SerializeField] private ComputeShader DC_Compute;
    ComputeBuffer verts;
    ComputeBuffer dualGrid;
    ComputeBuffer ind;
    [SerializeField] private Vector3[] verts_arr;
    [SerializeField] private uint[] ind_arr;
    [SerializeField] private uint[] dual_arr;

    [SerializeField] private Texture2D blueNoise;
    Stopwatch computeStopWatch = new Stopwatch();


    //GPU Readbacks
    AsyncGPUReadbackRequest dualGridReadbackRequest;
    AsyncGPUReadbackRequest vertReadbackRequest;
    int requestCount = 0;

    private void Start()
    {
        //Testing purposes
        //ind_serial_arr = new int[4 * scale.x * scale.y * scale.z];


        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        if (!rend) rend = this.gameObject.AddComponent<MeshRenderer>();


        if (!mf) mf = this.gameObject.AddComponent<MeshFilter>();

        if (m) m.Clear();
        m = mf.sharedMesh = new Mesh();

        //Initialize and generate vertices and quads
        dc = new Dual_Contour(transform.position, scale, length, block_voxel);
        dc.Generate(ref vertices, ref indices);

        /*m.vertices = vertices.ToArray();
        m.normals = normals;
        m.uv = uvs;
        m.SetIndices(indices, MeshTopology.Quads, 0);
        m.RecalculateBounds();
        m.RecalculateNormals();*/

        rend.material = mat;



        stopwatch.Stop();
        UnityEngine.Debug.Log("Took " + stopwatch.ElapsedMilliseconds.ToString() + " milliseconds");

        InitComputeShader();
    }

    private void InitComputeShader() {

        

        verts = new ComputeBuffer(scale.x*scale.y*scale.z, sizeof(float) * 3);
        dualGrid = new ComputeBuffer(scale.x * scale.y * scale.z, sizeof(UInt32));
        ind = new ComputeBuffer(scale.x * scale.y * scale.z * 4, sizeof(UInt32));

        int shaderHandle = DC_Compute.FindKernel("CSMain");

        DC_Compute.SetBuffer(shaderHandle, "vertexBuffer", verts);
        DC_Compute.SetVector("dimensions", (Vector3)scale);
        DC_Compute.SetInt("CELL_SIZE", length);
        DC_Compute.SetVector("global", transform.position);
        DC_Compute.SetBuffer(shaderHandle, "dualGrid", dualGrid);
        DC_Compute.SetBuffer(shaderHandle, "indices", ind);

        RenderTexture blueNoise_Test = new RenderTexture(scale.x, scale.y, 0, RenderTextureFormat.RFloat) { enableRandomWrite = true };

        DC_Compute.SetTexture(shaderHandle, "SimplexTex", blueNoise);

        computeStopWatch.Start();

        //dispatch
        DC_Compute.Dispatch(shaderHandle, 1, 1, 1);


        //Need to receive the verts and indices

        AsyncGPUReadback.Request(verts, VertsReadback);
        AsyncGPUReadback.Request(dualGrid, DualGridReadback);

    }

    private void MeshHelper() {
        AssignIndices();

        if (m) m.Clear();
        m = mf.sharedMesh = new Mesh();

        m.vertices = verts_arr;
        m.normals = normals;
        m.uv = uvs;
        m.SetIndices(ind_serial_arr, MeshTopology.Quads, 0);
        m.RecalculateBounds();
        m.RecalculateNormals();
    }


    protected void VertsReadback(AsyncGPUReadbackRequest request)
    {
        NativeArray<Vector3> _vertices;
        _vertices = request.GetData<Vector3>();

        verts_arr = new Vector3[scale.x * scale.y * scale.z];
        _vertices.CopyTo(verts_arr);

        verts.Release();
        verts_arr = condense(ref verts_arr, Vector3.zero);

        requestCount++;
        if (requestCount >= 2) MeshHelper();

        return;
    }

    protected void DualGridReadback(AsyncGPUReadbackRequest request)
    {
        NativeArray<uint> _dual;
        _dual = request.GetData<uint>();

        dual_arr = new uint[scale.x * scale.y * scale.z];
        _dual.CopyTo(dual_arr);

        //dual_arr = condense_generic<uint>(ref dual_arr, 0);
        requestCount++;
        if (requestCount >= 2) MeshHelper();

        return;
    }

    protected void IndicesReadback(AsyncGPUReadbackRequest request)
    {
        NativeArray<uint> _in;
        _in = request.GetData<uint>();

        ind_arr = new uint[scale.x * scale.y * scale.z * 4];
        _in.CopyTo(ind_arr);

        ind.Release();
        ind_arr = condense_generic<uint>(ref ind_arr, 0);

        computeStopWatch.Stop();
        UnityEngine.Debug.Log("Compute took " + computeStopWatch.ElapsedMilliseconds.ToString() + " milliseconds");

        return;
    }

    private void indexVerticesParallel(ref Vector3[] vertices, ref int[] indices)
    {

    }

    private Vector3[] condense(ref Vector3[] old, Vector3 nullVal) {
        List<Vector3> newValues = new List<Vector3>();

        for (int i = 0; i < old.Length; ++i) {
            if (!old[i].Equals(nullVal)) newValues.Add(old[i]);
        }

        return newValues.ToArray();
    }

    private T[] condense_generic<T>(ref T[] old, T nullVal) where T : IComparable
    {
        List<T> newValues = new List<T>();

        for (int i = 0; i < old.Length; ++i)
        {
            if (!old[i].Equals(nullVal)) newValues.Add(old[i]);
        }

        return newValues.ToArray();
    }

    void AssignIndices() {
        for (int x = 0; x < scale.x - 1; ++x)
        {
            for (int y = 0; y < scale.y - 1; ++y)
            {
                for (int z = 0; z < scale.z - 1; ++z)
                {
                    CreateQuad(x, y, z);
                }
            }
        }
    }

    void CreateQuad(int x, int y, int z)
    {
        //IF WE DONT WANT TO USE A LIST, THEN WE HAVE TO KEEP TRACK OF THE ITERATOR

        //WELL DO THAT LATER THOUGH, THE GOAL IS TO GET THIS WORKING FIRST


        int index = (int)(x + scale.x * (y + scale.y * z));
        uint dualGrid_index = dual_arr[index];

        if ((dualGrid_index & 0x20) == 0x20) //if edge[0].crossed
        {
            //Make a quad from neighboring x cells
            if ((dualGrid_index & 0x10) == 0x10) //if edge[0].sign
            {

                //IF BOTH VERTS AND DUALGRID ARE INDEXED THE SAME THEN JUST REFERENCE VERTS


                ind_serial_arr.Add((int)dual_arr[index] >> 6);
                ind_serial_arr.Add((int)dual_arr[(x + 1) + scale.x * (y + scale.y * z)] >> 6);
                ind_serial_arr.Add((int)dual_arr[(x + 1) + scale.x * ((y + 1) + scale.y * z)] >> 6);
                ind_serial_arr.Add((int)dual_arr[x + scale.x * ((y + 1) + scale.y * z)] >> 6);
            }
            else
            {
                ind_serial_arr.Add((int)dual_arr[index] >> 6);
                ind_serial_arr.Add((int)dual_arr[(x + scale.x * ((y + 1) + scale.y * z))] >> 6);
                ind_serial_arr.Add((int)dual_arr[((x + 1) + scale.x * ((y + 1) + scale.y * z))] >> 6);
                ind_serial_arr.Add((int)dual_arr[((x + 1) + scale.x * (y + scale.y * z))] >> 6);

            }
        }
        if ((dualGrid_index & 0x8) == 0x8)
        {
            //indices[index] = 512;
            //Make a quad from neighboring y cells
            if ((dualGrid_index & 0x4) == 0x4)
            {
                ind_serial_arr.Add((int)dual_arr[index] >> 6);
                ind_serial_arr.Add((int)dual_arr[(x + 1) + scale.x * (y + scale.y * z)] >> 6);
                ind_serial_arr.Add((int)dual_arr[(x + 1) + scale.x * (y + scale.y * (z + 1))] >> 6);
                ind_serial_arr.Add((int)dual_arr[x + scale.x * (y + scale.y * (z + 1))] >> 6);
            }
            else
            {

                ind_serial_arr.Add((int)dual_arr[index] >> 6);
                ind_serial_arr.Add((int)(dual_arr[(x + 1) + scale.x * (y + scale.y * z)] >> 6));
                ind_serial_arr.Add((int)(dual_arr[(x + 1) + scale.x * (y + scale.y * (z + 1))] >> 6));
                ind_serial_arr.Add((int)(dual_arr[x + scale.x * (y + scale.y * (z + 1))] >> 6));

            }
        }
        if ((dualGrid_index & 0x2) == 0x2)
        {
            //Make a quad from neighboring z cells
            if ((dualGrid_index & 0x1) == 0x1)
            {
                ind_serial_arr.Add((int)dual_arr[index] >> 6);
                ind_serial_arr.Add((int)dual_arr[x + scale.x * ((y + 1) + scale.y * z)] >> 6);
                ind_serial_arr.Add((int)dual_arr[x + scale.x * ((y + 1) + scale.y * (z + 1))] >> 6);
                ind_serial_arr.Add((int)dual_arr[x + scale.x * (y + scale.y * (z + 1))] >> 6);
            }
            else
            {
                ind_serial_arr.Add((int)dual_arr[index] >> 6);
                ind_serial_arr.Add((int)dual_arr[x + scale.x * (y + scale.y * (z + 1))] >> 6);
                ind_serial_arr.Add((int)dual_arr[x + scale.x * ((y + 1) + scale.y * (z + 1))] >> 6);
                ind_serial_arr.Add((int)dual_arr[x + scale.x * ((y + 1) + scale.y * z)] >> 6);
            }
        }
    }


}
