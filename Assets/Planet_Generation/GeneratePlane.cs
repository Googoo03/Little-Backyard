using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Simplex;
using Worley;
using UnityEngine.Rendering;
using Unity.Collections;
using System;


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

    //FOLIAGE PARAMETERS
    public bool generateFoliage;
    protected int tree_k;
    protected int tree_nummax = 256;
    protected int tree_radius;

    protected int rock_k;
    protected int rock_nummax;
    protected int rock_radius;

    //EVENT MANAGER
    Event_Manager_Script event_manager;

    protected bool foliageGenerationReturned = false;

    public abstract float NoiseValue(Vector3 pos, float scale);

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
        shader.SetInt("seed", 0);

        shader.SetTexture(shaderHandle, "Result", texture);
        shader.SetBuffer(shaderHandle, "vertexBuffer", verts);

        shader.SetBuffer(shaderHandle, "vertexWorldBuffer", worldVerts);

        shader.SetInt("resolution", 15); //should this be dynamic

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
        Generate(patch);
    }
    ~GeneratePlane() {
        texture.Release();
    }

    public void Generate(PatchConfig planePatch)
    {


        texture = new RenderTexture(15, 15, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true
        };
        texture.Create();

        

        mf = this.gameObject.AddComponent<MeshFilter>();
        rend = this.gameObject.AddComponent<MeshRenderer>();

        Material planetMaterial = Instantiate(Resources.Load("Planet_Shader", typeof(Material))) as Material;
        planetMaterial.SetFloat("_DisplacementStrength", 0.1f);

        float TypePlanet = (float)planePatch.planetObject.GetComponent<Sphere>().getPlanetType();
        event_manager = planePatch.planetObject.GetComponent<Sphere>().getEvent_Manager();
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
        seed = planePatch.planetObject.GetComponent<Sphere>().getSeed();


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
        DispatchNoise(vertices, position); //there's no ref here, optimization opportunity?

        m.RecalculateNormals();

        //////////////////////SET THE TEXTURE SIZE, SCALE, AND TEXTURE ITSELF
        transform.GetComponent<Renderer>().material.SetTexture("_HeightMap", texture);
        transform.GetComponent<Renderer>().material.SetTextureScale("_HeightMap", new Vector2(1 << patch.LODlevel, 1 << patch.LODlevel));

        //if (generateFoliage) GenerateFoliage(ref vertices, transform.position); //generate foliage if and only if it's at the lowest level
        //DispatchFoliage();

        transform.GetComponent<Renderer>().material.SetTextureOffset("_HeightMap", -patch.LODOffset * (1 << patch.LODlevel));
        transform.GetComponent<Renderer>().material.SetVector("_Tile", new Vector4(1 << patch.LODlevel, 1 << patch.LODlevel, 0, 0));

        float textureTiling = (1 << patch.maxLOD) / (1 << patch.LODlevel);
        transform.GetComponent<Renderer>().material.SetVector("_Tiling", new Vector4(textureTiling, textureTiling, 0, 0));


        transform.GetComponent<Renderer>().material.SetVector("_Offset", new Vector4(patch.textureOffset.x, patch.textureOffset.y, 0, 0));

        rend.material.SetVector("_SunPos", event_manager.get_sun().transform.position);
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

        //mf.sharedMesh.SetTriangles(mf.sharedMesh.GetTriangles(0), 0);
        if (generateFoliage) GenerateFoliage(ref vertices, transform.position);

        this.gameObject.AddComponent<MeshCollider>();

        if (patch.LODlevel > 0)
        {
            parent.GetComponent<MeshRenderer>().enabled = false; //not good practice, should be done through the PatchLOD tree
            parent.GetComponent<MeshCollider>().enabled = false; //however, the PatchLOD tree is messed up rn so this is a workaround.
        }


        return;
    }

    protected abstract void DispatchNoise(Vector3[] vertices, Vector3 origin);

    protected abstract void GenerateFoliage(ref Vector3[] vertices, Vector3 origin);

    protected abstract void DispatchFoliage();

    


    private void Update()
    {
        if(generateFoliage) DispatchFoliage();
        
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
