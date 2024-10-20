using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Simplex;
using System;
using Poisson;
using Unity.Collections;
using UnityEditor.Rendering.LookDev;
using UnityEngine.Assertions;



public class LifePlanetNoise : GeneratePlane
{
    ComputeShader simplex;

    [SerializeField] private Mesh grass_mesh;
    [SerializeField] private Material grass_mat;

    private PoissonDisc poissonSampling = new PoissonDisc(); //used for generating foliage

    [SerializeField] private List<GameObject> tree_objs = new List<GameObject>();
    [SerializeField] private List<GameObject> rock_objs = new List<GameObject>();

    private List<Matrix4x4> grass_m = new List<Matrix4x4>(1);

    Mesh mesh;



    public LifePlanetNoise()
    {
        //set up noise parameters. surely theres a better way to do this
        //perhaps load from a JSON file?
        



        oceanFloor = 0.1f;
        oceanMulitplier = 0.1f;
        landMultiplier = 0.15f;
        domainWarp = 0.2f;

        octaves = 5;
        scale = 3f;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;

        tree_k = 5;
        tree_radius = 12;

        rock_k = 2; rock_radius = 8;
        rock_nummax = 6;
    }

    

    private float EaseInCirc(float x) {
        return 1 - Mathf.Sqrt(1 - Mathf.Pow(x, 2));
    }


    ~LifePlanetNoise() {

        //this is where we should release all object pool items that were allocated
        
    }
    //protected override void createPatchTexture(ref Material mat, int x, int y, float currentHeight) { }


    protected override void GenerateFoliage(ref Vector3[] vertices, Vector3 origin) {

        return;

    }

    protected override void DispatchFoliage() {
        //sends over to the gpu
        if (!foliageGenerationReturned) return;

        //Graphics.DrawMeshInstanced(grass_mesh, 0, grass_mat, grass_m);
    }

    protected override void DispatchNoise( ref Vector3[] vertices, Vector3 origin) {
        
        simplex = (ComputeShader)(Resources.Load("Simplex Noise"));
        Vector3[] verticesWorldSpace = new Vector3[vertices.Length];

        //////CONVERTS RELATIVE VERTEX POINTS INTO WORLD SPACE POSITIONS
        for (int i = 0; i < vertices.Length; ++i)
        {
            Vector3 pos = origin + vertices[i]; //world space

            float xx = ((pos.x - xVertCount) / scale);
            float yy = ((pos.y - yVertCount) / scale);
            float zz = ((pos.z - xVertCount) / scale);

            verticesWorldSpace[i] = new Vector3(xx,yy,zz);
        }
        /////////////////////////////////////////////////////////////////

        verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        verts.SetData(vertices);

        worldVerts = new ComputeBuffer(verticesWorldSpace.Length, sizeof(float) * 3);
        worldVerts.SetData(verticesWorldSpace);

        setComputeNoiseVariables(ref simplex);
        simplex.SetBool("absValue", true);

        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);
        

        //simplex.SetInt("seed", seed);
        simplex.SetFloat("mountainStrength", 1f);
        simplex.SetFloat("persistance", 0.95f);
        simplex.SetFloat("lacunarity", 0.25f);
        
        simplex.SetFloat("domainWarp", .0f);
        simplex.SetInt("octaves", 3);
        //simplex.SetInt("mOctaves", 1);
        //simplex.SetBool("absValue", false);

        //simplex.SetBool("absValue", true);

        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);

        AsyncGPUReadback.Request(verts, OnCompleteReadback);
    }

    public override float NoiseValue(Vector3 pos, float scale) { return 0; }
}
