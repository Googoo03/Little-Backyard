using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Simplex;
using System;
using Poisson;



public class LifePlanetNoise : GeneratePlane
{
    //Noise simplexNoise = new Noise();
    ComputeShader simplex;
    //private int simplexHandle;
    [SerializeField] private Mesh tree_mesh;
    [SerializeField] private Material tree_mat;

    private PoissonDisc poissonSampling = new PoissonDisc(); //used for generating foliage
    private List<Matrix4x4> tree_m = new List<Matrix4x4>(30);
    Mesh mesh;

    public LifePlanetNoise()
    {
        //set up noise parameters. surely theres a better way to do this


        oceanFloor = 0;
        oceanMulitplier = 0.1f;
        landMultiplier = 0.15f;

        octaves = 20;
        scale = 3f;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;
    }

    private float EaseInCirc(float x) {
        return 1 - Mathf.Sqrt(1 - Mathf.Pow(x, 2));
    }


    protected override void createPatchTexture(ref Material mat, int x, int y, float currentHeight) { }


    protected override void GenerateFoliage(ref Vector3[] vertices, Vector3 origin) {
        //POISSON DISC DISTRIBUTION OF TREE MESHES. SETS TEH MATRICES FOR POSITION, ROTATION, AND SCALE.
        tree_mesh = (Mesh)(Resources.Load<GameObject>("Tree/Tree").GetComponent<MeshFilter>().sharedMesh);
        tree_mat = (Material)(Resources.Load("Tree/Tree_Mat"));
        

        List<Vector3> positions = new List<Vector3>(tree_m.Capacity);
        poissonSampling.setSeedPRNG(generateUniqueSeed(vertices[xVertCount*yVertCount/2]));
        poissonSampling.generatePoissonDisc(ref positions, ref vertices, tree_k, 256, xVertCount, yVertCount, tree_radius);

        for (int i = 0; i < positions.Count; ++i) {
            //Vector3 pos = new Vector3(i, 300, i);
            Vector3 lookVec = new Vector3(positions[i].x, positions[i].y, positions[i].z);
            Quaternion rot = Quaternion.LookRotation(-lookVec)/* * Quaternion.Euler(90,0,0)*/; //might have to change later
            Vector3 sca = Vector3.one * .01f;
            tree_m.Add(Matrix4x4.TRS(positions[i]+origin,rot,sca)); //transform rotation scale
        }
        return;

    }

    protected override void DispatchFoliage() {
        //sends over to the gpu
        Graphics.DrawMeshInstanced(tree_mesh, 0, tree_mat, tree_m); //does global position, needs local position. Convert local to global
    }

    protected override void DispatchNoise(ref Vector3[] vertices) {

        simplex = (ComputeShader)(Resources.Load("Simplex Noise"));
        Vector3[] verticesWorldSpace = new Vector3[vertices.Length];

        //////CONVERTS RELATIVE VERTEX POINTS INTO WORLD SPACE POSITIONS
        for (int i = 0; i < vertices.Length; ++i)
        {
            Vector3 pos = vertices[i];
            float nx = transform.TransformPoint(pos).x;
            float ny = transform.TransformPoint(pos).y;
            float nz = transform.TransformPoint(pos).z;

            float xx = ((nx - xVertCount) / scale);
            float yy = ((ny - yVertCount) / scale);
            float zz = ((nz - xVertCount) / scale);

            verticesWorldSpace[i] = new Vector3(xx,yy,zz);
        }
        /////////////////////////////////////////////////////////////////

        verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        verts.SetData(vertices);

        worldVerts = new ComputeBuffer(verticesWorldSpace.Length, sizeof(float) * 3);
        worldVerts.SetData(verticesWorldSpace);

        setComputeNoiseVariables(ref simplex);
        simplex.SetBool("absValue", false);
        

        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);

        simplex.SetFloat("seed", 100);
        simplex.SetFloat("mountainStrength", 2f);
        simplex.SetFloat("persistance", 0.8f);
        simplex.SetFloat("lacunarity", 0.25f);
        simplex.SetInt("octaves", 3);
        simplex.SetBool("absValue", true);

        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);
        
        
        verts.GetData(vertices);


        verts.Release();
    }

    public override float NoiseValue(Vector3 pos, float scale) { return 0; }
}
