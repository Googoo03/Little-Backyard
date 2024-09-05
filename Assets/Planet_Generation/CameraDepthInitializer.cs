using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraDepthInitializer : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera transparentCamera;

    [SerializeField] private Shader oceanDepthShader;

    [SerializeField] private Material mat;
    [SerializeField] private Material planetRings;
    [SerializeField] private Material sunHalo;

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
        playerCamera.depthTextureMode = DepthTextureMode.Depth;

        
        if (!waterDepthTexture) waterDepthTexture = new RenderTexture(Screen.width,Screen.height, 32, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat);
        //waterDepthTexture.format = RenderTextureFormat.Depth;
        waterDepthTexture.Create();

        transparentCamera.depthTextureMode = DepthTextureMode.Depth;
        transparentCamera.targetTexture = waterDepthTexture;

        //transparentCamera.cullingMask = TransparentObject; //Water
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


        if (planet != null)
        {
            mat.SetVector("_PlanetPos", planet.transform.position); //sets new planet position for atmosphere shader when adequately close.
            mat.SetFloat("_Radius", planet.GetComponent<Sphere>().getRadius());
            planetRings.SetVector("_PlanetPos", planet.transform.position);
            //planet.GetComponent<Sphere>().SetRingShader();
        }

        RenderTexture intermediate = new RenderTexture(source.width, source.height, 0, source.format)
        {
            enableRandomWrite = true
        };
        intermediate.Create();

        RenderTexture Planet_intermediate = new RenderTexture(source.width, source.height, 0, source.format)
        {
            enableRandomWrite = true
        };
        Planet_intermediate.Create();

        Graphics.Blit(source, intermediate, mat);
        Graphics.Blit(intermediate, Planet_intermediate, planetRings);
        Graphics.Blit(Planet_intermediate, destination, sunHalo);
        intermediate.Release();
        Planet_intermediate.Release();
    }

    private void MatchCameraSettings() {
        if(playerCamera) transparentCamera.fieldOfView = playerCamera.fieldOfView;
    }
}
