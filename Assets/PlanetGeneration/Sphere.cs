using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public struct PatchConfig
{
    public string name;
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
    public int seed;
    public float scale;
    public int octaves;
    public float persistance;
    public float lacunarity;

    

    public Color[] regions; //turn this into a 2D array and access directly?
    float[] heights = {0.5f, 0.7f, 0.8f, 0.9f };
    public AnimationCurve[] heightCurve;

    public GameObject ore;
    public GameObject iron;
    public GameObject meteorite;
    public float oreSeed;
    public float oreScale;

    public int planetType; // 0 = Hot, 1 = Ice, 2 = Life, 5 = Gas, 4 = Desert, 3 = Barren
    public float pscale;

    
    private PatchConfig[] patches;
    private List<PatchLOD> LOD;


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

    public void nextLODLevel() {
        for (int i = 0; i < LOD.Count; ++i)
        {
            LOD[i].traverseAndGenerate(LOD[i]);
        }
    }

    public void checkPatchDistances(GameObject player) {
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

    void Start()
    {
        LOD = new List<PatchLOD>() { };

        seed = Mathf.FloorToInt(Time.time);
        float px = (transform.position.x / pscale);
        float py = transform.position.y / pscale;
        float pz = transform.position.z / pscale;

        //MAKE A NEW WAY OF GENERATING TYPE??? UGLY TO READ??
        //planetType = Mathf.FloorToInt((Perlin3d(px + seed, py + seed, pz + seed)*1000) % 6);
        planetType = Random.Range(0, 6);

        transform.name = "Planet" + planetType.ToString();


        //WE NEED TO READ "planetColors" FILE TO DETERMINE REGION ARRAY.
        //ADDITIONALLY, WE NEED TO POSSIBLY COME UP WITH A WAY TO HAVE
        //UNIQUE THRESHOLDS FOR EACH PLANET
        //
        //ONCE THAT'S DONE CHANGE THE ASSIGNMENT OF COLOR TO BOTH STEEPNESS
        //AND ELEVATION, RATHER THAN JUST ELEVATION
        if (planetType == 0) {
            regions[0] = Color.red;
            regions[1] = Color.gray;
            regions[2] = Color.black;
            regions[3] = Color.gray;
            transform.Find("Atmosphere").GetComponent<Renderer>().material.color = Color.gray;
        }
        if (planetType == 1)
        {
            regions[0] = new Color(.5f, .9f, .9f);
            regions[1] = new Color(.8f,.99f,1);
            regions[2] = new Color(.9f,.9f,.9f);
            regions[3] = Color.white;
            transform.Find("Atmosphere").GetComponent<Renderer>().material.color = Color.white;
        }
        if (planetType == 2)
        {
            regions[0] = new Color(.1f,.5f,.8f);
            regions[1] = Color.green;
            regions[2] = new Color(.5f,.45f,.4f);
            regions[3] = Color.gray;
        }
        if (planetType == 5)
        {
            regions[0] = new Color(.3f, .3f, .3f);
            regions[1] = new Color(.6f,.6f,.6f);
            regions[2] = new Color(.5f, .45f, .4f);
            regions[3] = Color.gray;
            transform.Find("Atmosphere").GetComponent<Renderer>().material.color = new Color(0,0,0,0);
        }
        if (planetType == 4)
        {
            regions[0] = new Color(1f, .8f, .4f);
            regions[1] = new Color(.9f, .6f, .1f);
            regions[2] = new Color(.6f, .55f, .5f);
            regions[3] = Color.gray;
            transform.Find("Atmosphere").GetComponent<Renderer>().material.color = new Color(1,.7f,0);
        }
        if (planetType == 3)
        {
            regions[0] = Color.gray;
            regions[1] = new Color(.6f, .6f, .6f);
            regions[2] = new Color(.7f, .7f, .7f);
            regions[3] = Color.gray;
            transform.Find("Atmosphere").GetComponent<Renderer>().material.color = new Color(0,0,0,0);
        }
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
        
        GeneratePatches();
    }
    //we NEED AN EVALUATE FUNCTION FOR THE HEIGHTCURVE
    public float evaluateHeightCurve(int index, float value) {
        return heightCurve[index].Evaluate(value);
    }








    //THIS SHOULD ALL BE IN THE PATCH ITSELF, NOT THE PARENT. IT WILL MAKE IT MUCH EASIER WHEN IMPLEMENTING AN LOD SYSTEM
    void GeneratePatch(PatchConfig aConf, int u, int v)
    {
        GameObject patch = new GameObject(aConf.name + "_" + u + v);

        patch.AddComponent<GeneratePlane>();

        patch.transform.parent = transform;
        patch.transform.localEulerAngles = Vector3.zero; //zero out local position and rotation
        patch.transform.localPosition = Vector3.zero;
        Vector2 startingLOD = Vector2.one;

        patch.GetComponent<GeneratePlane>().Generate(aConf,1);

        //add patch to the LOD system
        PatchLOD newLOD = new PatchLOD(patch.gameObject, null);
        LOD.Add(newLOD);
    }

    public int getPlanetType() {
        return planetType;
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
