using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Atmosphere_Manager : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] ComputeShader transmittanceLUT;
    [SerializeField] RenderTexture targetTexture;

    [SerializeField] Material atmosphereMat, cloudMat, planetMat;
    int targetTextureResolution = 256;
    public Vector3 wavelengths = new Vector3(700, 530, 460);

    public Vector4 testParams = new Vector4(7, 1.26f, 0.1f, 3);
    public float scatteringStrength = 20;

    [Range(0, 10)]
    public float intensity = 1;

    public float ditherStrength = 1f;
    public float ditherScale = 4;

    [Range(1, 10)]
    public float densityFalloff = 4f;

    private Vector3 sunPos;
    private Vector3 dirToSun;
    [SerializeField] private float planetRadius;
    [SerializeField] private float atmosphereThickness; //ratio of atmosphere radius to planet radius
    [SerializeField] private Vector3 atmospherePosition;
    private Vector3 scatterVector;

    void Start()
    {
        targetTexture = new RenderTexture(targetTextureResolution, targetTextureResolution, 0, RenderTextureFormat.RFloat);
        targetTexture.enableRandomWrite = true;

        int kernel = transmittanceLUT.FindKernel("CSMain");
        transmittanceLUT.SetTexture(kernel, "Result", targetTexture);

        transmittanceLUT.SetInt("textureSize", targetTextureResolution);
        transmittanceLUT.SetInt("numOutScatteringSteps", 100);

        transmittanceLUT.SetFloat("atmosphereHeight", 4696f / 4096f);
        transmittanceLUT.SetFloat("densityFalloff", densityFalloff);

        int groupX = Mathf.CeilToInt(targetTextureResolution / 8.0f);
        int groupY = Mathf.CeilToInt(targetTextureResolution / 8.0f);
        transmittanceLUT.Dispatch(kernel, groupX, groupY, 1);

        //Set Camera Shaders
        CameraDepthInitializer camera = Camera.main.gameObject.GetComponent<CameraDepthInitializer>();
        camera.AddMaterial(atmosphereMat);
        //camera.AddMaterial(cloudMat);
    }

    void Update()
    {
        atmospherePosition = transform.position;
        LoadMaterialData(planetMat, atmosphereMat, cloudMat);
    }

    void OnDestroy()
    {
        CameraDepthInitializer camera = Camera.main?.gameObject.GetComponent<CameraDepthInitializer>();
        camera?.RemoveMaterial(atmosphereMat);
    }

    private void LoadMaterialData(Material planetMat, Material atmosphereMat = null, Material cloudMat = null)
    {

        //Set planet sun direction for lighting
        planetMat.SetVector("_DirToSun", dirToSun);
        planetMat.SetVector("planetCentre", atmospherePosition);

        if (atmosphereMat == null) return;

        atmosphereMat.SetVector("params", testParams);
        atmosphereMat.SetInt("numInScatteringPoints", 10);
        atmosphereMat.SetInt("numOpticalDepthPoints", 100);
        atmosphereMat.SetFloat("atmosphereRadius", planetRadius * atmosphereThickness);
        atmosphereMat.SetFloat("planetRadius", planetRadius);
        atmosphereMat.SetVector("planetCentre", atmospherePosition);
        atmosphereMat.SetFloat("densityFalloff", densityFalloff);
        atmosphereMat.SetVector("dirToSun", dirToSun);

        // Strength of (rayleigh) scattering is inversely proportional to wavelength^4
        scatterVector.x = Mathf.Pow(400 / wavelengths.x, 4);
        scatterVector.y = Mathf.Pow(400 / wavelengths.y, 4);
        scatterVector.z = Mathf.Pow(400 / wavelengths.z, 4);
        atmosphereMat.SetVector("scatteringCoefficients", scatterVector * scatteringStrength);



        atmosphereMat.SetFloat("intensity", intensity);
        atmosphereMat.SetFloat("ditherStrength", ditherStrength);
        atmosphereMat.SetFloat("ditherScale", ditherScale);

        atmosphereMat.SetTexture("_BakedOpticalDepth", targetTexture);

        if (cloudMat == null) return;

        //Set cloud params
        cloudMat.SetVector("planetCentre", Vector3.zero);
        cloudMat.SetFloat("_AtmosphereRadius", planetRadius * atmosphereThickness);
        cloudMat.SetFloat("cloudRadius", planetRadius);
        cloudMat.SetFloat("numCloudPoints", 50);
        cloudMat.SetVector("_SunPos", sunPos);
    }

    //Public methods------------------------------------------------------------------------------------

    public void SetPlanetMaterial(Material planetMat_)
    {
        planetMat = planetMat_;
    }

    public void SetAtmosphereMaterial(Material atmosphereMat_)
    {
        atmosphereMat = atmosphereMat_;
    }

    public void SetCloudMaterial(Material cloudMat_)
    {
        cloudMat = cloudMat_;
    }

    public void SetPlanetRadius(float radius_) { planetRadius = radius_; }

    public void SetSunProperties(Vector3 sunPos_, Vector3 dirToSun_)
    {
        sunPos = sunPos_;
        dirToSun = dirToSun_;
    }

    public Material GetPlanetMat() { return planetMat; }
    //--------------------------------------------------------------------------------------------------
}
