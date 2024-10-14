using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Worley;

public class IcePlanetNoise : GeneratePlane
{
    // Start is called before the first frame update
    public ComputeShader worley;
    ComputeShader simplex;


    public IcePlanetNoise()
    {
        
        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0.2f;
        oceanMulitplier = 0.15f;
        landMultiplier = 0.2f;
        octaves = 2;
        scale = 3f;
        lacunarity = 2;
        persistance = 0.5f;
        domainWarp = 0.5f;

        changeHeight = true;
    }

    /*protected override void createPatchTexture(ref Material mat, int x, int y, float currentHeight)
    {
        int regionLength = patch.planetObject.GetComponent<Sphere>().getRegionLength();
        for (int r = 0; r < regionLength - 1; r++)
        {
            float currentIndexHeight = patch.planetObject.GetComponent<Sphere>().getHeightArrayValue(r);
            float nextIndexHeight = patch.planetObject.GetComponent<Sphere>().getHeightArrayValue(r + 1);


            if (currentHeight >= currentIndexHeight && currentHeight < nextIndexHeight)
            {
                Color color = patch.planetObject.GetComponent<Sphere>().getRegionColor(r);
                //tex.SetPixel(x, y, color);
                break;
            }
            if (r == regionLength - 2)
            {
                Color color = patch.planetObject.GetComponent<Sphere>().getRegionColor(r - 1);
                //tex.SetPixel(x, y, color);
            }
        }
    }*/

    protected override void DispatchFoliage() { }

    protected override void GenerateFoliage(ref Vector3[] vertices, Vector3 origin) { }

    private float EaseOutExpo(float x)
    {
        return x == 1 ? 1 : 1 - Mathf.Pow(2, -10 * x);
    }
    //we want each planet class to have its necessary values and compute its noise values independently.
    //VARIABLES ARE INHERITED. THUS, HAVE THE NOISE PARAMETERS BE CLASS VARIABLES.worley.
    protected override void DispatchNoise(ref Vector3[] vertices, Vector3 origin)
    {

        worley = (ComputeShader)Resources.Load("Worley Noise");
        simplex = (ComputeShader)Resources.Load("Simplex Noise");

        Vector3[] verticesWorldSpace = new Vector3[vertices.Length];

        //////CONVERTS RELATIVE VERTEX POINTS INTO WORLD SPACE POSITIONS
        for (int i = 0; i < vertices.Length; ++i)
        {
            Vector3 pos = origin + vertices[i]; //world space

            float xx = ((pos.x - xVertCount) / scale);
            float yy = ((pos.y - yVertCount) / scale);
            float zz = ((pos.z - xVertCount) / scale);

            verticesWorldSpace[i] = new Vector3(xx, yy, zz);
        }
        /////////////////////////////////////////////////////////////////


        verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        verts.SetData(vertices);

        worldVerts = new ComputeBuffer(verticesWorldSpace.Length, sizeof(float) * 3);
        worldVerts.SetData(verticesWorldSpace);

        //set worley points
        List<Vector3> points = new List<Vector3>();
        points = patch.planetObject.GetComponent<Sphere>().getWorleyPoints();

        //setComputeNoiseVariables(ref worley);

        //ComputeBuffer listPoints = new ComputeBuffer(points.Count, sizeof(float) * 3);
        //listPoints.SetData(points);
        //worley.SetBuffer(shaderHandle, "points", listPoints);

        ///////////////////////////////////



        /*worley.SetBool("inverse", false);
        worley.SetBool("edgeDetect", false);
        worley.SetFloat("edgeThreshold", 0.1f);
        worley.SetBool("mode", false); //set to add mode
        worley.Dispatch(shaderHandle, xVertCount, yVertCount, 1);*/

        setComputeNoiseVariables(ref simplex);
        simplex.SetBool("absValue", false);

        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);

        AsyncGPUReadback.Request(verts, OnCompleteReadback);
    }
    public override float NoiseValue(Vector3 pos, float scale)
    {

        float nx = transform.TransformPoint(pos).x;
        float ny = transform.TransformPoint(pos).y;
        float nz = transform.TransformPoint(pos).z;

        /*float noiseValue = worley.Calculate(nx, ny, nz, worleyScale);
        noiseValue = noiseValue < 0.2f ? 0 : noiseValue;
        return noiseValue;*/
        return 0;
    }
}
