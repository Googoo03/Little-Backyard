using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using FloatingOrigin;
using System;

public class Floating_Origin_Manager : Manager
{
    //This class should take in a list of Managers that take care of object transforms

    //Each manager will go through a simple sequence changing translation, rotation, and scale
    // (Possibly TRS matrix) to be relative to the camera.
    //We dont care about a "lack of precision", we care about a lack of precision near the camera. That which is visible. 
    public static Floating_Origin_Manager Instance { get; private set; }
    private object lockObj = new();
    public bool update;



    [SerializeField] private Vector3 CameraPosition;
    [SerializeField] private BaseFreeCam Camera;
    [SerializeField] private Stack<Tuple<BaseFreeCam, Vector3>> CameraPositionStack = new();
    [SerializeField] private ComputeShader floatingOriginShader;

    [SerializeField] private List<Manager> managersToUpdate;

    void Awake()
    {
        Instance = this;
        foreach (Manager m in managersToUpdate)
        {
            m.floating_origin_transform = new Floating_Origin_Transform(Vector3.zero, Quaternion.identity, Vector3.one);

        }
    }

    // Update is called once per frame
    void Update()
    {
        if (update) update = false;
        UpdateTRS();
    }

    public void DispatchFloatingOriginShader(ComputeBuffer buffer, ComputeBuffer FO_buffer, Manager m, int limit)
    {
        int kernel = floatingOriginShader.FindKernel("CSMain");
        int groupX = Mathf.CeilToInt(limit / 64.0f);

        //Set buffer date for shader
        floatingOriginShader.SetBuffer(kernel, "matrices", buffer);
        floatingOriginShader.SetBuffer(kernel, "FO_matrices", FO_buffer);
        floatingOriginShader.SetMatrix("floating_origin_transform", m.floating_origin_transform.TRS);
        floatingOriginShader.SetVector("floating_origin_position", m.floating_origin_transform.TRS.GetColumn(3));
        floatingOriginShader.SetInt("floatingOriginLimit", limit);

        floatingOriginShader.Dispatch(kernel, groupX, 1, 1);
    }

    public void SetFloatingOriginDelta(Vector3 delta)
    {
        CameraPosition += delta;
    }

    public Tuple<BaseFreeCam, Vector3> GetCameraInfo() { return new Tuple<BaseFreeCam, Vector3>(Camera, CameraPosition); }

    public Vector3 GetFloatingOriginPosition() { return -CameraPosition; }

    public void SetCameraPosition(Vector3 cameraPosition_, BaseFreeCam cam)
    {
        Camera.gameObject.SetActive(false); //disable old camera
        CameraPositionStack.Push(new Tuple<BaseFreeCam, Vector3>(Camera, CameraPosition));
        CameraPosition = cameraPosition_; //set new vars
        Camera = cam;
        Camera.gameObject.SetActive(true);
    }

    public void ResetCameraPosition()
    {
        Camera.gameObject.SetActive(false);
        (Camera, CameraPosition) = CameraPositionStack.Pop();
        Camera.gameObject.SetActive(true);
    }

    private void UpdateTRS()
    {
        foreach (Manager m in managersToUpdate)
        {
            Vector4 newPos = new(-CameraPosition.x, -CameraPosition.y, -CameraPosition.z, 1);
            m.floating_origin_transform.TRS.SetColumn(3, newPos);
        }
    }
}
