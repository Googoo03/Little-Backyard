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
    private UInt16[] voxel_data;

    [SerializeField]private Texture3D tex;

    [SerializeField] private List<int> ind_serial_arr = new List<int>();

    //COMPUTE SHADER
    [SerializeField] private ComputeShader DC_Compute;
    [SerializeField] private ComputeShader Simplex;

    ComputeBuffer verts;
    ComputeBuffer dualGrid;
    ComputeBuffer ind;
    [SerializeField] private Vector3[] verts_arr;
    [SerializeField] private uint[] ind_arr;
    [SerializeField] private int[] dual_arr;
    [SerializeField] private RenderTexture blueNoise_Test;

    [SerializeField] private Texture2D blueNoise;
    Stopwatch computeStopWatch = new Stopwatch();

    /////////////////////////////////////////////////////


    //GPU Readbacks
    AsyncGPUReadbackRequest dualGridReadbackRequest;
    AsyncGPUReadbackRequest vertReadbackRequest;
    int requestCount = 0;

    private void Start()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        if (!rend) rend = this.gameObject.AddComponent<MeshRenderer>();


        if (!mf) mf = this.gameObject.AddComponent<MeshFilter>();

        if (m) m.Clear();
        m = mf.sharedMesh = new Mesh();

        //Initialize and generate vertices and quads
        dc = new Dual_Contour(transform.position, scale, length, block_voxel);
        dc.Generate(ref vertices, ref indices, ref voxel_data);

        uvs = new Vector2[vertices.Count];
        for (int i = 0; i < vertices.Count; ++i) {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }

        CreateDataTexture();

        stopwatch.Stop();
        UnityEngine.Debug.Log("Took " + stopwatch.ElapsedMilliseconds.ToString() + " milliseconds");

        mat.SetTexture("_VoxelData",tex);
        rend.material = mat;

        m.vertices = vertices.ToArray();
        m.normals = normals;


        //IF WE WANT TEXTURES, WE HAVE TO CALCULATE THE UVS MANUALLY
        m.uv = uvs;
        ////////////////////////////////////////////////////////////

        m.SetIndices(indices, MeshTopology.Quads, 0);
        m.RecalculateBounds();
        m.RecalculateNormals();
                
    }

    private void CreateDataTexture() {
        tex = new Texture3D(scale.x, scale.y, scale.z, TextureFormat.R16, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixelData(voxel_data, 0, 0);
        tex.Apply();
    }



    private void InitComputeShader() {

        

        verts = new ComputeBuffer(scale.x*scale.y*scale.z, sizeof(float) * 3);
        dualGrid = new ComputeBuffer(scale.x * scale.y * scale.z, sizeof(int));
        //ind = new ComputeBuffer(scale.x * scale.y * scale.z * 4, sizeof(UInt32));

        int shaderHandle = DC_Compute.FindKernel("CSMain");
        int simplex_handle = Simplex.FindKernel("CSMain");

        DC_Compute.SetBuffer(shaderHandle, "vertexBuffer", verts);
        DC_Compute.SetVector("dimensions", (Vector3)scale);
        DC_Compute.SetInt("CELL_SIZE", length);
        DC_Compute.SetVector("global", transform.position);
        DC_Compute.SetBuffer(shaderHandle, "dualGrid", dualGrid);
        DC_Compute.SetBool("block_voxel", block_voxel);
        //DC_Compute.SetBuffer(shaderHandle, "indices", ind);

        blueNoise_Test = new RenderTexture(scale.x, scale.z, 0, RenderTextureFormat.RFloat) { enableRandomWrite = true };
        Graphics.Blit(blueNoise, blueNoise_Test);
        Graphics.SetRenderTarget(null);

        DC_Compute.SetTexture(shaderHandle, "SimplexTex", blueNoise_Test);

        computeStopWatch.Start();

        //dispatch
        DC_Compute.Dispatch(shaderHandle, 1, 1, 1);


        //Need to receive the verts and indices

        AsyncGPUReadback.Request(verts, VertsReadback);
        AsyncGPUReadback.Request(dualGrid, DualGridReadback);

    }

    private void MeshHelper() {
        AssignIndices();

        verts.Release(); dualGrid.Release(); //ind.Release();

        

        computeStopWatch.Stop();
        UnityEngine.Debug.Log("Compute took " + computeStopWatch.ElapsedMilliseconds.ToString() + " milliseconds");

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
        NativeArray<int> _dual;
        _dual = request.GetData<int>();

        dual_arr = new int[scale.x * scale.y * scale.z];
        _dual.CopyTo(dual_arr);

        //dualGrid.Release();
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
        int index = (int)(x + scale.x * (y + scale.y * z));
        int dualGrid_index = dual_arr[index];

        bool notSatisfy;

        if (dualGrid_index == -1) return;

        if ((dualGrid_index & 0x20) == 0x20) //if edge[0].crossed
        {
            //Make a quad from neighboring x cells
            if ((dualGrid_index & 0x10) == 0x10) //if edge[0].sign
            {
                notSatisfy = false;
                notSatisfy |= dual_arr[index] >> 6 == -1;
                notSatisfy |= dual_arr[(x + 1) + scale.x * (y + scale.y * z)]>> 6 == -1;
                notSatisfy |= dual_arr[(x + 1) + scale.x * ((y + 1) + scale.y * z)]>> 6 == -1;
                notSatisfy |= dual_arr[x + scale.x * ((y + 1) + scale.y * z)]>> 6 == -1;

                if (!notSatisfy)
                {
                    ind_serial_arr.Add(dual_arr[index] >> 6);
                    ind_serial_arr.Add(dual_arr[(x + 1) + scale.x * (y + scale.y * z)] >> 6);
                    ind_serial_arr.Add(dual_arr[(x + 1) + scale.x * ((y + 1) + scale.y * z)] >> 6);
                    ind_serial_arr.Add(dual_arr[x + scale.x * ((y + 1) + scale.y * z)] >> 6);
                }
                else { UnityEngine.Debug.Log(dual_arr[index].ToString()); }
            }
            else
            {
                notSatisfy = false;
                notSatisfy |= dual_arr[index] >> 6 == -1;
                notSatisfy |= dual_arr[(x + scale.x * ((y + 1) + scale.y * z))] >> 6 == -1;
                notSatisfy |= dual_arr[((x + 1) + scale.x * ((y + 1) + scale.y * z))] >> 6 == -1;
                notSatisfy |= dual_arr[((x + 1) + scale.x * (y + scale.y * z))] >> 6 == -1;

                if (!notSatisfy)
                {
                    ind_serial_arr.Add(dual_arr[index] >> 6);
                    ind_serial_arr.Add(dual_arr[(x + scale.x * ((y + 1) + scale.y * z))] >> 6);
                    ind_serial_arr.Add(dual_arr[((x + 1) + scale.x * ((y + 1) + scale.y * z))] >> 6);
                    ind_serial_arr.Add(dual_arr[((x + 1) + scale.x * (y + scale.y * z))] >> 6);
                }
                else { UnityEngine.Debug.Log(dual_arr[index].ToString()); }

            }
        }
        if ((dualGrid_index & 0x08) == 0x08)
        {
            //Make a quad from neighboring y cells
            if ((dualGrid_index & 0x04) == 0x04)
            {
                notSatisfy = false;
                notSatisfy |= (dual_arr[index] >> 6) == -1;
                notSatisfy |= (dual_arr[(x + 1) + scale.x * (y + scale.y * z)] >> 6) == -1;
                notSatisfy |= (dual_arr[(x + 1) + scale.x * (y + scale.y * (z + 1))] >> 6) == -1;
                notSatisfy |= (dual_arr[x + scale.x * (y + scale.y * (z + 1))] >> 6) == -1;

                if (!notSatisfy)
                {
                    ind_serial_arr.Add(dual_arr[index] >> 6);
                    ind_serial_arr.Add(dual_arr[(x + 1) + scale.x * (y + scale.y * z)] >> 6);
                    ind_serial_arr.Add(dual_arr[(x + 1) + scale.x * (y + scale.y * (z + 1))] >> 6);
                    ind_serial_arr.Add(dual_arr[x + scale.x * (y + scale.y * (z + 1))] >> 6);
                }
                //else { UnityEngine.Debug.Log(dual_arr[index].ToString() + " pos"); }
            }
            else
            {
                notSatisfy = false;
                notSatisfy |= (dual_arr[index] >> 6) == -1;
                notSatisfy |= ((dual_arr[(x) + scale.x * (y + scale.y * (z + 1))] >> 6)) == -1;
                notSatisfy |= ((dual_arr[(x + 1) + scale.x * (y + scale.y * (z + 1))] >> 6)) == -1;
                notSatisfy |= ((dual_arr[(x + 1) + scale.x * (y + scale.y * z)] >> 6)) == -1;

                if (!notSatisfy)
                {
                    ind_serial_arr.Add(dual_arr[index] >> 6);
                    ind_serial_arr.Add((dual_arr[(x    ) + scale.x * (y + scale.y * (z + 1))] >> 6));
                    ind_serial_arr.Add((dual_arr[(x + 1) + scale.x * (y + scale.y * (z + 1))] >> 6));
                    ind_serial_arr.Add((dual_arr[(x + 1) + scale.x * (y + scale.y * z)] >> 6));
                }
                else { UnityEngine.Debug.Log(dual_arr[index].ToString() + " neg"); }

            }
        }
        if ((dualGrid_index & 0x02) == 0x02)
        {
            //Make a quad from neighboring z cells
            if ((dualGrid_index & 0x01) == 0x01)
            {
                notSatisfy = false;
                notSatisfy |= (dual_arr[index] >> 6) == -1;
                notSatisfy |= (dual_arr[x + scale.x * ((y + 1) + scale.y * z)] >> 6) == -1;
                notSatisfy |= (dual_arr[x + scale.x * ((y + 1) + scale.y * (z + 1))] >> 6) == -1;
                notSatisfy |= (dual_arr[x + scale.x * (y + scale.y * (z + 1))] >> 6) == -1;

                if (!notSatisfy)
                {
                    ind_serial_arr.Add(dual_arr[index] >> 6);
                    ind_serial_arr.Add(dual_arr[x + scale.x * ((y + 1) + scale.y * z)] >> 6);
                    ind_serial_arr.Add(dual_arr[x + scale.x * ((y + 1) + scale.y * (z + 1))] >> 6);
                    ind_serial_arr.Add(dual_arr[x + scale.x * (y + scale.y * (z + 1))] >> 6);
                }
            }
            else
            {
                notSatisfy = false;
                notSatisfy |= (dual_arr[index] >> 6) == -1;
                notSatisfy |= (dual_arr[x + scale.x * (y + scale.y * (z + 1))] >> 6) == -1;
                notSatisfy |= (dual_arr[x + scale.x * ((y + 1) + scale.y * (z + 1))] >> 6) == -1;
                notSatisfy |= (dual_arr[x + scale.x * ((y + 1) + scale.y * z)] >> 6) == -1;

                if (!notSatisfy)
                {
                    ind_serial_arr.Add(dual_arr[index] >> 6);
                    ind_serial_arr.Add(dual_arr[x + scale.x * (y + scale.y * (z + 1))] >> 6);
                    ind_serial_arr.Add(dual_arr[x + scale.x * ((y + 1) + scale.y * (z + 1))] >> 6);
                    ind_serial_arr.Add(dual_arr[x + scale.x * ((y + 1) + scale.y * z)] >> 6);
                }
            }
        }
    }


}
