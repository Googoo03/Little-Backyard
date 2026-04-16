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

    [SerializeField] private Vector3 CameraPosition;
    [SerializeField] private ComputeShader floatingOriginShader;

    [SerializeField] private List<Manager> managersToUpdate;
    public bool update;

    public static Floating_Origin_Manager Instance { get; private set; }
    private object lockObj = new object();

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
        foreach (Manager m in managersToUpdate)
        {
            m.floating_origin_transform = new Floating_Origin_Transform(Vector3.zero, Quaternion.identity, Vector3.one);

        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (update) update = false;
        UpdateTRS();
    }

    public void DispatchFloatingOriginShader(ComputeBuffer buffer, ComputeBuffer FO_buffer, NativeArray<Matrix4x4> arr, Manager m)
    {
        int kernel = floatingOriginShader.FindKernel("CSMain");
        int limit;
        limit = arr.Length;
        int groupX = Mathf.CeilToInt(limit / 64.0f);

        //Set buffer date for shader
        floatingOriginShader.SetBuffer(kernel, "matrices", buffer);
        floatingOriginShader.SetBuffer(kernel, "FO_matrices", FO_buffer);
        floatingOriginShader.SetMatrix("floating_origin_transform", m.floating_origin_transform.TRS);
        floatingOriginShader.SetInt("floatingOriginLimit", limit);

        floatingOriginShader.Dispatch(kernel, groupX, 1, 1);

        AsyncGPUReadback.Request(FO_buffer, (request) =>
        {
            if (request.hasError)
            {
                Debug.LogError("GPU readback error");
                return;
            }

            var temp = request.GetData<Matrix4x4>();
            lock (lockObj)
            {
                arr.CopyFrom(temp);
            }
            // Use data here
        });
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
