using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraDepthInitializer : MonoBehaviour
{
    // Start is called before the first frame update
    [Header("Cameras")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera transparentCamera;

    [SerializeField] private Shader oceanDepthShader;

    [Header("Material List")]
    [SerializeField] private Material[] materials;

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

        
        if (!waterDepthTexture) waterDepthTexture = new RenderTexture(Screen.width,Screen.height, 32, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat);
        waterDepthTexture.Create();

        transparentCamera.depthTextureMode = DepthTextureMode.Depth;
        transparentCamera.targetTexture = waterDepthTexture;
    }

    private void LateUpdate()
    {
        transparentCamera.RenderWithShader(oceanDepthShader, "");
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        MatchCameraSettings();

        //Render depth texture with oceanDepthShader by rendering everything in view with a shadow cast
        transparentCamera.RenderWithShader(oceanDepthShader, "");


        /*if (planet != null)
        {
            mat.SetVector("_PlanetPos", planet.transform.position); //sets new planet position for atmosphere shader when adequately close.
            mat.SetFloat("_Radius", planet.GetComponent<Sphere>().getRadius());
            mat.SetFloat("_OceanRad", planet.transform.GetChild(0).transform.localScale.x);
            planetRings.SetVector("_PlanetPos", planet.transform.position);
            //planet.GetComponent<Sphere>().SetRingShader();
        }*/

        RenderTexture temp = new RenderTexture(source.width, source.height, 0, source.format)
        {
            enableRandomWrite = true
        };
        temp.Create();

        int i = 0;
        RenderTexture start = source;
        RenderTexture end = start;
        foreach (Material _mat in materials) {


            start = i == 0 ? source : temp;

            System.Func<RenderTexture> createTex = () => {
                RenderTexture intermediate = new RenderTexture(source.width, source.height, 0, source.format)
                {
                    enableRandomWrite = true
                };
                intermediate.Create();
                return intermediate;
            };
            end = i == materials.Length - 1 ? destination : createTex();

            Graphics.Blit(start, end, _mat);
            if (temp != end && i < materials.Length - 1)
            {
                temp.Release();
                temp = end;
            }
            i++;
        }
        destination = end;
        temp.Release();
        start.Release();
        if(end) end.Release();
    }

    private void MatchCameraSettings() {
        if(playerCamera) transparentCamera.fieldOfView = playerCamera.fieldOfView;

        if (waterDepthTexture.width != Screen.width || waterDepthTexture.height != Screen.height)
        {
            waterDepthTexture.Release();
            waterDepthTexture = new RenderTexture(Screen.width, Screen.height, 32, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat);

            waterDepthTexture.Create();

            transparentCamera.depthTextureMode = DepthTextureMode.Depth;
            transparentCamera.targetTexture = waterDepthTexture;
        }
        
    }
}
