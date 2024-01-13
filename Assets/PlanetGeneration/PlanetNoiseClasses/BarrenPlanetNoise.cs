using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Worley;

public class BarrenPlanetNoise : GeneratePlane
{
    public WorleyNoise worley = new WorleyNoise(false);
    public BarrenPlanetNoise() {

        //set up noise parameters. surely theres a better way to do this
        oceanFloor = 0;
        oceanMulitplier = 0;
        landMultiplier = 0.1f;
        octaves = 1;
        scale = 5;
        lacunarity = 2;
        persistance = 0.5f;
        changeHeight = true;
    }
    protected override void createPatchTexture(ref Texture2D tex, int x, int y, float currentHeight)
    {
        tex.SetPixel(x, y, new Color(currentHeight, currentHeight, currentHeight));
    }

    private float EaseInOutCubic(float x) {
        return x < 0.5 ? 4 * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 3) / 2;
    }
    //we want each planet class to have its necessary values and compute its noise values independently.
    //VARIABLES ARE INHERITED. THUS, HAVE THE NOISE PARAMETERS BE CLASS VARIABLES.

    public override float NoiseValue(Vector3 pos, float scale) {

        float nx = transform.TransformPoint(pos).x;
        float ny = transform.TransformPoint(pos).y;
        float nz = transform.TransformPoint(pos).z;

        float noiseValue = worley.Calculate(nx,ny,nz,scale);
        noiseValue = EaseInOutCubic(noiseValue);
        return noiseValue;
    }
}
