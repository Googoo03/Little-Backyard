using System;
using System.Collections;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.ComponentModel;
using UnityEditorInternal;
using UnityEngine;
using NUnit.Framework;

[System.Serializable]
public struct PlanetMaterials
{
    public Material planetMat;
    public Material atmosphereMat;
    public Material cloudMat;
};
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
        byte[] bytes = GetSeedByteStream();

        numPlanets = bytes[0] & 0x07; //only capture the first 3 bits to determine planet number 0-7
        numPlanets = Mathf.Clamp(numPlanets, 1, 7);
        name = "Test System";
    }

    public byte[] GetSeedByteStream()
    {
        byte[] bytes = BitConverter.GetBytes(seed);

        if (BitConverter.IsLittleEndian) Array.Reverse(bytes); //reverse if needed
        return bytes;
    }
};

public struct PlanetProperties
{
    public Vector3 localPosition;
    public GameObject planetObj;

    public Atmosphere_Manager planetAtmosphere_Manager;

    public PlanetProperties(Vector3 localPosition_, GameObject planetObj_, Atmosphere_Manager planetAtmosphere_Manager_)
    {
        localPosition = localPosition_;
        planetObj = planetObj_;
        planetAtmosphere_Manager = planetAtmosphere_Manager_;
    }
};

public class SolarSystemManager : Manager
{
    //This class should monitor the distance between the player and the closest star

    //This class will also generate needed planets and their sizes
    //if the player leaves the "sphere of influence" for a given amount of time, the planets are unloaded

    //There can be so-called "Phantom planets" that will be object pool placeholders?

    //Scriptable object for Star types and data?
    public static SolarSystemManager Instance { get; private set; }

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

    //Planet Materials to Distribute on new objects
    [SerializeField] PlanetMaterials sharedPlanetMaterials;

    EllipseGalacticManager ellipseGalacticManager;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
        planets = new(10);
        state = States.UNLOADED;
    }

    void Start()
    {
        ellipseGalacticManager = EllipseGalacticManager.Instance;
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
        foreach (PlanetProperties planetProp in planets)
            Destroy(planetProp.planetObj);

        planets.Clear();

    }

    private void LoadSolarSystem()
    {
        //determine hash by the origin star's local position in the galaxy.
        //this is origin pos - floating origin.

        //more consistent method needed, perhaps based on the galactic hash

        Vector3 localGalacticPosition = originPos - floatingOriginPos;
        Vector3 galacticSectorPosition = ellipseGalacticManager.galaxySpatialHash.GetSectorID(floatingOriginPos);

        var hash = new Hash128();
        hash.Append(galacticSectorPosition.x);
        hash.Append(galacticSectorPosition.y);
        hash.Append(galacticSectorPosition.z);

        int seed = hash.GetHashCode();
        solarSystemProperties = new SolarSystemProperties(seed); //set seed properties. The struct will derive the rest

        var planetHash = new Hash128();

        //offset
        Vector3 offset;

        byte[] seedByteStream = solarSystemProperties.GetSeedByteStream();
        float theta_partition = 2 * 3.14159265f / 0xff;

        //It is assumed by this point the number of planets is decided.
        for (int i = 0; i < solarSystemProperties.numPlanets; ++i)
        {
            Assert.Less(i, planets.Capacity, "planet number exceeded buffer capacity");
            Assert.Less(i, seedByteStream.Length, "i exceeded byte stream capacity");

            float theta = theta_partition * seedByteStream[i];

            planetHash.Append(seed);
            int newSeed = planetHash.GetHashCode();

            offset = Circle(theta) * (solarSystemProperties.sunScale + Mathf.Lerp(0, distanceThreshold * 0.5f, (i + 1) / solarSystemProperties.numPlanets));
            GameObject newPlanetObj = Instantiate(planetPrefab, (originPos + floatingOriginPos) + offset, Quaternion.identity);
            Atmosphere_Manager atmosphere_Manager = newPlanetObj.GetComponent<PlanetWrapper>().GetAtmosphere_Manager();
            SVOTest planetSVO = newPlanetObj.GetComponent<PlanetWrapper>().GetSVOTest();
            planetSVO.SetSeed(newSeed);

            planets.Add(new PlanetProperties(offset, newPlanetObj, atmosphere_Manager));
            SetPlanetMaterialProperties(newPlanetObj);
        }
    }

    private void SetPlanetMaterialProperties(GameObject planetObj)
    {
        Atmosphere_Manager planetAtmosphere_Manager = planetObj.GetComponent<PlanetWrapper>().GetAtmosphere_Manager();

        //Make new instances for each planet objects, that way they can be manipulated independently
        planetAtmosphere_Manager.SetPlanetMaterial(Instantiate(sharedPlanetMaterials.planetMat));
        planetAtmosphere_Manager.SetAtmosphereMaterial(Instantiate(sharedPlanetMaterials.atmosphereMat));
        planetAtmosphere_Manager.SetCloudMaterial(Instantiate(sharedPlanetMaterials.cloudMat));

    }

    //Returns a point on a unit circle on the xz plane
    private Vector3 Circle(float theta)
    {
        return new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta));
    }

    //Returns Vector2. X is distance, Y is radius of planet
    public Vector2 ComputeShortestDistanceToPlanet(Vector3 dest)
    {
        float minDistance = float.MaxValue;
        float planetRadius = 1f;
        foreach (PlanetProperties planet in planets)
        {
            float distance = Vector3.Distance(planet.planetObj.transform.position, dest);
            if (distance < minDistance)
            {
                minDistance = distance;
                planetRadius = planet.planetObj.GetComponent<PlanetWrapper>().GetPlanetRadius();
            }
        }
        return new Vector2(minDistance, planetRadius);
    }

    public PlanetProperties GetClosestPlanet(Vector3 dest)
    {
        float minDistance = float.MaxValue;
        PlanetProperties closestPlanet = planets.Count > 0 ? planets[0] : new();
        foreach (PlanetProperties planet in planets)
        {
            float distance = Vector3.Distance(planet.planetObj.transform.position, dest);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestPlanet = planet;
            }
        }
        return closestPlanet;
    }

    private void UpdateFloatingOrigin()
    {
        foreach (PlanetProperties planetProp in planets)
        {
            planetProp.planetObj.transform.position = originPos + floatingOriginPos + planetProp.localPosition;
            planetProp.planetAtmosphere_Manager.SetSunProperties(originPos + floatingOriginPos, -planetProp.localPosition.normalized);
        }
    }
}
