using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraDepthInitializer : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Material mat;
    [SerializeField] private Material sunHalo;
    [SerializeField] protected List<Vector3> planetPositions;
    //[SerializeField] private int planetCount = 3;
    

    void Start()
    {
        InitializeDepthTexture(); //sets depth texture generation for camera
    }

    private void InitializeDepthTexture()
    {
        playerCamera = this.GetComponent<Camera>();
        playerCamera.depthTextureMode = DepthTextureMode.Depth;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {

        GameObject planet = transform.parent.GetComponent<ShipControls>().nearbyPlanet;
        if (planet != null) mat.SetVector("_PlanetPos", planet.transform.position); //sets new planet position for atmosphere shader when adequately close.

        RenderTexture intermediate = new RenderTexture(source.width, source.height, 0, source.format)
        {
            enableRandomWrite = true
        };
        intermediate.Create();

        Graphics.Blit(source, intermediate, mat);
        Graphics.Blit(intermediate, destination, sunHalo);
        intermediate.Release();
    }
}
