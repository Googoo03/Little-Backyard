using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace NoiseComputeDispatch
{

    public struct NoiseSettings
    {
        public float lacunarity;
        public float frequency;
        public float persistence;
        public float octaves;
    }


    public class NoiseDispatcher
    {

        //takes in a scriptable object? Want this to be random. Takes in settings and seed
        //gives back the rendertexture for it.
        public static RenderTexture DispatchNoiseTexture(NoiseSettings settings, int seed)
        {
            return null;
        }

    }
}
