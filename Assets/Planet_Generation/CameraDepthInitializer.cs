using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public struct MaterialPair
{
    public Material mat;
    public float resolutionFactor;
    public MaterialPair(Material mat_, float res)
    {
        mat = mat_;
        resolutionFactor = res;
    }

};

public class CameraDepthInitializer : MonoBehaviour
{
    // Start is called before the first frame update
    [Header("Cameras")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera transparentCamera;

    [SerializeField] private Shader oceanDepthShader;

    [Header("Material List")]
    [SerializeField] private List<MaterialPair> materials;

    [SerializeField] private Material depthCopier;
    //[SerializeField] private int planetCount = 3;
    [SerializeField] private GameObject planet;
    private RenderTexture waterDepthTexture;

    void Start()
    {
        InitializeDepthTexture(); //sets depth texture generation for camera
    }

    private void InitializeDepthTexture()
    {
        playerCamera = this.GetComponent<Camera>();
        playerCamera.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals;


        if (!waterDepthTexture) waterDepthTexture = new RenderTexture(Screen.width, Screen.height, 32, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat);
        waterDepthTexture.Create();

        transparentCamera.depthTextureMode = DepthTextureMode.Depth;
        transparentCamera.targetTexture = waterDepthTexture;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        MatchCameraSettings();

        RenderTexture temp = new RenderTexture(source.width, source.height, 0, source.format)
        {
            enableRandomWrite = true
        };
        temp.Create();

        int i = 0;
        RenderTexture start = source;
        RenderTexture end = start;
        foreach (MaterialPair mPair in materials)
        {
            Material _mat = mPair.mat;
            float resolution = mPair.resolutionFactor;

            start = i == 0 ? source : temp;

            System.Func<float, RenderTexture> createTex = (float resolution) =>
            {
                RenderTexture intermediate = new RenderTexture((int)(source.width * resolution), (int)(source.height * resolution), 0, source.format)
                {
                    enableRandomWrite = true
                };
                intermediate.Create();
                return intermediate;
            };
            end = i == materials.Count - 1 ? destination : createTex(resolution);

            Graphics.Blit(start, end, _mat);
            if (temp != end && i < materials.Count - 1)
            {
                temp.Release();
                temp = end;
            }
            i++;
        }
        destination = end;
        temp.Release();
        start.Release();
        if (end) end.Release();
    }

    private void MatchCameraSettings()
    {
        if (playerCamera) transparentCamera.fieldOfView = playerCamera.fieldOfView;

        if (waterDepthTexture.width != Screen.width || waterDepthTexture.height != Screen.height)
        {
            waterDepthTexture.Release();
            waterDepthTexture = new RenderTexture(Screen.width, Screen.height, 32, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat);

            waterDepthTexture.Create();

            transparentCamera.depthTextureMode = DepthTextureMode.Depth;
            transparentCamera.targetTexture = waterDepthTexture;
        }

    }

    public void AddMaterial(Material mat, float resolution = 1f)
    {
        materials.Add(new MaterialPair(mat, resolution));
    }

    public void RemoveMaterial(Material mat)
    {
        MaterialPair matPair = materials.Find(m => m.mat == mat);
        materials.Remove(matPair);
    }
}
