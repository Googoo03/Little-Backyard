using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Simplex;
using System;



public class LifePlanetNoise : GeneratePlane
{
    //Noise simplexNoise = new Noise();
    ComputeShader simplex;
    private int simplexHandle;
    
    Mesh mesh;

    public LifePlanetNoise()
    {
        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0;
        oceanMulitplier = 0.1f;
        landMultiplier = 0.5f;

        octaves = 3;
        scale = 0.25f;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;
    }

    private float EaseInCirc(float x) {
        return 1 - Mathf.Sqrt(1 - Mathf.Pow(x, 2));
    }

    
    protected override void createPatchTexture(ref Material mat, int x, int y, float currentHeight)
    {
        //EACH PLANET TYPE NEEDS TO HAVE INDEPENDENT TUNED PARAMETERS



        //the getcomponent lines look ugly, is there a way to clean it up?
        int regionLength = patch.planetObject.GetComponent<Sphere>().getRegionLength();
        for (int r = 0; r < regionLength - 1; r++)
        {
            float currentIndexHeight = patch.planetObject.GetComponent<Sphere>().getHeightArrayValue(r);
            float nextIndexHeight = patch.planetObject.GetComponent<Sphere>().getHeightArrayValue(r+1);


            if (currentHeight >= currentIndexHeight && currentHeight < nextIndexHeight)
            {
                Color color = patch.planetObject.GetComponent<Sphere>().getRegionColor(r);
                mat.SetColor("_Land", color);
                break;
            }
            if (r == regionLength - 2)
            {
                Color color = patch.planetObject.GetComponent<Sphere>().getRegionColor(r-1);
                mat.SetColor("_Land", color);
            }
        }
    }

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

        ComputeBuffer verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        verts.SetData(vertices);

        simplexHandle = simplex.FindKernel("CSMain");
        simplex.SetInt("seed", 0);

        simplex.SetTexture(simplexHandle, "Result", texture);
        simplex.SetBuffer(simplexHandle, "vertexBuffer", verts);

        simplex.SetFloat("octaves", octaves);
        simplex.SetFloat("persistance", persistance);
        simplex.SetFloat("lacunarity", lacunarity);

        simplex.SetFloat("oceanMultiplier", oceanMulitplier);
        simplex.SetFloat("landMultiplier", landMultiplier);
        simplex.SetFloat("seaLevel", oceanFloor);

        simplex.Dispatch(simplexHandle, xVertCount, yVertCount, 1);


        

        verts.Release();
    }
    public override float NoiseValue(Vector3 pos, float scale)
    {



        /*float noiseValue = simplexNoise.CalcPixel3D(xx, yy, zz, 1f / scale); // should return a value between 0 and 1
        noiseValue = EaseInCirc(noiseValue);
        return noiseValue;*/
        return 0;
    }
}
