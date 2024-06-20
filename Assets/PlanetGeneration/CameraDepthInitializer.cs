using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraDepthInitializer : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Material mat;
    [SerializeField] protected List<Vector3> planetPositions;
    [SerializeField] private int planetCount = 3;
    

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

        Graphics.Blit(source, destination, mat);
    }
}
