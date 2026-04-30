using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeCam_FloatingOrigin : BaseFreeCam
{
    private Floating_Origin_Manager Floating_Origin_Manager;
    private SolarSystemManager solarSystemManager;
    private float distanceToClosestPlanet;
    private float closestPlanetRadius;
    void Start()
    {
        Floating_Origin_Manager = Floating_Origin_Manager.Instance;
        solarSystemManager = SolarSystemManager.Instance;
    }

    protected override void ApplyForwardDelta()
    {
        Floating_Origin_Manager.SetFloatingOriginDelta(delta);

        //Find closest planet
        Vector2 closestPlanet = solarSystemManager.ComputeShortestDistanceToPlanet(transform.position);
        distanceToClosestPlanet = closestPlanet.x;
        closestPlanetRadius = closestPlanet.y;
        float t = Mathf.Clamp(distanceToClosestPlanet - closestPlanetRadius, 0, closestPlanetRadius) / closestPlanetRadius;
        clampedSpeed = Mathf.Lerp(0.01f * baseSpeed, baseSpeed, t);
    }

}
