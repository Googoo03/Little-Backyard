using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Worley;

public class BarrenPlanetNoise : GeneratePlane
{
    ComputeShader worley;
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
    }
    protected override void createPatchTexture(ref Material mat, int x, int y, float currentHeight)
    {
        mat.SetColor("_Land", new Color(currentHeight,currentHeight,currentHeight,1));
    }

    private float EaseInOutCubic(float x) {
        return x < 0.5 ? 4 * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 3) / 2;
    }
    //we want each planet class to have its necessary values and compute its noise values independently.
    //VARIABLES ARE INHERITED. THUS, HAVE THE NOISE PARAMETERS BE CLASS VARIABLES.
    protected override void DispatchNoise(ref Vector3[] vertices)
    {
        worley = (ComputeShader)Resources.Load("Worley Noise");


        Vector3[] worleyVerts = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; ++i)
        {
            worleyVerts[i] = transform.TransformPoint(vertices[i]);
        }

        verts = new ComputeBuffer(worleyVerts.Length, sizeof(float) * 3);
        verts.SetData(worleyVerts);

        //set worley points
        List<Vector3> points = new List<Vector3>();
        points = patch.planetObject.GetComponent<Sphere>().getWorleyPoints();

        setComputeNoiseVariables(ref worley);

        ComputeBuffer listPoints = new ComputeBuffer(points.Count, sizeof(float) * 3);
        listPoints.SetData(points);
        worley.SetBuffer(shaderHandle, "points", listPoints);

        ///////////////////////////////////



        worley.SetBool("inverse", false);
        worley.SetBool("edgeDetect", false);
        worley.SetBool("volcano_crater", false);
        worley.SetFloat("edgeThreshold", 0.9f);
        worley.SetBool("mode", false); //set to add mode
        worley.Dispatch(shaderHandle, xVertCount, yVertCount, 1);

        verts.Release();
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
