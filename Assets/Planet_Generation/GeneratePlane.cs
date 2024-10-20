using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Simplex;
using Worley;
using Poisson;
using UnityEngine.Rendering;
using Unity.Collections;
using System;
using static UnityEngine.UI.Image;

public abstract class GeneratePlane : MonoBehaviour
{
    public int xVertCount = 16, yVertCount = 16;
    protected float radius;
    public Material patchMaterial;

    public PatchConfig patch;
    public float LODstep; //powerof2Frac

    protected int octaves;
    protected float lacunarity;
    protected float persistance;
    protected UInt64 seed;
    [SerializeField] protected float scale;

    protected float oceanFloor;
    protected float oceanMulitplier;
    protected float landMultiplier;
    protected float frequency;
    protected float amplitude;
    protected float domainWarp;
    protected float range;

    protected bool changeHeight;

    [SerializeField]protected RenderTexture texture;
    protected ComputeBuffer verts;
    protected ComputeBuffer worldVerts;
    protected int shaderHandle;

    //MESH DETAILS
    protected Mesh m;
    protected MeshFilter mf;
    protected MeshRenderer rend;
    protected Vector3[] vertices;
    protected Vector3[] normals;
    protected Vector2[] uvs;
    protected int[] indices;

    private GameObject parent;

    //WIND PARTICLE SYSTEM
    private GameObject wind_normal;
    private GameObject wind_swirl;
    [SerializeField] protected double timeMarker;
    private double timeElapsed;

    //SCRIPTABLE OBJECT
    //
    //This will contain all the necessary information for foliage generation and noise parameters
    //Will be loaded from resources by individual planet class
    [SerializeField] protected Planet_Scriptable_Obj planet_preset;

    //List of Resource Objects to be allocated from the object pool
    [SerializeField] private List<List<GameObject>> foliage_objs = new List<List<GameObject>>();

    //List of wind objects to be allocated from the wind pool
    [SerializeField] private List<GameObject> wind_obj = new List<GameObject>();

    public bool generateFoliage;
    protected int tree_k;
    protected int tree_nummax = 256;
    protected int tree_radius;

    protected int rock_k;
    protected int rock_nummax;
    protected int rock_radius;

    //EVENT MANAGER
    Event_Manager_Script event_manager;

    //POOL MANAGER
    protected Object_Pool_Manager object_pool_manager;

    //WIND POOL MANAGER
    protected Object_Pool_Manager wind_pool_manager;

    //SHADER PARAMS
    [SerializeField] protected Vector3 SunPos;

    protected bool foliageGenerationReturned = false;

    public abstract float NoiseValue(Vector3 pos, float scale);

    public void Awake()
    {
        
    }

    protected int generateUniqueSeed(Vector3 pos) {

        //FIX THIS
        //new line for the sake of invoking a domain relaod
        var hash = new Hash128();
        hash.Append(pos.x);
        hash.Append(pos.y);
        hash.Append(pos.z);
        
        return hash.GetHashCode();
    }



    protected void setComputeNoiseVariables(ref ComputeShader shader)
    {
        shaderHandle = shader.FindKernel("CSMain");
        shader.SetInt("seed", (int)seed);

        shader.SetTexture(shaderHandle, "Result", texture);
        shader.SetBuffer(shaderHandle, "vertexBuffer", verts);

        shader.SetBuffer(shaderHandle, "vertexWorldBuffer", worldVerts);

        shader.SetInt("resolution", 15); //should this be dynamic

        shader.SetFloat("domainWarp", domainWarp);
        shader.SetFloat("frequency", 1f);

        shader.SetFloat("octaves", octaves);
        shader.SetFloat("persistance", persistance);
        shader.SetFloat("lacunarity", lacunarity);

        shader.SetFloat("oceanMultiplier", oceanMulitplier);
        shader.SetFloat("landMultiplier", landMultiplier);
        shader.SetFloat("seaLevel", oceanFloor);
    }


    private void Start() //this may need to be removed
    {
        parent = transform.parent.gameObject;

        //Find object manager script at start. Expensive.
        if(!object_pool_manager) object_pool_manager = GameObject.FindWithTag("Object_Manager").GetComponent<Object_Pool_Manager>();

        if(!wind_pool_manager) wind_pool_manager = GameObject.FindWithTag("Wind_Manager").GetComponent<Object_Pool_Manager>();

        //Load wind particle systems from Resources folder
        wind_swirl = (GameObject)(Resources.Load("FX/Wind/Wind_Swirl"));
        wind_normal = (GameObject)(Resources.Load("FX/Wind/Wind_Normal"));
        timeElapsed = 0;
        timeMarker += UnityEngine.Random.Range(10, 50);

        Generate(patch);
    }
    ~GeneratePlane() {
        texture.Release();
    }

    private void OnDestroy()
    {
        if (this == null) return;


        foliage_objs.ForEach(list => { 
            list.ForEach(item => { item.SetActive(false); });

            if (!object_pool_manager) return;
            object_pool_manager.releasePoolObjs(ref list);
        }) ;
        

        texture.Release();
    }

    public void Generate(PatchConfig planePatch)
    {


        texture = new RenderTexture(15, 15, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true
        };
        texture.Create();


        planet_preset = (Planet_Scriptable_Obj)Resources.Load("Planet_Presets/" + planePatch.planetObject.name.ToString());
        mf = this.gameObject.AddComponent<MeshFilter>();
        rend = this.gameObject.AddComponent<MeshRenderer>();
        Sphere planetScript = planePatch.planetObject.GetComponent<Sphere>();

        Material planetMaterial = Instantiate(Resources.Load("Planet_Shader", typeof(Material))) as Material;
        planetMaterial.SetFloat("_DisplacementStrength", 0.1f);

        float TypePlanet = (float)planetScript.getPlanetType();
        event_manager = planetScript.getEvent_Manager();
        planetMaterial.SetFloat("_PlanetType", TypePlanet); //I guess pixel sampling is 1-indexed?


        rend.sharedMaterial = planetMaterial;
        m = mf.sharedMesh = new Mesh();
        //patch = planePatch;

        xVertCount = planePatch.vertices.x;
        yVertCount = planePatch.vertices.y;

        radius = patch.radius;

        Vector2 offset = new Vector2(-0.5f, -0.5f) + planePatch.LODOffset; //to center all side meshes. Multiply by LODoffset to give correct quadrant
        Vector2 step = new Vector2(1f / (xVertCount - 1), 1f / (yVertCount - 1)); //determines the distance or "step" amount between vertices

        LODstep = 1f / (1 << (patch.LODlevel));
        step *= LODstep; //make smaller steps if higher lod


        vertices = new Vector3[xVertCount * yVertCount];
        normals = new Vector3[vertices.Length];
        uvs = new Vector2[vertices.Length];

        //GET NECESSARY VALUES FOR NOISE FROM PARENT PLANET
        seed = planetScript.getSeed();


        for (int y = 0; y < yVertCount; y++)
        {
            for (int x = 0; x < xVertCount; x++)
            {

                int i = x + y * xVertCount;
                Vector2 p = offset + new Vector2(x * step.x, y * step.y); //determines vertex location in grid

                uvs[i] = p + Vector2.one * 0.5f;

                Vector3 vec = (planePatch.uAxis * p.x) + (planePatch.vAxis * p.y) + (planePatch.height * 0.5f); //determine plane vertex based on direction. p determines
                                                                                                                //vertex location in grid

                //float noiseHeight = 0f; // should return a value between 0 and 1
                vec = vec.normalized; //makes it a sphere

                range = 1f;

                normals[i] = vec;
                vertices[i] = vec * radius;

            }
        }

        //SET INDICES FOR THE MESH
        indices = new int[(xVertCount - 1) * (yVertCount - 1) * 4];
        for (int y = 0; y < yVertCount - 1; y++)
        {
            for (int x = 0; x < xVertCount - 1; x++)
            {
                int i = (x + y * (xVertCount - 1)) * 4;
                indices[i] = x + y * xVertCount;
                indices[i + 1] = x + (y + 1) * xVertCount;
                indices[i + 2] = x + 1 + (y + 1) * xVertCount;
                indices[i + 3] = x + 1 + y * xVertCount;
            }
        }
        /////////OUTSIDE INFORMATION USED FOR THREAD


        Vector3 position = transform.position; //world position of planet
        DispatchNoise(ref vertices, position); //there's no ref here, optimization opportunity?

        m.RecalculateNormals();

        //////////////////////SET THE TEXTURE SIZE, SCALE, AND TEXTURE ITSELF

        planetMaterial.SetTexture("_HeightMap", texture);
        planetMaterial.SetTextureScale("_HeightMap", new Vector2(1 << patch.LODlevel, 1 << patch.LODlevel));

        planetMaterial.SetTextureOffset("_HeightMap", -patch.LODOffset * (1 << patch.LODlevel));
        planetMaterial.SetVector("_Tile", new Vector4(1 << patch.LODlevel, 1 << patch.LODlevel, 0, 0));

        float textureTiling = (1 << patch.maxLOD) / (1 << patch.LODlevel);
        planetMaterial.SetVector("_Tiling", new Vector4(textureTiling, textureTiling, 0, 0));


        planetMaterial.SetVector("_Offset", new Vector4(patch.textureOffset.x, patch.textureOffset.y, 0, 0));

        //if there's not an event manager present, assume test
        planetMaterial.SetVector("_SunPos", patch.planetObject.GetComponent<Sphere>().getSunPos());
        /////////////////////////////////////////////////////////////////////
    }

    protected void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        NativeArray<Vector3> _vertices;
        _vertices = request.GetData<Vector3>();
        verts.Release();
        worldVerts.Release();

        _vertices.CopyTo(vertices);
        m.vertices = vertices;
        m.normals = normals;
        m.uv = uvs;
        m.SetIndices(indices, MeshTopology.Quads, 0);
        m.RecalculateBounds();
        m.RecalculateNormals();

        foliageGenerationReturned = true;

        //Generate foliage if and only if its at the lowest LOD level
        if (generateFoliage) GenerateFoliage(ref vertices);

        gameObject.AddComponent<MeshCollider>();

        if (patch.LODlevel > 0)
        {
            parent.GetComponent<MeshRenderer>().enabled = false; //not good practice, should be done through the PatchLOD tree
            parent.GetComponent<MeshCollider>().enabled = false; //however, the PatchLOD tree is messed up rn so this is a workaround.
        }


        return;
    }

    protected abstract void DispatchNoise(ref Vector3[] vertices, Vector3 origin);


    protected void RequestWindObj() {
        

    }
    protected void GenerateFoliage(ref Vector3[] vertices) {

        //This should be changed to a pointer in the future
        List<mesh_pair> mesh_list = planet_preset.getMesh_List();
        PoissonDisc poissonSampling = new PoissonDisc(); //used for generating foliage
        int seed;
        int mid_index;

        for (int i = 0; i < mesh_list.Count; ++i) {
            List<Vector3> foliage_positions = new List<Vector3>();


            
            mid_index = xVertCount * (yVertCount / 2) + (xVertCount / 2); //calculates the middle index of a square array. Like, direct center of square.
            poissonSampling.setDensity(3f);


            seed = generateUniqueSeed(vertices[mid_index] + new Vector3(i,0,0));
            poissonSampling.setSeedPRNG(seed);
            poissonSampling.generatePoissonDisc(ref foliage_positions, ref vertices, mesh_list[i].poissonK, xVertCount * yVertCount, xVertCount, yVertCount, mesh_list[i].poissonRadius);

            List<GameObject> temp_foliage_objs = new List<GameObject>();
            object_pool_manager.requestPoolObjs(ref temp_foliage_objs, foliage_positions.Count);
            foliage_objs.Add(temp_foliage_objs); //Expensive, change later

            for (int j = 0; j < temp_foliage_objs.Count; ++j)
            { //add the tree positions and subsequent rotations to the matrix buffer

                Vector3 lookVec = foliage_positions[j];
                Quaternion rot = Quaternion.LookRotation(lookVec);
                Vector3 sca = Vector3.one * .02f;

                foliage_objs[i][j].SetActive(true);
                foliage_objs[i][j].transform.position = foliage_positions[j] + transform.position;
                foliage_objs[i][j].transform.rotation = rot;
                foliage_objs[i][j].transform.localScale = sca;
                foliage_objs[i][j].GetComponent<MeshFilter>().mesh = mesh_list[i].mesh;
                foliage_objs[i][j].GetComponent<MeshRenderer>().material = mesh_list[i].mat;
                foliage_objs[i][j].GetComponent<MeshCollider>().sharedMesh = mesh_list[i].mesh;
                foliage_objs[i][j].GetComponent<Resource_Class>().setResourcePreset(mesh_list[i].resource);
                foliage_objs[i][j].tag = "Resource";
            }
        }
    }

    protected abstract void GenerateFoliage(ref Vector3[] vertices, Vector3 origin);

    protected abstract void DispatchFoliage();

    


    private void Update()
    {
        if (!generateFoliage) return;
        DispatchFoliage();

        //have a timer for the wind. Once the timer has expired, spawn a wind particle
        timeElapsed += Time.deltaTime;
        if (timeElapsed > timeMarker) {
            timeElapsed = 0;

            wind_pool_manager.requestPoolObjs(ref wind_obj, 1);
            Vector3 pos = vertices[xVertCount * (yVertCount / 2) + (xVertCount / 2)];
            Vector3 lookVec = pos;
            Quaternion rot = Quaternion.LookRotation(lookVec);
            Vector3 sca = Vector3.one * .02f;
            wind_obj[0].SetActive(true);
            wind_obj[0].transform.position = transform.position + pos;
            wind_obj[0].transform.rotation = rot;
            wind_obj.Remove(wind_obj[0]);
            //Instantiate(UnityEngine.Random.Range(0, 2) == 0 ? wind_normal : wind_swirl, transform.position + pos, rot);
        }
    }


    public Vector3 getPosition() { //returns the middle vertex position. Is used to
                                                                        //measure distance for LOD

        float xVert = patch.vertices.x;
        float yVert = patch.vertices.y;

        Vector2 offset = new Vector2(-0.5f, -0.5f) + patch.LODOffset; //to center all side meshes. Multiply by LODoffset to give correct quadrant
        Vector2 step = new Vector2(1f / (xVert - 1), 1f / (yVert - 1)); //determines the distance or "step" amount between vertices
        step *= LODstep; //make smaller steps if higher lod

        Vector2 p = offset + new Vector2( (xVert/2) * step.x, (yVert/2) * step.y); //finds center position of patch

        Vector3 vec = ((patch.uAxis * p.x) + (patch.vAxis * p.y) + (patch.height * 0.5f));
        vec = vec.normalized * patch.radius;

        vec += transform.position;
        return vec;
    }
}
