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
        oceanMulitplier = 0.5f;
        landMultiplier = 1f;
        octaves = 1;
        scale = 5;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;

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
        rock_mesh = (Resources.Load<GameObject>("Rock/Rock_Prefab").GetComponent<MeshFilter>().sharedMesh);
        rock_mat = (Material)(Resources.Load("Rock/Rock_Mat"));

        Mesh big_rock_mesh = (Resources.Load<GameObject>("Big_Rock/Big_Rock_Prefab").GetComponent<MeshFilter>().sharedMesh);
        Material big_rock_mat = (Material)(Resources.Load("Big_Rock/Big_Rock_Mat"));

        List<Vector3> big_rock_positions = new List<Vector3>();
        List<Vector3> rock_positions = new List<Vector3>();

        int seed;
        int mid_index = xVertCount * (yVertCount / 2) + (xVertCount / 2); //calculates the middle index of a square array. Like, direct center of square.
        poissonSampling.setDensity(3f);

        poissonSampling.setDensity(1);
        seed = generateUniqueSeed(vertices[mid_index]);
        poissonSampling.setSeedPRNG(seed);
        poissonSampling.generatePoissonDisc(ref big_rock_positions, ref vertices, 2, xVertCount * yVertCount, xVertCount, yVertCount, 10);

        poissonSampling.setDensity(2f);
        seed = generateUniqueSeed(vertices[mid_index] + new Vector3(1, 0, 0));
        poissonSampling.setSeedPRNG(seed);
        poissonSampling.generatePoissonDisc(ref rock_positions, ref vertices, rock_k, rock_nummax, xVertCount, yVertCount, rock_radius);

        //Remove positions that do not meet criteria
        int t = 0;
        while (t < big_rock_positions.Count)
        {
            if (big_rock_positions[t].magnitude > (radius + oceanFloor * 2))
            {
                big_rock_positions.RemoveAt(t);
                t--;
            }
            t++;
        }


        //Request objects from the object pool (1 frame buffer)
        object_pool_manager.requestPoolObjs(ref big_rock_objs, big_rock_positions.Count);

        for (int i = 0; i < big_rock_objs.Count; ++i)
        { 

            Vector3 lookVec = new Vector3(big_rock_positions[i].x, big_rock_positions[i].y, big_rock_positions[i].z);

            Quaternion rot = Quaternion.LookRotation(lookVec);
            Vector3 sca = Vector3.one * .02f;

            big_rock_objs[i].SetActive(true);
            big_rock_objs[i].transform.position = big_rock_positions[i] + origin;
            big_rock_objs[i].transform.rotation = rot;
            big_rock_objs[i].transform.localScale = sca;
            big_rock_objs[i].GetComponent<MeshFilter>().mesh = big_rock_mesh;
            big_rock_objs[i].GetComponent<MeshRenderer>().material = big_rock_mat;
        }


        object_pool_manager.requestPoolObjs(ref rock_objs, rock_positions.Count);

        for (int i = 0; i < rock_objs.Count; ++i)
        { 

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
    }

    protected override void DispatchNoise(Vector3[] vertices, Vector3 origin)
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
