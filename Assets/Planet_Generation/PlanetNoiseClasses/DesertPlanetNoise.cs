using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Simplex;
using Worley;

public class DesertPlanetNoise : GeneratePlane
{
    ComputeShader simplex;
    ComputeShader worley;

    int worleyScale;

    public DesertPlanetNoise()
    {
        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0;
        oceanMulitplier = .1f;
        landMultiplier = .5f;

        octaves = 12;
        scale = 5f;
        //worleyScale = 4;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;
    }
    /*protected override void createPatchTexture(ref Material mat, int x, int y, float currentHeight)
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
    }*/

    protected override void DispatchFoliage() { }

    protected override void GenerateFoliage(ref Vector3[] vertices, Vector3 origin) { }

    protected override void DispatchNoise(Vector3[] vertices, Vector3 origin)
    {
        simplex = (ComputeShader)(Resources.Load("Simplex Noise"));
        worley = (ComputeShader)Instantiate(Resources.Load("Worley Noise"));

        Vector3[] simplexVerts = new Vector3[vertices.Length];
        Vector3[] worleyVerts = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; ++i)
        {
            Vector3 pos = vertices[i];
            float nx = transform.TransformPoint(pos).x;
            float ny = transform.TransformPoint(pos).y;
            float nz = transform.TransformPoint(pos).z;

            float xx = ((nx - xVertCount) / scale);
            float yy = ((ny - yVertCount) / scale);
            float zz = ((nz - xVertCount) / scale);

            simplexVerts[i] = new Vector3(xx, yy, zz);
            worleyVerts[i] = new Vector3(nx, ny, nz);
        }

        //SIMPLEX NOISE
        verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        verts.SetData(simplexVerts);

        setComputeNoiseVariables(ref simplex);
        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);
        ////////////////

        verts.SetData(worleyVerts);
        setComputeNoiseVariables(ref worley);

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
        worley.Dispatch(shaderHandle, xVertCount, yVertCount, 1);
        
        verts.Release();
        listPoints.Release();
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
