using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraDepthInitializer : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Camera playerCamera;
    void Start()
    {
        playerCamera = this.GetComponent<Camera>();
        playerCamera.depthTextureMode = DepthTextureMode.Depth;
    }
}
