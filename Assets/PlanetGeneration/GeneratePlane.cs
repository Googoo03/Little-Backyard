using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Simplex;
using Worley;


public abstract class GeneratePlane : MonoBehaviour
{
    public int xVertCount = 16, yVertCount = 16;
    private float radius;
    public Material patchMaterial;
    Color[] regions; //will i run into trouble if this is pointing to a reference?
    float[] heights;
    public PatchConfig patch;

    protected int octaves;
    protected float lacunarity;
    protected float persistance;
    protected int seed;
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

    public bool generateFoliage;

    public abstract float NoiseValue(Vector3 pos, float scale);

    protected void setComputeNoiseVariables(ref ComputeShader shader)
    {
        shaderHandle = shader.FindKernel("CSMain");
        shader.SetInt("seed", 0);

        shader.SetTexture(shaderHandle, "Result", texture);
        shader.SetBuffer(shaderHandle, "vertexBuffer", verts);

        shader.SetBuffer(shaderHandle, "vertexWorldBuffer", worldVerts);

        shader.SetFloat("octaves", octaves);
        shader.SetFloat("persistance", persistance);
        shader.SetFloat("lacunarity", lacunarity);

        shader.SetFloat("oceanMultiplier", oceanMulitplier);
        shader.SetFloat("landMultiplier", landMultiplier);
        shader.SetFloat("seaLevel", oceanFloor);
    }
    public void Generate(PatchConfig planePatch,float LODstep) {


        texture = new RenderTexture(16, 16, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true
        };
        texture.Create();
        

        MeshFilter mf = this.gameObject.AddComponent<MeshFilter>();
        MeshRenderer rend = this.gameObject.AddComponent<MeshRenderer>();

        Material planetMaterial = Instantiate(Resources.Load("Planet_Shader", typeof(Material))) as Material;
        planetMaterial.SetFloat("_DisplacementStrength",0.1f);

        float TypePlanet = (float)planePatch.planetObject.GetComponent<Sphere>().getPlanetType();
        planetMaterial.SetFloat("_PlanetType", TypePlanet+.99f); //I guess pixel sampling is 1-indexed?


        rend.sharedMaterial = planetMaterial;
        Mesh m = mf.sharedMesh = new Mesh();
        patch = planePatch;

        xVertCount = planePatch.vertices.x;
        yVertCount = planePatch.vertices.y;

        radius = planePatch.radius;

        Vector2 offset = new Vector2(-0.5f, -0.5f) + planePatch.LODOffset; //to center all side meshes. Multiply by LODoffset to give correct quadrant
        Vector2 step = new Vector2(1f / (xVertCount - 1), 1f / (yVertCount - 1)); //determines the distance or "step" amount between vertices
        step *= LODstep; //make smaller steps if higher lod


        Vector3[] vertices = new Vector3[xVertCount * yVertCount];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];

        //Texture2D tex = new Texture2D(xVertCount, yVertCount);

        //float maxHeightReached = 1;

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
        int[] indices = new int[(xVertCount - 1) * (yVertCount - 1) * 4];
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
        DispatchNoise(ref vertices); //change the vertices, then set them

        m.vertices = vertices;
        m.normals = normals;
        m.uv = uvs;
        m.SetIndices(indices, MeshTopology.Quads, 0);
        m.RecalculateBounds();

        mf.sharedMesh.SetTriangles(mf.sharedMesh.GetTriangles(0), 0);

        this.gameObject.AddComponent<MeshCollider>();


        m.RecalculateNormals();

        transform.GetComponent<Renderer>().material.SetTexture("_HeightMap", texture);
        transform.GetComponent<Renderer>().material.SetTextureScale("_HeightMap", new Vector2(1 << patch.LODlevel, 1 << patch.LODlevel));

        if(generateFoliage) GenerateFoliage(getPosition(planePatch,planePatch.LODlevel)); //generate foliage if and only if it's at the lowest level
        //DispatchFoliage();
    }

    protected abstract void DispatchNoise(ref Vector3[] vertices);

    protected abstract void GenerateFoliage(Vector3 startPos);

    protected abstract void DispatchFoliage();

    protected abstract void createPatchTexture(ref Material mat, int x, int y, float currentHeight);

    private void Update()
    {
        if(generateFoliage) DispatchFoliage();
    }
    /*float OctaveNoise(Vector3 vec,ref float range, ref float noiseHeight, int seed, float scale, int octaves, float lacunarity, float persistance)
    {

        frequency = 1;
        amplitude = 1;

        for (int g = 0; g < octaves; g++)
        {
            
            //THIS LINE HERE WILL CHANGE TO ACCOMODATE ADDITIONAL ALGORITHMS


            //WHAT IF WE WANT OCTAVES FOR ONE ALGORITHM AND NOT FOR ANOTHER??
            float perlinValue = NoiseValue(vec, scale);

            noiseHeight += perlinValue * amplitude;

            amplitude *= persistance;
            frequency *= lacunarity;
            range += amplitude / 4;
        }
        return noiseHeight;
    }*/

    public Vector3 getPosition(PatchConfig planePatch, float LODstep) { //returns the middle vertex position. Is used to
                                                                        //measure distance for LOD

        float xVert = planePatch.vertices.x;
        float yVert = planePatch.vertices.y;

        Vector2 offset = new Vector2(-0.5f, -0.5f) + planePatch.LODOffset; //to center all side meshes. Multiply by LODoffset to give correct quadrant
        Vector2 step = new Vector2(1f / (xVert - 1), 1f / (yVert - 1)); //determines the distance or "step" amount between vertices
        step *= LODstep; //make smaller steps if higher lod

        Vector2 p = offset + new Vector2( (xVert/2) * step.x, (yVert/2) * step.y);

        Vector3 vec = ((planePatch.uAxis * p.x) + (planePatch.vAxis * p.y) + (planePatch.height * 0.5f));
        vec = vec.normalized * radius;

        vec += this.transform.position;
        return vec;
    }
}
