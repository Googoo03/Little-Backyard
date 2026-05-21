using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace NoiseComputeDispatch
{
    public enum NOISETYPE { PERLIN, WORLEY };
    public struct NoiseProperties
    {
        public NOISETYPE noiseType;
        public int octaves;

        public float persistance;
        public float frequency;
        public float amplitude;
        public float lacunarity;
        public float domainWarp;
        public int seed;

        public NoiseProperties(NOISETYPE type, int seed_ = 0, int octaves_ = 1, float peristance_ = 0.5f, float frequency_ = 1f, float amplitude_ = 1f, float lacunarity_ = 1.25f)
        {
            octaves = octaves_;
            persistance = peristance_;
            frequency = frequency_;
            amplitude = amplitude_;
            lacunarity = lacunarity_;
            domainWarp = 0;
            seed = seed_;
            noiseType = type;
        }
    }
    public class NoisePipeline : MonoBehaviour
    {
        // This class should be solely responsible for creating and compositing noise textures together to get layered noise

        //Other classes should be able to specify if they want perlin noise, how many octaves, freq, amp, etc.
        //Also domain warping

        //Should offload this responsibility to a compute shader. It will compute the texture in parallel

        //The planets should have the following settings
        //Continent noise
        //Mountain noise
        //Ore vein noise
        //Octave noise


        public static NoisePipeline Instance { get; private set; }
        [SerializeField] private ComputeShader noiseShader;

        void Awake()
        {
            Instance = this;
        }

        private int GetStride(RenderTextureFormat format)
        {
            switch (format)
            {
                case RenderTextureFormat.RFloat:
                    return sizeof(float);
                case RenderTextureFormat.ARGBFloat:
                    return sizeof(float) * 4;
                default:
                    throw new System.Exception("Unsupported texture format");
            }
        }

        public void ComputeNoiseTexture(RenderTexture result, NoiseProperties prop)
        {
            int kernel = noiseShader.FindKernel("CSMain");

            int width = result.width;
            int height = result.height;
            int depth = result.volumeDepth;

            //set noise properties
            noiseShader.SetTexture(kernel, "Result", result);
            noiseShader.SetInt("octaves", prop.octaves);
            noiseShader.SetFloat("frequency", prop.frequency);
            noiseShader.SetFloat("amplitude", prop.amplitude);
            noiseShader.SetInt("seed", prop.seed);
            noiseShader.SetFloat("persistance", prop.persistance);
            noiseShader.SetFloat("lacunarity", prop.lacunarity);

            //set noise texture dimensions
            noiseShader.SetInt("width", width);
            noiseShader.SetInt("height", height);
            noiseShader.SetInt("depth", depth);

            //dispatch
            int groupX = Mathf.CeilToInt(width / 8.0f);
            int groupY = Mathf.CeilToInt(height / 8.0f);
            int groupZ = Mathf.CeilToInt(depth / 8.0f);
            noiseShader.Dispatch(kernel, groupX, groupY, groupZ);

            /*AsyncGPUReadback.Request(result, (request) =>
            {
                if (request.hasError)
                {
                    Debug.LogError("GPU readback error");
                    return;
                }

                lock (lockObj)
                {

                }
            });*/

            return;
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
};
