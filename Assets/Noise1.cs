using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Noise1 : MonoBehaviour
{

    // Use this for initialization
       // Update is called once per frame
    void Update()
    {

    }

    public static float Perlin3d(float x, float y, float z)
    {
        float AB = Mathf.PerlinNoise(x, y);
        float BC = Mathf.PerlinNoise(y, z);
        float AC = Mathf.PerlinNoise(x, z);

        float BA = Mathf.PerlinNoise(y, x);
        float CB = Mathf.PerlinNoise(z, y);
        float CA = Mathf.PerlinNoise(z, x);

        float ABC = AB + BC + AC + BA + CB + CA;
        return ABC / 6f;
    }
}

