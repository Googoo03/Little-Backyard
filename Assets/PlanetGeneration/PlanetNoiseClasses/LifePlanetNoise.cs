using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Simplex;
using System;



public class LifePlanetNoise : GeneratePlane
{
    //Noise simplexNoise = new Noise();
    ComputeShader simplex;
    //private int simplexHandle;
    


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

    protected override void DispatchNoise(ref Vector3[] vertices) {
        simplex = (ComputeShader)(Resources.Load("Simplex Noise"));

        for (int i = 0; i < vertices.Length; ++i)
        {
            Vector3 pos = vertices[i];
            float nx = transform.TransformPoint(pos).x;
            float ny = transform.TransformPoint(pos).y;
            float nz = transform.TransformPoint(pos).z;

            float xx = ((nx - xVertCount) / scale);
            float yy = ((ny - yVertCount) / scale);
            float zz = ((nz - xVertCount) / scale);

            vertices[i] = new Vector3(xx,yy,zz);
        }

        verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        verts.SetData(vertices);

        setComputeNoiseVariables(ref simplex);
        simplex.SetBool("absValue", false);
        

        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);

        simplex.SetFloat("seed", 100);
        simplex.SetFloat("mountainStrength", 5f);
        simplex.SetFloat("persistance", 0.8f);
        simplex.SetFloat("lacunarity", 0.25f);
        simplex.SetInt("octaves", 3);
        simplex.SetBool("absValue", true);

        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);

        verts.Release();
    }

    public override float NoiseValue(Vector3 pos, float scale) { return 0; }
}
