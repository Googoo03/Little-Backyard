using System;
using System.Collections;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.ComponentModel;
using UnityEditorInternal;
using UnityEngine;
using NUnit.Framework;

public struct SolarSystemProperties
{
    public int numPlanets;
    public float sunScale;
    public string name;
    public int seed;

    public SolarSystemProperties(int seed_, int numPlanets_ = 0, string name_ = "")
    {
        seed = seed_;
        numPlanets = numPlanets_;
        name = name_;
        sunScale = EllipseGalacticManager.Instance.starObj.instanceData.size * EllipseGalacticManager.Instance.galaxySize; //can modify this later for different star types
        DerivePropertiesFromSeed();
    }

    private void DerivePropertiesFromSeed()
    {
        byte[] bytes = BitConverter.GetBytes(seed);

        if (BitConverter.IsLittleEndian) Array.Reverse(bytes); //reverse if needed

        numPlanets = bytes[0] & 0x07; //only capture the first 3 bits to determine planet number 0-7
        name = "Test System";
    }
};

public struct PlanetProperties
{
    public Vector3 localPosition;
    public GameObject planetObj;

    public PlanetProperties(Vector3 localPosition_, GameObject planetObj_)
    {
        localPosition = localPosition_;
        planetObj = planetObj_;
    }
};

public class SolarSystemManager : Manager
{
    //This class should monitor the distance between the player and the closest star

    //This class will also generate needed planets and their sizes
    //if the player leaves the "sphere of influence" for a given amount of time, the planets are unloaded

    //There can be so-called "Phantom planets" that will be object pool placeholders?

    //Scriptable object for Star types and data?

    enum States { UNLOADED, UNLOADED_TIMED, LOADED_TIMED, LOADED }

    //Solar System Data
    [SerializeField] GameObject planetPrefab;
    [SerializeField] float distanceThreshold;
    [SerializeField] float distanceToNearestStar;
    [SerializeField] float distanceToOrigin;
    [SerializeField] float timeToUnload;
    [SerializeField] float timeUnloadThreshold;

    [SerializeField] States state;


    [SerializeField] List<PlanetProperties> planets;

    //Solar System Properties
    SolarSystemProperties solarSystemProperties;

    //closest star transform
    [SerializeField] Matrix4x4 closestStarTransform;
    Matrix4x4 origin;
    Vector3 originPos;

    Vector3 floatingOriginPos;
    // Start is called before the first frame update
    void Start()
    {
        planets = new(10);
        state = States.UNLOADED;
    }

    // Update is called once per frame
    void Update()
    {
        closestStarTransform = EllipseGalacticManager.Instance.GetClosestStar();
        floatingOriginPos = floating_origin_transform.TRS.GetColumn(3);
        Tick();
        UpdateFloatingOrigin();
    }

    private void Tick()
    {


        if (closestStarTransform == Matrix4x4.identity) return; //everything here is useless unless we have a star to compare to

        Vector3 playerPos = floating_origin_transform.TRS.GetColumn(3);
        Vector3 closestStarPos = closestStarTransform.GetColumn(3);
        distanceToNearestStar = (-playerPos - closestStarPos).magnitude;
        distanceToOrigin = (-playerPos - originPos).magnitude;

        switch (state)
        {
            case States.LOADED:
                if (distanceToOrigin > distanceThreshold)
                {
                    state = States.LOADED_TIMED;
                    timeToUnload = 0;
                }
                break;
            case States.UNLOADED:
                if (distanceToNearestStar < distanceThreshold)
                {
                    state = States.UNLOADED_TIMED;
                    timeToUnload = 0;
                }
                break;
            case States.UNLOADED_TIMED:
                if (distanceToNearestStar > distanceThreshold) //abort countdown
                    state = States.UNLOADED;


                if (timeToUnload >= timeUnloadThreshold)
                {
                    origin = closestStarTransform;
                    originPos = origin.GetColumn(3);
                    LoadSolarSystem();
                    state = States.LOADED;
                }
                timeToUnload += Time.deltaTime;
                break;
            case States.LOADED_TIMED:

                if (distanceToOrigin < distanceThreshold) //abort countdown
                    state = States.LOADED;


                if (timeToUnload >= timeUnloadThreshold)
                {
                    UnloadSolarSystem();
                    state = States.UNLOADED;
                }
                timeToUnload += Time.deltaTime;
                break;
            default:
                break;
        }
    }

    private void UnloadSolarSystem()
    {
        origin = Matrix4x4.identity;
    }

    private void LoadSolarSystem()
    {
        //determine hash by the origin star's local position in the galaxy.
        //this is origin pos - floating origin.

        Vector3 localGalacticPosition = originPos - floatingOriginPos;

        var hash = new Hash128();
        hash.Append(localGalacticPosition.x);
        hash.Append(localGalacticPosition.y);
        hash.Append(localGalacticPosition.z);

        int seed = hash.GetHashCode();
        solarSystemProperties = new SolarSystemProperties(seed); //set seed properties. The struct will derive the rest

        //offset
        Vector3 offset = new(0, 0, 0);

        //It is assumed by this point the number of planets is decided.
        for (int i = 0; i < solarSystemProperties.numPlanets; ++i)
        {
            Assert.Less(i, planets.Capacity, "planet number exceeded buffer capacity");
            offset.z = solarSystemProperties.sunScale + 5000 * (i + 1);
            GameObject newPlanetObj = Instantiate(planetPrefab, (originPos + floatingOriginPos) + offset, Quaternion.identity);
            planets.Add(new PlanetProperties(offset, newPlanetObj));
        }
    }

    private void UpdateFloatingOrigin()
    {
        foreach (PlanetProperties planetProp in planets)
        {
            planetProp.planetObj.transform.position = (originPos + floatingOriginPos) + planetProp.localPosition;
        }
    }
}
