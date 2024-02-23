using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Simplex;
using Worley;

public class HotPlanetNoise : GeneratePlane
{
    // Start is called before the first frame update
    Noise simplexNoise = new Noise();
    ComputeShader worley;

    public HotPlanetNoise()
    {
        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0.3f;
        oceanMulitplier = 0.08f;
        landMultiplier = 0.02f;

        octaves = 4;
        scale = 0.4f;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;
    }
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
        worley.SetBool("volcano_crater", true);
        worley.SetFloat("edgeThreshold", 0.9f);
        worley.SetBool("mode", false); //set to add mode
        worley.Dispatch(shaderHandle, xVertCount, yVertCount, 1);

        verts.Release();
    }


    protected override void createPatchTexture(ref Material mat, int x, int y, float currentHeight)
    {
        //EACH PLANET TYPE NEEDS TO HAVE INDEPENDENT TUNED PARAMETERS



        //the getcomponent lines look ugly, is there a way to clean it up?
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
    }
    public override float NoiseValue(Vector3 pos, float scale)
    {

        /*float nx = transform.TransformPoint(pos).x;
        float ny = transform.TransformPoint(pos).y;
        float nz = transform.TransformPoint(pos).z;

        float xx = ((nx - xVertCount) / scale) * frequency;
        float yy = ((ny - yVertCount) / scale) * frequency;
        float zz = ((nz - xVertCount) / scale) * frequency;

        float noiseValue = (worleyNoise.Calculate(nx, ny, nz, worleyScale)) * 0.5f; // should return a value between 0 and 1
        noiseValue -= simplexNoise.CalcPixel3D(xx, yy, zz, 1f / scale);

        return noiseValue;*/
        return 0;
    }
}
