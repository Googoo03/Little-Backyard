using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Atmosphere_Manager : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] ComputeShader transmittanceLUT;
    [SerializeField] RenderTexture targetTexture;

    [SerializeField] Material atmosphereMat;
    [SerializeField] Material planetMat;
    int targetTextureResolution = 256;
    public Vector3 wavelengths = new Vector3(700, 530, 460);

    public Vector4 testParams = new Vector4(7, 1.26f, 0.1f, 3);
    public float scatteringStrength = 20;

    [Range(1, 10)]
    public float intensity = 1;

    public float ditherStrength = 1f;
    public float ditherScale = 4;

    [Range(1, 10)]
    public float densityFalloff = 4f;

    public GameObject sun;

    void Update()
    {
        targetTexture = new RenderTexture(targetTextureResolution, targetTextureResolution, 0, RenderTextureFormat.RFloat);
        targetTexture.enableRandomWrite = true;

        int kernel = transmittanceLUT.FindKernel("CSMain");
        transmittanceLUT.SetTexture(kernel, "Result", targetTexture);

        transmittanceLUT.SetInt("textureSize", targetTextureResolution);
        transmittanceLUT.SetInt("numOutScatteringSteps", 100);

        transmittanceLUT.SetFloat("atmosphereHeight", 4696f / 4096f);
        transmittanceLUT.SetFloat("densityFalloff", densityFalloff);


        atmosphereMat.SetVector("params", testParams);
        atmosphereMat.SetInt("numInScatteringPoints", 10);
        atmosphereMat.SetInt("numOpticalDepthPoints", 100);
        atmosphereMat.SetFloat("atmosphereRadius", 4696);
        atmosphereMat.SetFloat("planetRadius", 4096);
        atmosphereMat.SetFloat("densityFalloff", densityFalloff);
        atmosphereMat.SetVector("dirToSun", sun.transform.position.normalized);

        // Strength of (rayleigh) scattering is inversely proportional to wavelength^4
        float scatterX = Mathf.Pow(400 / wavelengths.x, 4);
        float scatterY = Mathf.Pow(400 / wavelengths.y, 4);
        float scatterZ = Mathf.Pow(400 / wavelengths.z, 4);
        atmosphereMat.SetVector("scatteringCoefficients", new Vector3(scatterX, scatterY, scatterZ) * scatteringStrength);



        atmosphereMat.SetFloat("intensity", intensity);
        atmosphereMat.SetFloat("ditherStrength", ditherStrength);
        atmosphereMat.SetFloat("ditherScale", ditherScale);

        int groupX = Mathf.CeilToInt(targetTextureResolution / 8.0f);
        int groupY = Mathf.CeilToInt(targetTextureResolution / 8.0f);
        transmittanceLUT.Dispatch(kernel, groupX, groupY, 1);

        atmosphereMat.SetTexture("_BakedOpticalDepth", targetTexture);

        //Set planet sun direction for lighting
        planetMat.SetVector("_DirToSun", sun.transform.position.normalized);
    }
}
