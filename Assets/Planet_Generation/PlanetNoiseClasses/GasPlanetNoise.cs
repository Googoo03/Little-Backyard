using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Simplex;

public class GasPlanetNoise : GeneratePlane
{
    // Start is called before the first frame update
    ComputeShader worley;
    public GasPlanetNoise()
    {

        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0.3f;
        oceanMulitplier = 0.1f;
        landMultiplier = 1f;
        octaves = 4;
        scale = 0.3f;
        //worleyScale = 3;
        lacunarity = 3;
        persistance = 0.9f;
        changeHeight = false;
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
    //VARIABLES ARE INHERITED. THUS, HAVE THE NOISE PARAMETERS BE CLASS VARIABLES.
    protected override void DispatchNoise(ref Vector3[] vertices, Vector3 origin)
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
        worley.SetBool("volcano_crater", true);
        worley.SetFloat("edgeThreshold", 0.9f);
        worley.SetBool("mode", false); //set to multiply mode
        worley.Dispatch(shaderHandle, xVertCount, yVertCount, 1);

        verts.Release();
    }

    public override float NoiseValue(Vector3 pos, float scale)
    {
        return 0;
    }
}
