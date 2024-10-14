using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Simplex;
using Worley;
using UnityEngine.Rendering;
using Poisson;
using UnityEngine.Assertions;

public class DesertPlanetNoise : GeneratePlane
{
    ComputeShader simplex;
    ComputeShader worley;

    private PoissonDisc poissonSampling = new PoissonDisc();

    [SerializeField] private List<GameObject> tree_objs = new List<GameObject>();
    [SerializeField] private List<GameObject> rock_objs = new List<GameObject>();
    [SerializeField] private List<GameObject> bush_objs = new List<GameObject>();

    int worleyScale;

    public DesertPlanetNoise()
    {
        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0;
        oceanMulitplier = .1f;
        landMultiplier = .3f;

        octaves = 5;
        scale = 5f;
        //worleyScale = 4;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;
        domainWarp = 0.2f;

        tree_k = 3;
        tree_radius = 8;
        tree_nummax = 1;

        rock_k = 3;
        rock_radius = 8;
        rock_nummax = 1;
    }

    private void OnDestroy()
    {
        if (this == null) return;
        tree_objs.ForEach(item => { item.SetActive(false); });
        rock_objs.ForEach(item => { item.SetActive(false); });
        bush_objs.ForEach(item => { item.SetActive(false); });

        if (object_pool_manager)
        {
            object_pool_manager.releasePoolObjs(ref tree_objs);
            object_pool_manager.releasePoolObjs(ref rock_objs);
            object_pool_manager.releasePoolObjs(ref bush_objs);
        }
    }

    protected override void DispatchFoliage() { }

    protected override void GenerateFoliage(ref Vector3[] vertices, Vector3 origin) {
        tree_mesh = (Resources.Load<GameObject>("Cactus/Cactus_Prefab").GetComponent<MeshFilter>().sharedMesh);
        tree_mat = (Material)(Resources.Load("Cactus/Cactus_Mat"));

        rock_mesh = (Resources.Load<GameObject>("Rock/Rock_Prefab").GetComponent<MeshFilter>().sharedMesh);
        rock_mat = (Material)(Resources.Load("Rock/Rock_Mat"));

        Mesh bush_mesh = (Resources.Load<GameObject>("Dead_Bush/Dead_Bush_Prefab").GetComponent<MeshFilter>().sharedMesh);
        Material bush_mat = (Material)(Resources.Load("Dead_Bush/Dead_Bush_Mat"));


        List<Vector3> tree_positions = new List<Vector3>();
        List<Vector3> rock_positions = new List<Vector3>();
        List<Vector3> bush_positions = new List<Vector3>();

        int seed;
        int mid_index = xVertCount * (yVertCount / 2) + (xVertCount / 2); //calculates the middle index of a square array. Like, direct center of square.
        poissonSampling.setDensity(2f);

        seed = generateUniqueSeed(vertices[mid_index]);
        poissonSampling.setSeedPRNG(seed);
        poissonSampling.generatePoissonDisc(ref tree_positions, ref vertices, tree_k, tree_nummax, xVertCount, yVertCount, tree_radius);

        seed = generateUniqueSeed(vertices[mid_index] + new Vector3(1, 0, 0));
        poissonSampling.setSeedPRNG(seed);
        poissonSampling.generatePoissonDisc(ref rock_positions, ref vertices, rock_k, rock_nummax, xVertCount, yVertCount, rock_radius);


        poissonSampling.setDensity(2.5f);
        seed = generateUniqueSeed(vertices[mid_index] + new Vector3(2, 0, 0));
        poissonSampling.setSeedPRNG(seed);
        poissonSampling.generatePoissonDisc(ref bush_positions, ref vertices, 3, 6, xVertCount, yVertCount, 6);

        //Request objects from the object pool (1 frame buffer)
        Assert.IsTrue(object_pool_manager, "Object Pool Manager is not set");
        object_pool_manager.requestPoolObjs(ref tree_objs, tree_positions.Count);

        for (int i = 0; i < tree_objs.Count; ++i)
        { //add the tree positions and subsequent rotations to the matrix buffer

            Vector3 lookVec = new Vector3(tree_positions[i].x, tree_positions[i].y, tree_positions[i].z);

            Quaternion rot = Quaternion.LookRotation(lookVec) * Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 180));
            Vector3 sca = Vector3.one * .015f;

            tree_objs[i].SetActive(true);
            tree_objs[i].transform.position = tree_positions[i] + origin;
            tree_objs[i].transform.rotation = rot;
            tree_objs[i].transform.localScale = sca;
            tree_objs[i].GetComponent<MeshFilter>().mesh = tree_mesh;
            tree_objs[i].GetComponent<MeshRenderer>().material = tree_mat;
        }

        //REQUEST OBJECTS FOR ROCKS-----------------------------------------------
        object_pool_manager.requestPoolObjs(ref rock_objs, rock_positions.Count);

        for (int i = 0; i < rock_objs.Count; ++i)
        { //add the tree positions and subsequent rotations to the matrix buffer

            Vector3 lookVec = new Vector3(rock_positions[i].x, rock_positions[i].y, rock_positions[i].z);

            Quaternion rot = Quaternion.LookRotation(lookVec) * Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 180));
            Vector3 sca = Vector3.one * .01f;
            rock_objs[i].SetActive(true);
            rock_objs[i].transform.position = rock_positions[i] + origin;
            rock_objs[i].transform.rotation = rot;
            rock_objs[i].transform.localScale = sca;
            rock_objs[i].GetComponent<MeshFilter>().mesh = rock_mesh;
            rock_objs[i].GetComponent<MeshRenderer>().material = rock_mat;
        }
        //-----------------------------------------------------------------------

        //REQUEST OBJECTS FOR DEAD BUSHES----------------------------------------
        object_pool_manager.requestPoolObjs(ref bush_objs, bush_positions.Count);

        for (int i = 0; i < bush_objs.Count; ++i)
        { //add the tree positions and subsequent rotations to the matrix buffer

            Vector3 lookVec = bush_positions[i];

            Quaternion rot = Quaternion.LookRotation(lookVec) * Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 180));
            Vector3 sca = Vector3.one * .002f;
            bush_objs[i].SetActive(true);
            bush_objs[i].transform.position = bush_positions[i] + origin;
            bush_objs[i].transform.rotation = rot;
            bush_objs[i].transform.localScale = sca;
            bush_objs[i].GetComponent<MeshFilter>().mesh = bush_mesh;
            bush_objs[i].GetComponent<MeshRenderer>().material = bush_mat;
        }
        //-----------------------------------------------------------------------

        return;
    }

    protected override void DispatchNoise(ref Vector3[] vertices, Vector3 origin)
    {
        simplex = (ComputeShader)(Resources.Load("Simplex Noise"));
        worley = (ComputeShader)Instantiate(Resources.Load("Worley Noise"));

        Vector3[] simplexVerts = new Vector3[vertices.Length];
        Vector3[] worleyVerts = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; ++i)
        {
            Vector3 pos = origin + vertices[i]; //world space

            float xx = ((pos.x - xVertCount) / scale);
            float yy = ((pos.y - yVertCount) / scale);
            float zz = ((pos.z - xVertCount) / scale);

            simplexVerts[i] = new Vector3(xx, yy, zz);
            worleyVerts[i] = pos;
        }

        //SIMPLEX NOISE
        verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        verts.SetData(vertices);

        worldVerts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        worldVerts.SetData(simplexVerts);

        setComputeNoiseVariables(ref simplex);
        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);
        ////////////////

        //verts.SetData(worleyVerts);
        //setComputeNoiseVariables(ref worley);

        //initialize points vector array
        List<Vector3> points = new List<Vector3>();
        points = patch.planetObject.GetComponent<Sphere>().getWorleyPoints();
        //set random points

        //EACH PLANE MAKES A DIFFERENT SET OF POINTS



        ComputeBuffer listPoints = new ComputeBuffer(points.Count, sizeof(float) * 3);
        listPoints.SetData(points);

        worley.SetBuffer(shaderHandle,"points", listPoints);
        worley.SetBool("inverse", false);
        worley.SetBool("mode", true); //set to mulitply mode
        //worley.SetFloat("scale", 4.0f);
        //worley.SetInt("octaves", 1);
        //worley.Dispatch(shaderHandle, xVertCount, yVertCount, 1);
        listPoints.Release();

        AsyncGPUReadback.Request(verts, OnCompleteReadback);

        //verts.Release();
        
    }

    public override float NoiseValue(Vector3 pos, float scale)
    {

        float nx = transform.TransformPoint(pos).x;
        float ny = transform.TransformPoint(pos).y;
        float nz = transform.TransformPoint(pos).z;

        float xx = ((nx - xVertCount) / scale) * frequency;
        float yy = ((ny - yVertCount) / scale) * frequency;
        float zz = ((nz - xVertCount) / scale) * frequency;

        //float noiseValue = simplexNoise.CalcPixel3D(xx, yy, zz, 1f / scale); // should return a value between 0 and 1
        //noiseValue += (worleyNoise.Calculate(nx, ny, nz, worleyScale))*0.1f;

        return 0;
    }
}
