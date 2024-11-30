using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerView : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Vector3 prevCameraTransform;
    private bool cameraSpin;
    private bool reverseSpin;
    private float spinT;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if ((Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow)) && !cameraSpin) {
            cameraSpin = true;
            reverseSpin = Input.GetKeyDown(KeyCode.LeftArrow);
            prevCameraTransform = transform.rotation.eulerAngles;
        }
        if (cameraSpin) SpinCamera(reverseSpin);
    }

    private void SpinCamera(bool reverse) {
        Vector3 newRot = prevCameraTransform;
        newRot += new Vector3(0,reverse ? -90 : 90,0);
        Vector3 interRot = Vector3.Lerp(prevCameraTransform, newRot, easeFunction(spinT));

        transform.rotation = Quaternion.Euler(interRot.x,interRot.y,interRot.z);
        spinT += 0.5f * Time.deltaTime;
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
