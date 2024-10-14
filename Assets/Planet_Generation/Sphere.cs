using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System;

public struct PatchConfig
{
    public string name;
    public int maxLOD;
    public int LODlevel;
    public Vector2 LODOffset;

    public Vector2 textureOffset;

    public Vector3 uAxis;
    public Vector3 vAxis;
    public Vector3 height;
    public Vector2Int vertices;
    public GameObject planetObject;
    public float distanceThreshold;

    public float radius;
    public PatchConfig(string aName, Vector3 aUAxis, Vector3 aVAxis, int level,Vector2 LODoffset, Vector2Int xyVert, GameObject planet, float distanceT, float _radius, Vector2 texOffset)
    {
        //seed, persistance, lacunarity, octaves, ref heightCurve, planetType, ref regions, ref heights
        name = aName;
        uAxis = aUAxis;
        vAxis = aVAxis;

        height = Vector3.Cross(vAxis, uAxis);

        LODlevel = level;
        LODOffset = LODoffset;

        textureOffset = texOffset;

        vertices = xyVert;
        planetObject = planet;
        distanceThreshold = distanceT;
        maxLOD = 4;
        radius = _radius;
        
    }
}

public class Sphere : MonoBehaviour
{
    //Assign each cube-sphere face
    
    //////////////////////////////////////

    public int xVertCount;
    public int yVertCount;

    [SerializeField] private float radius;
    [SerializeField] private float atmosphereHeight;
    [SerializeField] private float atmosphereDensity;
    [SerializeField] private float cloudDensity;

    [SerializeField] private UInt64 seed;

    [SerializeField] private bool hasRings;
    [SerializeField] private bool hasOcean;


    //SHADER PARAMS
    [Header("Shader Params")]
    [SerializeField] private Material ringShader;
    [SerializeField] private Vector3 ringNormal;
    [SerializeField] private Color ringColor = new Color(0,0,0);
    [SerializeField] private float ringRadius;
    [SerializeField] private float ringWidth;
    [SerializeField] private Vector3 sunPos;
    [SerializeField] private Color atmoColor;

    [SerializeField] private Material atmoShader;

    [SerializeField] private Event_Manager_Script event_manager;

    public float oceanFloor;
    public float oceanMultiplier;

    public float landMultiplier;

    //public Color[] regions; //turn this into a 2D array and access directly?

    [SerializeField] private int planetType; // 0 = Hot, 1 = Ice, 2 = Life, 5 = Gas, 4 = Desert, 3 = Barren
    public float pscale;

    
    private PatchConfig[] patches;
    [SerializeField]private List<PatchLOD> LOD;

    [SerializeField]private List<Vector3> worleyPoints = new List<Vector3>();

    public bool nextLOD;
    public bool prevLOD;

    private float initialDistanceThreshold;
    private void Update()
    {
        //////////THIS AND THE UPDATE FUNCTION ARE ONLY FOR TESTING PURPOSES, MEANT TO BE REMOVED LATER
        if (nextLOD)
        {
            nextLODLevel();
            nextLOD = false;
        }
        if (prevLOD)
        {

            prevLODLevel();
            prevLOD = false;
        }
        ////////////////////////////////////////////////////
    }

    

    void Start()
    {
        LOD = new List<PatchLOD>() { };

        //Get ring shader

        //create seed using a hash
        var hash = new Hash128();
        hash.Append(transform.position.x);
        hash.Append(transform.position.y);
        hash.Append(transform.position.z);

        if(seed == 0) seed = (UInt64)hash.GetHashCode(); //this may cause issues because it is so large, but this is just for testing purposes
        ////////////////////////////////////

        //Planet Type override
        planetType = planetType != -1 ? planetType : Mathf.Abs((int)seed)%6;
        transform.name = "Planet" + planetType.ToString();


        hasRings = true; //for testing purposes only
        hasOcean = planetType == 2 || planetType == 0 ? true : false;

        atmosphereDensity = ((seed >> 8) % 256) / 256.0f;
        atmosphereDensity *= planetType == 3 ? 0.03f : 1;

        cloudDensity = ((seed >> 12) % 256) / 256.0f * 32.0f;
        cloudDensity *= planetType == 3 ? 0 : 1;

        sunPos = event_manager ? event_manager.get_sun().transform.position : sunPos;

        //create all 6 sides of the sphere-cube
        Vector2Int xyVert = new Vector2Int(xVertCount, yVertCount);
        initialDistanceThreshold = 4 * radius;
        patches = new PatchConfig[]
        {
         new PatchConfig("top", Vector3.right, Vector3.forward,0, Vector2.zero,xyVert,transform.gameObject,initialDistanceThreshold,radius, Vector2.zero),
         new PatchConfig("bottom", Vector3.left, Vector3.forward, 0, Vector2.zero, xyVert, transform.gameObject, initialDistanceThreshold, radius, Vector2.zero),
         new PatchConfig("left", Vector3.up, Vector3.forward, 0, Vector2.zero, xyVert, transform.gameObject, initialDistanceThreshold, radius,Vector2.zero),
         new PatchConfig("right", Vector3.down, Vector3.forward,0, Vector2.zero,xyVert, transform.gameObject,initialDistanceThreshold,radius,Vector2.zero),
         new PatchConfig("front", Vector3.right, Vector3.down, 0, Vector2.zero, xyVert, transform.gameObject, initialDistanceThreshold, radius, Vector2.zero),
         new PatchConfig("back", Vector3.right, Vector3.up, 0, Vector2.zero, xyVert, transform.gameObject, initialDistanceThreshold, radius, Vector2.zero)
        };

        //create a list of points for worleyNoise
        generateWorleyPoints(25);

        GenerateAtmoColor();

        //Spawn Ring with correct orientation. Store orientation?
        if (hasRings) GenerateRings();
        if (hasOcean)
        {
            transform.GetChild(0).gameObject.SetActive(true);
            //set the ocean size
            transform.GetChild(0).transform.localScale = Vector3.one * (radius + oceanFloor);
            transform.GetChild(0).GetComponent<Renderer>().material.SetVector("_SunPos", sunPos);
        }
        else {
            transform.GetChild(0).gameObject.SetActive(true);
            //set the ocean size
            transform.GetChild(0).transform.localScale = Vector3.one * (0);
            
        }
        
        ////////////////////////////

        //generate the patches when finished configuring
        GeneratePatches();
    }
    private void generateWorleyPoints(int num) {
        for (int i = 0; i < num; ++i)
        {
            Vector3 point = new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100));
            point.Normalize();
            point *= radius;
            //this should be multiplied by the radius in the future
            //point += transform.position;
            
            worleyPoints.Add(point);
        }
    }

    private void GenerateRings() {
        ringNormal = new Vector3((seed >> 4) % 360, (seed >> 8) % 360, (seed >> 12) % 360).normalized;
        ringColor = new Color((seed >> 3) % 256, (seed >> 6) % 256,(seed >> 9) % 256, (seed >> 12) % 256);
        ringColor /= 256.0f;
        ringRadius = (radius+1) + (1 * (seed % 10));
        ringWidth = (ringRadius+1) + (1 * (seed % 4));
        GameObject rings = transform.GetChild(1).gameObject;
        rings.SetActive(true);
        rings.transform.up = ringNormal;
        rings.transform.localScale = Vector3.one*ringWidth;

        //NEEDS TO BE CHANGED WITH SOMETHING MORE ELEGANT LATER
        rings.transform.GetChild(0).GetComponent<Renderer>().material.color = ringColor;
        rings.transform.GetChild(1).GetComponent<Renderer>().material.color = ringColor;
        //SetRingShader();
        return;
    }

    private void GenerateAtmoColor()
    {
        atmoColor = new Color((seed>>2)%256,(seed >> 5)%256,(seed >> 8)%256,256);
        atmoColor /= 256.0f;

        Color baseColor = Color.black;

        switch(planetType){
            case 0:
                baseColor = new Color(0.2f,0.2f,0.2f,1);
                break;
            case 1:
                break;
                baseColor = new Color(0.7f, 0.2f, 0.2f, 1);
            case 2:
                break;
                baseColor = new Color(1f, 0.9f, 0.1f, 1);
            case 3:
                break;
                
            case 4:
                break;
                baseColor = new Color(1f, 0.7f, 0.1f, 1);
            case 5:
                break;
            default:
                break;
        }
        // A + (B-A)*t
        atmoColor = (atmoColor - baseColor) * 0.5f + baseColor;
        return;

    }

    public void SetRingShader() {
        GameObject rings = transform.GetChild(1).gameObject;
        rings.SetActive(false);
        ringShader.SetColor("_Color", ringColor);
        ringShader.SetVector("_PlanetPos", transform.position);
        ringShader.SetVector("_PlaneNormal", ringNormal);
        ringShader.SetFloat("_Radius", ringRadius);
        ringShader.SetFloat("_Width", ringWidth);
        ringShader.color = ringColor;
        return;
    }

    public void SetAtmoShader() {
        atmoShader.SetVector("_PlanetPos", transform.position);
        if (!event_manager) atmoShader.SetVector("_SunPos", sunPos);
        atmoShader.SetColor("_Color", atmoColor);
        atmoShader.SetFloat("_Radius", radius+0.5f);
        atmoShader.SetFloat("_CloudDensity", cloudDensity);
        atmoShader.SetFloat("_Density", atmosphereDensity);
        atmoShader.SetFloat("_OceanRad", transform.GetChild(0).transform.localScale.x);
        //no setting atmosphere height as of yet
        return;
    }

    public Event_Manager_Script getEvent_Manager() { return event_manager; }

    public ref List<Vector3> getWorleyPoints() {
        return ref worleyPoints;
    }

    public void nextLODLevel()
    {
        for (int i = 0; i < LOD.Count; ++i)
        {
            LOD[i].traverseAndGenerate(LOD[i]);
        }
    }

    public void checkPatchDistances(Vector3 playerPos)
    {
        for (int i = 0; i < LOD.Count; ++i)
        {
            LOD[i].LODbyDistance(LOD[i], playerPos);
        }
    }

    public void prevLODLevel()
    {
        for (int i = 0; i < LOD.Count; ++i)
        {
            LOD[i].prevLOD(LOD[i]);
        }

    }

    //THIS SHOULD ALL BE IN THE PATCH ITSELF, NOT THE PARENT. IT WILL MAKE IT MUCH EASIER WHEN IMPLEMENTING AN LOD SYSTEM
    void GeneratePatch(PatchConfig aConf, int u, int v)
    {
        GameObject patch = new GameObject(aConf.name + "_" + u + v);
        patch.transform.tag = "Planet";

        //patch.AddComponent<GeneratePlane>();
        addMeshGenerationScript(patch);

        patch.transform.parent = transform;
        patch.transform.localEulerAngles = Vector3.zero; //zero out local position and rotation
        patch.transform.localPosition = Vector3.zero;
        Vector2 startingLOD = Vector2.one;

        patch.GetComponent<GeneratePlane>().patch = aConf;
            

        //add patch to the LOD system
        PatchLOD newLOD = new PatchLOD(patch.gameObject, null);
        LOD.Add(newLOD);
    }

    public void addMeshGenerationScript(GameObject patchChild)
    {
        switch (planetType)
        {
            case 0:
                patchChild.AddComponent<HotPlanetNoise>();
                break;
            case 1:
                patchChild.AddComponent<IcePlanetNoise>();
                break;
            case 2:
                patchChild.AddComponent<LifePlanetNoise>();
                break;
            case 3:
                patchChild.AddComponent<BarrenPlanetNoise>();
                break;
            case 4:
                patchChild.AddComponent<DesertPlanetNoise>();
                break;
            case 5:
                patchChild.AddComponent<GasPlanetNoise>();
                break;
            default:
                patchChild.AddComponent<BarrenPlanetNoise>();
                break;
        }
    }

    public float getAtmosphereDistance() {
        return (radius + atmosphereHeight);
    }

    public float getRadius() {
        return radius;
    }

    public int getPlanetType() {
        return planetType;
    }

    public float getOceanFloor() {
        return oceanFloor;
    }

    public float getOceanMultiplier() {
        return oceanMultiplier;
    }

    public float getLandMultiplier()
    {
        return landMultiplier;
    }

    public UInt64 getSeed() {
        return seed;
    }

    public float getInitialDistanceThreshold() {
        return initialDistanceThreshold;
    }

    public void setEventManager(Event_Manager_Script e_m) { event_manager = e_m; }


    void GeneratePatches()
    {
        //generate LOD tree

        
        for (int i = 0; i < 6; i++)
        {
            GeneratePatch(patches[i], 1, 1); //GENERATES CUBE SIDE. THE 1, 1 ARGUMENT REFERS TO LOD
        }
    }

    public Vector3 getSunPos() {
        return sunPos;
    }




    public static float Perlin3d(float x, float y, float z)
    {
        float AB = Mathf.PerlinNoise(x, y);
        float BC = Mathf.PerlinNoise(y, z);
        float AC = Mathf.PerlinNoise(x, z);

        float BA = Mathf.PerlinNoise(y, x);
        float CB = Mathf.PerlinNoise(z, y);
        float CA = Mathf.PerlinNoise(z, x);

        float ABC = AB + BC + AC + BA + CB + CA;

        return ABC / 6f;
    }
}
