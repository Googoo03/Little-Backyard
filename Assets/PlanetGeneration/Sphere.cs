using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public struct PatchConfig
{
    public string name;
    public int maxLOD;
    public int LODlevel;
    public Vector2 LODOffset;
    public Vector3 uAxis;
    public Vector3 vAxis;
    public Vector3 height;
    public Vector2Int vertices;
    public GameObject planetObject;
    public float distanceThreshold;
    public PatchConfig(string aName, Vector3 aUAxis, Vector3 aVAxis, int level,Vector2 LODoffset, Vector2Int xyVert, GameObject planet, float distanceT)
    {
        //seed, persistance, lacunarity, octaves, ref heightCurve, planetType, ref regions, ref heights
        name = aName;
        uAxis = aUAxis;
        vAxis = aVAxis;
        height = Vector3.Cross(vAxis, uAxis);
        LODlevel = level;
        LODOffset = LODoffset;
        vertices = xyVert;
        planetObject = planet;
        distanceThreshold = distanceT;
        maxLOD = 5;
        
    }
}

public struct PlanetProperties {
    public int seed;
    public int octaves;
    public float persistance;
    public float lacunarity;
    //private AnimationCurve[] heightCurve; //SINCE WE CANT HAVE A REFERENCE TO THE HEIGHT CURVE, WE NEED AN EVALUATE FUNCTION
    // FROM THE PLANET??
}



public class Sphere : MonoBehaviour
{
    //Assign each cube-sphere face
    
    //////////////////////////////////////

    public int uPatchCount = 1;
    public int vPatchCount = 1; //purpose is unknown
    public int xVertCount;
    public int yVertCount;
    public float radius = 5f;
    [SerializeField] private int seed;
    [SerializeField] private float scale;
    [SerializeField] private int octaves;
    [SerializeField] private float persistance;
    [SerializeField] private float lacunarity;

    public float oceanFloor;
    public float oceanMultiplier;

    public float landMultiplier;

    public Texture2D regionReference;

    public Color[] regions; //turn this into a 2D array and access directly?
    float[] heights = {0.5f, 0.7f, 0.8f, 0.9f };

    private int planetType; // 0 = Hot, 1 = Ice, 2 = Life, 5 = Gas, 4 = Desert, 3 = Barren
    public float pscale;

    
    private PatchConfig[] patches;
    private List<PatchLOD> LOD;

    [SerializeField]private List<Vector3> worleyPoints = new List<Vector3>();

    public bool nextLOD;
    public bool prevLOD;
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

        //create seed using a hash
        var hash = new Hash128();
        hash.Append(transform.position.x);
        hash.Append(transform.position.y);
        hash.Append(transform.position.z);

        seed = hash.GetHashCode(); //this may cause issues because it is so large, but this is just for testing purposes
        ////////////////////////////////////


        //MAKE A NEW WAY OF GENERATING TYPE??? UGLY TO READ??
        //planetType = Mathf.FloorToInt((Perlin3d(px + seed, py + seed, pz + seed)*1000) % 6);
        //planetType = Random.Range(0,6);

        //set the planet type and name
        planetType = 3;
        transform.name = "Planet" + planetType.ToString();


        //set the atmosphere color
        Color atmosphereColor = regionReference.GetPixel(4, planetType);
        transform.GetChild(0).GetComponent<Renderer>().material.SetColor("_Atmosphere_Color", atmosphereColor);
        ////////////////////////////

        //create all 6 sides of the sphere-cube
        Vector2Int xyVert = new Vector2Int(xVertCount, yVertCount);
        patches = new PatchConfig[]
        {
         new PatchConfig("top", Vector3.right, Vector3.forward,0, Vector2.zero,xyVert,transform.gameObject,4),
         new PatchConfig("bottom", Vector3.left, Vector3.forward, 0, Vector2.zero, xyVert, transform.gameObject, 4),
         new PatchConfig("left", Vector3.up, Vector3.forward, 0, Vector2.zero, xyVert, transform.gameObject, 4),
         new PatchConfig("right", Vector3.down, Vector3.forward,0, Vector2.zero,xyVert, transform.gameObject,4),
         new PatchConfig("front", Vector3.right, Vector3.down, 0, Vector2.zero, xyVert, transform.gameObject, 4),
         new PatchConfig("back", Vector3.right, Vector3.up, 0, Vector2.zero, xyVert, transform.gameObject, 4)
        };

        //create a list of points for worleyNoise
        generateWorleyPoints(25);

        //generate the patches when finished configuring
        GeneratePatches();
    }
    private void generateWorleyPoints(int num) {
        for (int i = 0; i < num; ++i)
        {
            Vector3 point = new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100));
            point.Normalize();
            //this should be multiplied by the radius in the future
            point += transform.position;
            worleyPoints.Add(point);
        }
    }

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

    public void checkPatchDistances(GameObject player)
    {
        for (int i = 0; i < LOD.Count; ++i)
        {
            LOD[i].LODbyDistance(LOD[i], player);
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

        //patch.AddComponent<GeneratePlane>();
        addMeshGenerationScript(patch);

        patch.transform.parent = transform;
        patch.transform.localEulerAngles = Vector3.zero; //zero out local position and rotation
        patch.transform.localPosition = Vector3.zero;
        Vector2 startingLOD = Vector2.one;

        patch.GetComponent<GeneratePlane>().Generate(aConf,1);

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
    public float getHeightArrayValue(int index)
    {
        //for safety you should throw an exception here if out of range;
        return heights[index];
    }

    public Color getRegionColor(int index)
    {
        return regions[index];
    }

    public int getRegionLength()
    {
        return regions.Length;
    }

    public int getOctaves()
    {
        return octaves;
    }

    public float getLacunarity()
    {
        return lacunarity;
    }

    public float getPersistance()
    {
        return persistance;
    }

    public int getSeed() {
        return seed;
    }
    public float getScale() {
        return scale;
    }

    void GeneratePatches()
    {
        //generate LOD tree

        
        for (int i = 0; i < 6; i++)
        {
            GeneratePatch(patches[i], 1, 1); //GENERATES CUBE SIDE. THE 1, 1 ARGUMENT REFERS TO LOD
        }
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
