using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


public class GeneratePlane : MonoBehaviour
{



    
    //THEN WORK ON A FUNCTIONING LOD SYSTEM USING A QUADTREE MECHANIC



    // Start is called before the first frame update
    public int xVertCount, yVertCount;
    private int radius;
    public Material patchMaterial;
    Color[] regions; //will i run into trouble if this is pointing to a reference?
    float[] heights;
    public PatchConfig patch;
    public void Generate(PatchConfig planePatch,float LODstep) {

        MeshFilter mf = this.gameObject.AddComponent<MeshFilter>();
        MeshRenderer rend = this.gameObject.AddComponent<MeshRenderer>();

        rend.sharedMaterial = patchMaterial;
        Mesh m = mf.sharedMesh = new Mesh();
        patch = planePatch;

        xVertCount = planePatch.vertices.x;
        yVertCount = planePatch.vertices.y;
        radius = 1;
        //regions = regionArray;
        //heights = heightArray;

        Vector2 offset = new Vector2(-0.5f, -0.5f) + planePatch.LODOffset; //to center all side meshes. Multiply by LODoffset to give correct quadrant
        Vector2 step = new Vector2(1f / (xVertCount - 1), 1f / (yVertCount - 1)); //determines the distance or "step" amount between vertices
        step *= LODstep; //make smaller steps if higher lod


        Vector3[] vertices = new Vector3[xVertCount * yVertCount];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];


        float minNoiseHeight = float.MaxValue + .1f;
        float maxNoiseHeight = float.MinValue;

        Texture2D tex = new Texture2D(xVertCount, yVertCount);

        for (int y = 0; y < yVertCount; y++)
        {
            for (int x = 0; x < xVertCount; x++)
            {

                int i = x + y * xVertCount;
                Vector2 p = offset + new Vector2(x * step.x, y * step.y); //determines vertex location in grid

                uvs[i] = p + Vector2.one * 0.5f;
                Vector3 vec = ((planePatch.uAxis * p.x) + (planePatch.vAxis * p.y) + (planePatch.height * 0.5f)); //determine plane vertex based on direction. p determines
                                                                                                   //vertex location in grid
                vec = vec.normalized; //makes it a sphere

                float range = 1f;
                float noiseHeight = 0f;

                //GET NECESSARY VALUES FOR NOISE FROM PARENT PLANET
                int octaves = planePatch.planetObject.GetComponent<Sphere>().getOctaves();
                float lacunarity = planePatch.planetObject.GetComponent<Sphere>().getLacunarity();
                float persistance = planePatch.planetObject.GetComponent<Sphere>().getPersistance();
                int seed = planePatch.planetObject.GetComponent<Sphere>().getSeed();
                float scale = planePatch.planetObject.GetComponent<Sphere>().getScale();

                OctaveNoise(vec,ref range, ref noiseHeight, seed, scale, octaves, lacunarity, persistance);

                Mathf.Clamp(noiseHeight, minNoiseHeight, maxNoiseHeight);


                int planetType = planePatch.planetObject.GetComponent<Sphere>().getPlanetType();
                float addHeight = planePatch.planetObject.GetComponent<Sphere>().evaluateHeightCurve(planetType,(noiseHeight/range));
                    //heightCurve[planetType].Evaluate(noiseHeight / range);
                vec = vec * (1.0f + addHeight); //change vertex according to height map curve
                float currentHeight = noiseHeight / range;

                normals[i] = vec;
                vertices[i] = vec * radius;

                //SET TEXTURE PIXELS ACCORDINGLY
                createPatchTexture(ref tex, x, y, currentHeight);

            }
        }

        tex.Apply();
        tex.alphaIsTransparency = true;
        tex.filterMode = FilterMode.Point;
        
        transform.GetComponent<Renderer>().material.mainTexture = tex;
        transform.GetComponent<Renderer>().material.mainTextureScale = new Vector2(1 << patch.LODlevel, 1 << patch.LODlevel);
        //transform.GetComponent<SectionTexture>().tex = tex;


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
        m.vertices = vertices;
        m.normals = normals;
        m.uv = uvs;
        m.SetIndices(indices, MeshTopology.Quads, 0);
        m.RecalculateBounds();

        mf.sharedMesh.SetTriangles(mf.sharedMesh.GetTriangles(0), 0);

        this.gameObject.AddComponent<MeshCollider>();

    }

    void createPatchTexture(ref Texture2D tex, int x, int y, float currentHeight)
    {
        //the getcomponent lines look ugly, is there a way to clean it up?
        int regionLength = patch.planetObject.GetComponent<Sphere>().getRegionLength();

        for (int r = 0; r < regionLength - 1; r++)
        {
            float currentIndexHeight = patch.planetObject.GetComponent<Sphere>().getHeightArrayValue(r);
            float nextIndexHeight = patch.planetObject.GetComponent<Sphere>().getHeightArrayValue(r+1);


            if (currentHeight >= currentIndexHeight && currentHeight < nextIndexHeight)
            {
                Color color = patch.planetObject.GetComponent<Sphere>().getRegionColor(r);
                tex.SetPixel(x, y, color);
                break;
            }
            if (r == regionLength - 2)
            {
                Color color = patch.planetObject.GetComponent<Sphere>().getRegionColor(r-1);
                tex.SetPixel(x, y, color);
            }
        }
    }

    float OctaveNoise(Vector3 vec,ref float range, ref float noiseHeight, int seed, float scale, int octaves, float lacunarity, float persistance)
    {

        float frequency = 1;
        float amplitude = 1;

        for (int g = 0; g < octaves; g++)
        {
            float nx = transform.TransformPoint(vec).x;
            float ny = transform.TransformPoint(vec).y;
            float nz = transform.TransformPoint(vec).z;



            float xx = ((nx - xVertCount) / scale) * frequency;
            float yy = ((ny - yVertCount) / scale) * frequency;
            float zz = ((nz - xVertCount) / scale) * frequency;

            /*float ox = ((nx - xVertCount) / oreScale) * frequency;
            float oy = ((ny - yVertCount) / oreScale) * frequency;
            float oz = ((nz - xVertCount) / oreScale) * frequency;*/

            float perlinValue = Perlin3d(xx + seed, yy + seed, zz + seed);
            noiseHeight += perlinValue * amplitude;

            amplitude *= persistance;
            frequency *= lacunarity;
            range += amplitude / 4;
        }
        return noiseHeight;
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

    public Vector3 getPosition(PatchConfig planePatch, float LODstep) { //returns the middle vertex position. Is used to
                                                                        //measure distance for LOD

        float xVert = planePatch.vertices.x;
        float yVert = planePatch.vertices.y;

        Vector2 offset = new Vector2(-0.5f, -0.5f) + planePatch.LODOffset; //to center all side meshes. Multiply by LODoffset to give correct quadrant
        Vector2 step = new Vector2(1f / (xVert - 1), 1f / (yVert - 1)); //determines the distance or "step" amount between vertices
        step *= LODstep; //make smaller steps if higher lod

        Vector2 p = offset + new Vector2( (xVert/2) * step.x, (yVert/2) * step.y);

        Vector3 vec = ((planePatch.uAxis * p.x) + (planePatch.vAxis * p.y) + (planePatch.height * 0.5f));
        vec = vec.normalized;

        vec += this.transform.position;
        return vec;
    }
}
