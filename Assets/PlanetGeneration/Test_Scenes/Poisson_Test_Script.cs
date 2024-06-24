using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Poisson;
using System;

public class Poisson_Test_Script : MonoBehaviour
{
    [SerializeField] private Mesh tree_mesh;
    [SerializeField] private Material tree_mat;
    private List<Matrix4x4> tree_m = new List<Matrix4x4>(50);
    [SerializeField] private int seed;
    [SerializeField] private int k;
    [SerializeField] private int num;
    [SerializeField] private int radius;

    PoissonDisc poissonSampling = new PoissonDisc();
    // Start is called before the first frame update
    void Start()
    {
        poissonSampling.setSeedPRNG(seed);
        GenerateFoliage();
    }

    // Update is called once per frame
    void Update()
    {
        DispatchFoliage();
    }

    void GenerateFoliage()
    {
        //POISSON DISC DISTRIBUTION OF TREE MESHES. SETS TEH MATRICES FOR POSITION, ROTATION, AND SCALE.
        tree_mesh = (Mesh)(Resources.Load<GameObject>("Tree/Tree").GetComponent<MeshFilter>().sharedMesh);
        tree_mat = (Material)(Resources.Load("Tree/Tree_Mat"));

        
        List<Vector3> positions = new List<Vector3>(tree_m.Capacity);
        poissonSampling.generatePoissonDisc(ref positions,k,num,100,100,radius);

        for (int i = 0; i < positions.Count; ++i)
        {
            Vector3 pos = positions[i];
            
            Quaternion rot = Quaternion.Euler(-90,UnityEngine.Random.Range(0,180),0).normalized; //might have to change later
            Vector3 sca = new Vector3(1, 1, 1/* * UnityEngine.Random.Range(1, 5)*/);
            tree_m.Add(Matrix4x4.TRS(pos, rot, sca)); //transform rotation scale
        }
        return;

    }

    void DispatchFoliage()
    {
        //sends over to the gpu
        Graphics.DrawMeshInstanced(tree_mesh, 0, tree_mat, tree_m);
    }
}
