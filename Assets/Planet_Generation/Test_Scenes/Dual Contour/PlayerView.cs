using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerView : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Vector3 prevCameraTransform;
    [SerializeField] private Camera playerCam;
    [SerializeField] private float panSpeed;
    private bool cameraSpin;
    private bool reverseSpin;
    private float spinT;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if ((Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.A)) && !cameraSpin) {
            cameraSpin = true;
            reverseSpin = Input.GetKeyDown(KeyCode.A);
            prevCameraTransform = transform.rotation.eulerAngles;
        }
        if (Input.GetKey(KeyCode.W)) transform.position += new Vector3(playerCam.transform.forward.x,0,playerCam.transform.forward.z).normalized * Time.deltaTime * panSpeed;
        if (cameraSpin) SpinCamera(reverseSpin);
        playerCam.GetComponent<Camera>().orthographicSize += Input.mouseScrollDelta.y;
    }

    private void SpinCamera(bool reverse) {
        Vector3 newRot = prevCameraTransform;
        newRot += new Vector3(0,reverse ? -90 : 90,0);
        Vector3 interRot = Vector3.Lerp(prevCameraTransform, newRot, easeFunction(spinT));

        transform.rotation = Quaternion.Euler(interRot.x,interRot.y,interRot.z);
        spinT += 1f * Time.deltaTime;
        if (spinT > 1)
        {
            cameraSpin = false;
            spinT = 0;
        }
    }

    private float easeFunction(float t)
    {
        return 1 - Mathf.Pow(1 - t, 8);

    }
}
