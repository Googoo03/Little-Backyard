using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Simplex;
using System;
using Poisson;
using Unity.Collections;



public class LifePlanetNoise : GeneratePlane
{
    //Noise simplexNoise = new Noise();
    ComputeShader simplex;
    //private int simplexHandle;
    [SerializeField] private Mesh tree_mesh;
    [SerializeField] private Material tree_mat;

    [SerializeField] private Mesh rock_mesh;
    [SerializeField] private Material rock_mat;

    [SerializeField] private Mesh grass_mesh;
    [SerializeField] private Material grass_mat;

    private PoissonDisc poissonSampling = new PoissonDisc(); //used for generating foliage
    private List<Matrix4x4> tree_m = new List<Matrix4x4>(1);
    private List<Matrix4x4> rock_m = new List<Matrix4x4>(1);
    private List<Matrix4x4> grass_m = new List<Matrix4x4>(1);
    Mesh mesh;

    public LifePlanetNoise()
    {
        //set up noise parameters. surely theres a better way to do this
        

        oceanFloor = 0.1f;
        oceanMulitplier = 0.1f;
        landMultiplier = 0.15f;

        octaves = 6;
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
    }
    protected override void createPatchTexture(ref Material mat, int x, int y, float currentHeight) { }


    protected override void GenerateFoliage(ref Vector3[] vertices, Vector3 origin) {
        //POISSON DISC DISTRIBUTION OF TREE MESHES. SETS TEH MATRICES FOR POSITION, ROTATION, AND SCALE.
        tree_mesh = (Resources.Load<GameObject>("Tree/Tree").GetComponent<MeshFilter>().sharedMesh);
        tree_mat = (Material)(Resources.Load("Tree/Tree_Mat"));

        rock_mesh = (Resources.Load<GameObject>("Rock/Rock").GetComponent<MeshFilter>().sharedMesh);
        rock_mat = (Material)(Resources.Load("Rock/Rock_Mat"));

        grass_mesh = (Resources.Load<GameObject>("Grass/Grass").GetComponent<MeshFilter>().sharedMesh);
        grass_mat = (Material)(Resources.Load("Grass/Grass_Mat"));


        List<Vector3> tree_positions = new List<Vector3>(tree_m.Capacity);
        List<Vector3> rock_positions = new List<Vector3>(rock_m.Capacity);
        List<Vector3> grass_positions = new List<Vector3>(grass_m.Capacity);

        int seed;
        int mid_index = xVertCount * (yVertCount / 2) + (xVertCount / 2); //calculates the middle index of a square array. Like, direct center of square.
        

        seed = generateUniqueSeed(vertices[mid_index]);
        poissonSampling.setSeedPRNG(seed);
        poissonSampling.generatePoissonDisc(ref tree_positions, ref vertices, tree_k, xVertCount*yVertCount, xVertCount, yVertCount, tree_radius);

        seed = generateUniqueSeed(vertices[mid_index] + new Vector3(1, 0, 0));
        poissonSampling.setSeedPRNG(seed);
        poissonSampling.generatePoissonDisc(ref rock_positions, ref vertices, rock_k, xVertCount * yVertCount, xVertCount, yVertCount, rock_radius);

        poissonSampling.setSeedPRNG(generateUniqueSeed(vertices[xVertCount * yVertCount / 2] + new Vector3(2, 0, 0)));
        poissonSampling.generatePoissonDisc(ref grass_positions, ref vertices, 10, xVertCount * yVertCount, xVertCount, yVertCount, 2);

        for (int i = 0; i < tree_positions.Count; ++i) { //add the tree positions and subsequent rotations to the matrix buffer
            
            Vector3 lookVec = new Vector3(tree_positions[i].x, tree_positions[i].y, tree_positions[i].z);
            if (lookVec.magnitude < (radius+oceanFloor*2)) continue; //dont generate in water
            Quaternion rot = Quaternion.LookRotation(-lookVec);
            Vector3 sca = Vector3.one * .01f;
            tree_m.Add(Matrix4x4.TRS(tree_positions[i]+origin,rot,sca)); //transform rotation scale
        }

        for (int i = 0; i < rock_positions.Count; ++i)
        { //add the tree positions and subsequent rotations to the matrix buffer

            Vector3 lookVec = new Vector3(rock_positions[i].x, rock_positions[i].y, rock_positions[i].z);
            
            Quaternion rot = Quaternion.LookRotation(lookVec) * Quaternion.Euler(0,0,UnityEngine.Random.Range(0,180));
            Vector3 sca = Vector3.one * .01f;
            rock_m.Add(Matrix4x4.TRS(rock_positions[i] + origin, rot, sca)); //transform rotation scale
        }
        for (int i = 0; i < grass_positions.Count; ++i)
        { //add the tree positions and subsequent rotations to the matrix buffer

            Vector3 lookVec = grass_positions[i];
            if (lookVec.magnitude < radius+0.15f || lookVec.magnitude > radius + 0.2f) continue; //corresponds to level1 in the shader. These need to communicate with one another
            Quaternion rot = Quaternion.LookRotation(lookVec) * Quaternion.Euler(0, 0, 90);
            Vector3 sca = Vector3.one * .01f;
            grass_m.Add(Matrix4x4.TRS(grass_positions[i] + origin, rot, sca)); //transform rotation scale
        }
        return;

    }

    protected override void DispatchFoliage() {
        //sends over to the gpu
        Graphics.DrawMeshInstanced(tree_mesh, 0, tree_mat, tree_m);
        Graphics.DrawMeshInstanced(rock_mesh, 0, rock_mat, rock_m);
        Graphics.DrawMeshInstanced(grass_mesh, 0, grass_mat, grass_m);
    }

    protected override void DispatchNoise(Vector3[] vertices, Vector3 origin) {
        
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
        simplex.SetBool("absValue", false);

        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);
        

        simplex.SetFloat("seed", 100);
        simplex.SetFloat("mountainStrength", 5f);
        simplex.SetFloat("persistance", 0.95f);
        simplex.SetFloat("lacunarity", 0.25f);
        simplex.SetInt("octaves", 3);
        simplex.SetBool("absValue", false);

        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);

        //var req = await Task.Run(() => AsyncGPUReadback.Request(verts));
        //await Task.Run(() => verts.GetData(vertices));


        //verts.GetData(vertices);
        AsyncGPUReadback.Request(verts, OnCompleteReadback);
        //vertices = StartCoroutine(getVertData(verts));// getVertData(verts);

        //AsyncGPUReadbackRequest.GetData();

        
        
        
    }

    public override float NoiseValue(Vector3 pos, float scale) { return 0; }
}
