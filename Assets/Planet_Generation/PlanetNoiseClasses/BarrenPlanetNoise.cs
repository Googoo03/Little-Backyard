using Poisson;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Worley;

public class BarrenPlanetNoise : GeneratePlane
{
    ComputeShader simplex;
    ComputeShader worley;

    private PoissonDisc poissonSampling = new PoissonDisc();

    [SerializeField] private List<GameObject> big_rock_objs = new List<GameObject>();
    [SerializeField] private List<GameObject> rock_objs = new List<GameObject>();
    public BarrenPlanetNoise() {

        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0.3f;
        oceanMulitplier = 0.05f;
        landMultiplier = 0.2f;
        octaves = 5;
        scale = 5;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;
        domainWarp = 1f;

        rock_k = 2; rock_radius = 8;
        rock_nummax = 6;
    }

    private void OnDestroy()
    {
        if (this == null) return;
        big_rock_objs.ForEach(item => { item.SetActive(false); });
        rock_objs.ForEach(item => { item.SetActive(false); });

        object_pool_manager.releasePoolObjs(ref big_rock_objs);
        object_pool_manager.releasePoolObjs(ref rock_objs);
    }

    protected override void DispatchFoliage() { }

    protected override void GenerateFoliage(ref Vector3[] vertices, Vector3 origin)
    {
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

        /*

        worley.SetBool("inverse", false);
        worley.SetBool("edgeDetect", false);
        worley.SetBool("volcano_crater", false);
        worley.SetFloat("edgeThreshold", 0.9f);
        worley.SetBool("mode", false); //set to add mode
        worley.Dispatch(shaderHandle, xVertCount, yVertCount, 1);
        */

        AsyncGPUReadback.Request(verts, OnCompleteReadback);
    }

    public override float NoiseValue(Vector3 pos, float scale) {

        /*float nx = transform.TransformPoint(pos).x;
        float ny = transform.TransformPoint(pos).y;
        float nz = transform.TransformPoint(pos).z;

        float noiseValue = worley.Calculate(nx,ny,nz,scale);
        noiseValue = EaseInOutCubic(noiseValue);
        return noiseValue;*/
        return 0;
    }
}
