using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Simplex;

public class GasPlanetNoise : GeneratePlane
{
    // Start is called before the first frame update
    Noise simplexNoise = new Noise();
    public GasPlanetNoise()
    {

        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0;
        oceanMulitplier = 0f;
        landMultiplier = 0.03f;
        octaves = 4;
        scale = 0.3f;
        //worleyScale = 3;
        lacunarity = 3;
        persistance = 0.9f;
        changeHeight = false;
    }
    protected override void createPatchTexture(ref Material mat, int x, int y, float currentHeight)
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
    }

    private float EaseOutExpo(float x)
    {
        return x == 1 ? 1 : 1 - Mathf.Pow(2, -10 * x);
    }
    //we want each planet class to have its necessary values and compute its noise values independently.
    //VARIABLES ARE INHERITED. THUS, HAVE THE NOISE PARAMETERS BE CLASS VARIABLES.

    public override float NoiseValue(Vector3 pos, float scale)
    {

        float nx = transform.TransformPoint(pos).x;
        float ny = transform.TransformPoint(pos).y;
        float nz = transform.TransformPoint(pos).z;

        float xx = ((nx - xVertCount) / scale) * frequency;
        float yy = ((ny - yVertCount) / scale) * frequency;
        float zz = ((nz - xVertCount) / scale) * frequency;

        float noiseValue = simplexNoise.CalcPixel3D(xx, yy, zz, 1f / scale);
        // should return a value between 0 and 1

        return noiseValue;
    }
}
