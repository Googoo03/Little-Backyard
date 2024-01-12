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
    WorleyNoise worleyNoise = new WorleyNoise(false);

    int worleyScale;

    public HotPlanetNoise()
    {
        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0;
        oceanMulitplier = 0.08f;
        landMultiplier = 0.02f;

        octaves = 4;
        scale = 0.4f;
        worleyScale = 3;
        lacunarity = 2;
        persistance = 0.5f;
    }
    protected override void createPatchTexture(ref Texture2D tex, int x, int y, float currentHeight)
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
                tex.SetPixel(x, y, color);
                break;
            }
            if (r == regionLength - 2)
            {
                Color color = patch.planetObject.GetComponent<Sphere>().getRegionColor(r - 1);
                tex.SetPixel(x, y, color);
            }
        }
    }
    public override float NoiseValue(Vector3 pos, float scale)
    {

        float nx = transform.TransformPoint(pos).x;
        float ny = transform.TransformPoint(pos).y;
        float nz = transform.TransformPoint(pos).z;

        float xx = ((nx - xVertCount) / scale) * frequency;
        float yy = ((ny - yVertCount) / scale) * frequency;
        float zz = ((nz - xVertCount) / scale) * frequency;

        float noiseValue = (worleyNoise.Calculate(nx, ny, nz, worleyScale)) * 0.5f; // should return a value between 0 and 1
        noiseValue -= simplexNoise.CalcPixel3D(xx, yy, zz, 1f / scale);

        return noiseValue;
    }
}
