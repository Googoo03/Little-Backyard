using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using DualContour;
using System;

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
        dc.Generate(ref vertices, ref indices);

        m.vertices = vertices.ToArray();
        m.normals = normals;
        m.uv = uvs;
        m.SetIndices(indices, MeshTopology.Quads, 0);
        m.RecalculateBounds();
        m.RecalculateNormals();

        rend.material = mat;



        stopwatch.Stop();
        UnityEngine.Debug.Log("Took " + stopwatch.ElapsedMilliseconds.ToString() + " milliseconds");
    }
}
