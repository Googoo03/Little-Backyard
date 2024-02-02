using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Worley;

public class IcePlanetNoise : GeneratePlane
{
    // Start is called before the first frame update
    public WorleyNoise worley = new WorleyNoise(true);
    int worleyScale;
    public IcePlanetNoise()
    {
        
        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0;
        oceanMulitplier = 0.07f;
        landMultiplier = 0.07f;
        octaves = 1;
        worleyScale = 3;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;
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

        float noiseValue = worley.Calculate(nx, ny, nz, worleyScale);
        noiseValue = noiseValue < 0.2f ? 0 : noiseValue;
        return noiseValue;
    }
}
