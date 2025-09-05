using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using DualContour;
using System;
using UnityEngine.Rendering;
using Unity.Collections;
using Poisson;
using chunk_events;

using QuadTree;

public class DC_Chunk : MonoBehaviour
{
    private Dual_Contour dc;
    private PoissonDisc pois;
    [SerializeField] private Vector3Int scale; //how much voxel resolution each chunk receives
    [SerializeField] private int length; //how long in units each chunk is
    [SerializeField] private bool block_voxel;
    [SerializeField] private float editRadius;
    [SerializeField] private int dir;
    [SerializeField] private Vector3 max;
    [SerializeField] private Vector3 min;
    [SerializeField] private int LODLevel;
    [SerializeField] private Vector3 offset;

    [SerializeField] private Material mat;

    //Test Cube
    [SerializeField] private GameObject cube;
    [SerializeField] private GameObject tree_obj;
    public bool subdivide = false;

    //MESH DETAILS
    private Mesh m;
    private MeshFilter mf;
    private MeshRenderer rend;
    private MeshCollider coll;
    private NativeList<Vector3> vertices;
    private Vector3[] normals;
    private Vector2[] uvs;
    private NativeList<int> indices;
    private UInt16[] voxel_data;
    [SerializeField] private bool emptyMesh;

    //Voxel Data
    [SerializeField]private Texture3D tex;
    [SerializeField] private ChunkConfig chunkConfig;

    //Tree Data
    [SerializeField] private List<Vector3> tree_pos;

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
        scale = chunkConfig.scale;
        dir = chunkConfig.dir;
    }

    private void GenerateFoliage() {

        Vector3 treePos = Vector3.zero;
        tree_pos.ForEach(pos => {
            treePos = new Vector3(pos.x, FindSurface(pos), pos.z) - (scale / 2);
            treePos = dc.FindTransformedCoord(treePos, (int)treePos.y + (scale.y / 2));
            GameObject tree = Instantiate(tree_obj, transform.position + treePos, Quaternion.identity);

            //have each tree looking outward from the surface
            tree.transform.up = treePos - transform.position;
        });
    }

    public Dual_Contour GetDC() { return dc; }

    public bool HasEmptyMesh() { return emptyMesh; }

    private int FindSurface(Vector3 pos) {
        int y = scale.y-1;
        while (y > 0) {
            int index = (int)pos.x + scale.x * (y + scale.y * (int)pos.z);
            
            if (voxel_data[index] != 15) return y;
            y--;
        }
        return 0;
    }

    public void InitializeDualContourBounds() {
        if (dc == null) dc = new Dual_Contour(transform.position, chunkConfig.scale, chunkConfig.lodOffset, chunkConfig.lodLevel, length, block_voxel, editRadius, chunkConfig.dir);
        dc.InitializeBounds();
    }

    public void InitializeDualContour() {

        vertices = new NativeList<Vector3>(scale.x * scale.y * scale.z, Allocator.Persistent);
        indices = new NativeList<int>(4 * scale.x * scale.y * scale.z, Allocator.Persistent);

        vertices.Clear();
        vertices.Capacity = scale.x * scale.y * scale.z;

        indices.Clear();
        indices.Capacity = 4 * scale.x * scale.y * scale.z;

        if(dc == null) dc = new Dual_Contour(transform.position, chunkConfig.scale, chunkConfig.lodOffset, chunkConfig.lodLevel, length, block_voxel, editRadius, chunkConfig.dir);

        dc.InitializeGrid(chunkConfig.seam, ref vertices, ref indices);

        min = dc.GetMin();
        max = dc.GetMax();
        LODLevel = dc.GetLODLevel();
        
    }

    public void GenerateDCMesh() {

        //REASSIGN MESH RENDERING COMPONENTS.
        if (!rend) rend = this.gameObject.AddComponent<MeshRenderer>();


        if (!mf) mf = this.gameObject.AddComponent<MeshFilter>();

        if (m) m.Clear();
        m = mf.sharedMesh = new Mesh();
        //////////////////////////////////////

        uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; ++i)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }

        //CreateDataTexture();

        mat.SetTexture("_VoxelData", tex);
        rend.material = mat;

        //If nothing is generated,dont bother setting vertices and indices.
        emptyMesh = vertices.Length == 0 || indices.Length < 3;
        if (emptyMesh) return;
        //----------------------------------------------------------------

        Vector3[] newverts = new Vector3[vertices.Length];
        vertices.AsArray().CopyTo(newverts);
        m.vertices = newverts;
        m.normals = normals;


        //IF WE WANT TEXTURES, WE HAVE TO CALCULATE THE UVS MANUALLY
        m.uv = uvs;
        ////////////////////////////////////////////////////////////
        if (indices.Length < 3) return;


        m.SetIndices(indices.AsArray(), MeshTopology.Triangles, 0);
        m.RecalculateBounds();
        m.RecalculateNormals();

        if (!coll)
        {
            coll = this.gameObject.AddComponent<MeshCollider>();
        }
        else {
            coll.sharedMesh = null;
            coll.sharedMesh = mf.sharedMesh;
        }

        //vertices.Dispose();
        //indices.Dispose();

    }




    public void UpdateChunk(ref List<chunk_event> points) {
        dc.UpdateVoxelData(ref points);
        GenerateDCMesh();
    }

    private void CreateDataTexture() {
        tex = new Texture3D(scale.x, scale.y, scale.z, TextureFormat.R16, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixelData(voxel_data, 0, 0);
        tex.Apply();
    }

    public void SetChunkConfig( ChunkConfig ichunkConfig) { chunkConfig = ichunkConfig; }

    public void SetOffset(Vector3 ioffset) { offset = ioffset; }

    private void InitComputeShader() {

        

        verts = new ComputeBuffer(scale.x*scale.y*scale.z, sizeof(float) * 3);
        dualGrid = new ComputeBuffer(scale.x * scale.y * scale.z, sizeof(int));

        int shaderHandle = DC_Compute.FindKernel("CSMain");
        int simplex_handle = Simplex.FindKernel("CSMain");

        DC_Compute.SetBuffer(shaderHandle, "vertexBuffer", verts);
        DC_Compute.SetVector("dimensions", (Vector3)scale);
        DC_Compute.SetInt("CELL_SIZE", length);
        DC_Compute.SetVector("global", transform.position);
        DC_Compute.SetBuffer(shaderHandle, "dualGrid", dualGrid);
        DC_Compute.SetBool("block_voxel", block_voxel);

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
