using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Simplex;
using Worley;
using UnityEngine.Rendering;

public class HotPlanetNoise : GeneratePlane
{
    // Start is called before the first frame update
    ComputeShader simplex;
    ComputeShader worley;

    public HotPlanetNoise()
    {
        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0.1f;
        oceanMulitplier = 0.05f;
        landMultiplier = 0.2f;

        octaves = 7;
        scale = 3f;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;
    }
    protected override void DispatchNoise(ref Vector3[] vertices, Vector3 origin)
    {
        simplex = (ComputeShader)(Resources.Load("Simplex Noise"));
        worley = (ComputeShader)Instantiate(Resources.Load("Worley Noise"));

        Vector3[] simplexVerts = new Vector3[vertices.Length];
        Vector3[] worleyVerts = new Vector3[vertices.Length];

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

        //SIMPLEX NOISE
        verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        verts.SetData(vertices);

        worldVerts = new ComputeBuffer(verticesWorldSpace.Length, sizeof(float) * 3);
        worldVerts.SetData(verticesWorldSpace);

        setComputeNoiseVariables(ref simplex);
        simplex.Dispatch(shaderHandle, xVertCount, yVertCount, 1);
        ////////////////

        //verts.SetData(worleyVerts);


        oceanFloor = 0.3f;
        setComputeNoiseVariables(ref worley);

        //set worley points
        List<Vector3> points = new List<Vector3>();
        points = patch.planetObject.GetComponent<Sphere>().getWorleyPoints();

        ComputeBuffer listPoints = new ComputeBuffer(points.Count, sizeof(float) * 3);
        listPoints.SetData(points);
        worley.SetBuffer(shaderHandle, "points", listPoints);

        ///////////////////////////////////



        worley.SetBool("inverse", false);
        worley.SetBool("edgeDetect", false);
        worley.SetBool("volcano_crater", true);
        worley.SetFloat("edgeThreshold", 0.9f);
        worley.SetBool("mode", false); //set to add mode
        //worley.Dispatch(shaderHandle, xVertCount, yVertCount, 1);
        AsyncGPUReadback.Request(verts, OnCompleteReadback);

        //verts.Release();
    }

    protected override void DispatchFoliage() { }

    protected override void GenerateFoliage(ref Vector3[] vertices, Vector3 origin) { }

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

    public override float NoiseValue(Vector3 pos, float scale)
    {
        return 0;
    }
}
