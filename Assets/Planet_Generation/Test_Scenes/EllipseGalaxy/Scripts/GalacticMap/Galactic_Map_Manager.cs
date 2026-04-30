using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Galactic_Map_Manager : MonoBehaviour
{

    protected Vector3 mapBasePos;
    protected Vector3 spaceBasePos;
    private enum Mode { MAP, SPACE };
    private Mode state;

    [SerializeField] private Vector3 baseGalacticMapCameraPosition;

    [SerializeField] private Vector3 playerPositionFloatingOrigin;
    private BaseFreeCam playerCameraFloatingOrigin;
    [SerializeField] private BaseFreeCam galacticMapCamera;

    private Floating_Origin_Manager Floating_Origin_Manager;
    [SerializeField] protected float interpolationT;
    private float dt;
    [SerializeField] private bool waitingToUpdate;
    // Start is called before the first frame update
    void Start()
    {
        state = Mode.SPACE;
        Floating_Origin_Manager = Floating_Origin_Manager.Instance;
    }

    // Update is called once per frame
    void Update()
    {
        MapStateMachine();
        ApplyAnimation();
    }

    private void ApplyAnimation()
    {
        if (interpolationT == 1 || interpolationT == 0) return;
        float t = easeInOutSine(interpolationT);

        Vector3 delta = (baseGalacticMapCameraPosition - playerPositionFloatingOrigin) * dt;
        galacticMapCamera.AddDelta(delta);
    }

    float easeInOutSine(float x)
    {
        return -(Mathf.Cos(Mathf.PI * x) - 1) / 2;
    }
    protected virtual void MapStateMachine()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            waitingToUpdate = true;
            state = (state == Mode.MAP) ? Mode.SPACE : Mode.MAP;
        }

        //if turned into map
        if (state == Mode.MAP && waitingToUpdate)
        {
            waitingToUpdate = false;
            (playerCameraFloatingOrigin, playerPositionFloatingOrigin) = Floating_Origin_Manager.GetCameraInfo();

            galacticMapCamera.SetPosition(playerPositionFloatingOrigin);

            Floating_Origin_Manager.SetCameraPosition(galacticMapCamera.GetPosition(), galacticMapCamera);
        }

        if (interpolationT == 0 && state == Mode.SPACE && waitingToUpdate)
        {
            waitingToUpdate = false;
            Floating_Origin_Manager.ResetCameraPosition();
        }


        switch (state)
        {
            case Mode.MAP:
                dt = 0.5f * Time.deltaTime;
                interpolationT += dt;
                interpolationT = Mathf.Clamp(interpolationT, 0, 1);
                break;
            case Mode.SPACE:
                dt = -0.5f * Time.deltaTime;
                interpolationT += dt;
                interpolationT = Mathf.Clamp(interpolationT, 0, 1);
                break;
        }
    }
}
