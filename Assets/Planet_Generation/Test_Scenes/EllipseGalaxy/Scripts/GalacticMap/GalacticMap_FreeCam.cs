using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GalacticMap_FreeCam : BaseFreeCam
{
    private Floating_Origin_Manager Floating_Origin_Manager;

    [SerializeField] private float scrollSensitivity;
    [SerializeField] private Vector2 scrollDelta;
    [SerializeField] private Vector2 baseMouseSensitivity;

    void Start()
    {
        Floating_Origin_Manager = Floating_Origin_Manager.Instance;
        baseMouseSensitivity = new Vector2(mouseSensitivity.x, mouseSensitivity.y);
    }

    protected override void ApplyScrollDelta()
    {

        scrollDelta = Input.mouseScrollDelta;
        if (Mathf.Abs(scrollDelta.y) < 1e-4f) return;

        float scaleFactor = 1f - (scrollDelta.y * Mathf.Exp(-scrollSensitivity));
        cam.fieldOfView *= scaleFactor;
        cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, 0, 60);
        mouseSensitivity = baseMouseSensitivity * (cam.fieldOfView / 60f);
        LookExtinctionFactor = new Vector2(Mathf.Max(1f, mouseSensitivity.x), Mathf.Max(1f, mouseSensitivity.y));
    }

    protected override void ApplyForwardDelta()
    {
        Floating_Origin_Manager.SetFloatingOriginDelta(delta);
    }
}