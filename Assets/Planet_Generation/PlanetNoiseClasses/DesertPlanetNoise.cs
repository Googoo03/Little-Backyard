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
