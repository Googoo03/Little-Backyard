using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Simplex;
using UnityEngine.Assertions;


public struct Edge {
    public Vector3 intersection;
    public Vector3 normal;
    public bool crossed;
    public bool sign;

    public Edge(Vector3 _intersection, Vector3 _normal, bool _crossed, bool _sign) {
        crossed = _crossed;
        intersection = _intersection;
        normal = _normal;
        sign = _sign;
    }
};

public struct cell {
    public int vertIndex;
    public Edge[] edges;

};


public class Dual_Contour : MonoBehaviour
{
    //MESH DIMENSIONS
    [SerializeField] private int sizeX;
    [SerializeField] private int sizeY;
    [SerializeField] private int sizeZ;
    [SerializeField] private float T;
    [SerializeField] private float lastT;
    [SerializeField] private float amplitude;
    [SerializeField] private Material mat;
    [SerializeField] private int LOD_Level;

    [SerializeField] private float CELL_SIZE = 8;

    private cell[] dualGrid;
    private Vector3[] cellVerts;


    //MESH DETAILS
    private Mesh m;
    private MeshFilter mf;
    private MeshRenderer rend;
    private List<Vector3> vertices = new List<Vector3>();
    private Vector3[] normals;
    private Vector2[] uvs;
    private List<int> indices;

    //NOISE FUNCTIONS TEMPORARY
    Noise simplexNoise = new Noise();


    private void Start()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        Generate();
        stopwatch.Stop();
        UnityEngine.Debug.Log("Took " + stopwatch.ElapsedMilliseconds.ToString() + " milliseconds");
    }

    private void Update()
    {
        if (lastT != T) {
            lastT = T;
            Generate();
        }
    }

    private void Generate() {

        //initialize both the dual grid and the vertex grid
        dualGrid = new cell[(sizeX) * (sizeY) * sizeZ];
        //vertices = new List<Vector3>();
        indices = new List<int>();

        if (!rend) rend = this.gameObject.AddComponent<MeshRenderer>();


        if (!mf) mf = this.gameObject.AddComponent<MeshFilter>();

        if (m) m.Clear();
        m = mf.sharedMesh = new Mesh();



        List<Vector3> verts = new List<Vector3>();

        for (int x = 0; x < sizeX; ++x) {
            for (int y = 0; y < sizeY; ++y)
            {
                for (int z = 0; z < sizeZ; ++z)
                {
                    dualGrid[x + sizeX * (y + sizeY * z)].vertIndex = -1;
                    Find_Best_Vertex(function,ShellElevate, x, y, z);
                }
            }
        }

        for (int x = 0; x < sizeX - 1; ++x)
        {
            for (int y = 0; y < sizeY - 1; ++y)
            {
                for (int z = 0; z < sizeZ - 1; ++z)
                {
                    CreateQuads(x, y, z);
                }
            }
        }

        m.vertices = vertices.ToArray();
        m.normals = normals;
        m.uv = uvs;
        m.SetIndices(indices, MeshTopology.Quads, 0);
        m.RecalculateBounds();
        m.RecalculateNormals();

        rend.material = mat;

    }

    private void Find_Best_Vertex(Func<Vector3,float, float> f, Func<Vector3, Vector3> spaceTransform, int x, int y, int z) {


        //determines the "step" between vertices to cover 1 unit total
        Vector3 step = new Vector3(1f/(sizeX-1),1f/(sizeY-1),1f/(sizeZ-1));
        step *= (1f / (1 << LOD_Level));
        step *= CELL_SIZE;

        //sets the offset of the grid by a power of two based on the LOD level
        Vector3 offset =  -Vector3.one* (1f / (1 << (LOD_Level+1)))*CELL_SIZE;
        //offset += transform.position;
        //Given a spacetransform function, transform the point from a grid to said space
        //Vector3 position = spaceTransform(new Vector3(x, y, z));

        //We have 8 vertices, and need to store them for later use
        Vector3 pos = offset + new Vector3(x*step.x,y*step.y,z*step.z);//new Vector3 ((x- (sizeX/2)) *CELL_SIZE, y*CELL_SIZE, (z-(sizeZ/2)) * CELL_SIZE);
        //if (spaceTransform != null) { pos = spaceTransform(pos); }

        //we find evaluate the function at all four points
        //this is an opportunity for optimization in the future
        float x0y0z0 = f(spaceTransform(pos),y);
        float x0y0z1 = f(spaceTransform(pos + new Vector3(0,0,step.z)),y);
        float x0y1z0 = f(spaceTransform(pos + new Vector3(0,step.y,0)),y+1);
        float x0y1z1 = f(spaceTransform(pos + new Vector3(0,step.y,step.z)),y+1);
        float x1y0z0 = f(spaceTransform(pos + new Vector3(step.x,0,0)),y);
        float x1y0z1 = f(spaceTransform(pos + new Vector3(step.x,0,step.z)), y);
        float x1y1z0 = f(spaceTransform(pos + new Vector3(step.x,step.y,0)), y + 1);
        float x1y1z1 = f(spaceTransform(pos + new Vector3(step.x,step.y,step.z)), y + 1);

        float[] vertValues = new float[8];
        Vector3[] vertPos = new Vector3[8];
        for (int i = 0; i < 8; ++i) {
            vertPos[i] = spaceTransform(pos + new Vector3(step.x * ((i >> 2) & 0x01),step.y *((i >> 1) & 0x01),step.z * (i & 0x01)));
            vertValues[i] = f(vertPos[i],y + ((i&0x02)==0x02 ? 1 : 0));
        }
        //calculate the adapt of only the edges that cross, rather than the whole thing
        //calculate the positions of the edges itself

        Vector3 avg = Vector3.zero;
        int count = 0;

        //replace Vector3 with dynamic sizing

        //we then identify where changes in the function are (sign changes)
        bool signChange = false;
        Edge[] _newedges = new Edge[3]; //0 is reserved for x, 1 for y, 2 for z


        //set sign change if any edge is crossed
        signChange |= (x0y0z0 > 0) != (x1y0z0 > 0);
        signChange |= (x0y0z0 > 0) != (x0y0z1 > 0);
        signChange |= (x1y0z1 > 0) != (x0y0z1 > 0);
        signChange |= (x1y0z1 > 0) != (x1y0z0 > 0);
        signChange |= (x1y0z0 > 0) != (x1y1z0 > 0);
        signChange |= (x0y0z1 > 0) != (x0y1z1 > 0);
        signChange |= (x0y0z0 > 0) != (x0y1z0 > 0);
        signChange |= (x1y0z1 > 0) != (x1y1z1 > 0);
        signChange |= (x1y1z0 > 0) != (x1y1z1 > 0);
        signChange |= (x0y1z1 > 0) != (x1y1z1 > 0);
        signChange |= (x0y1z0 > 0) != (x0y1z1 > 0);
        signChange |= (x0y1z0 > 0) != (x1y1z0 > 0);

        if (!signChange) return;

        //X EDGES NEW 
        for (int i = 0; i < 4; ++i)
        {
            float a = vertValues[i];
            float b = vertValues[i | 0x04];
            if (a > 0 != b > 0)
            {
                avg += vertPos[i] + adapt(a, b) * (vertPos[i | 0x04] - vertPos[i]);
                count++;
            }
        }

        //Y EDGES
        int[] yindices = new int[4] { 0, 1, 4, 5 };
        for (int i = 0; i < 4; ++i)
        {
            int j = yindices[i];
            float a = vertValues[j];
            float b = vertValues[j | 0x02];
            if (a > 0 != b > 0)
            {
                avg += vertPos[j] + adapt(a, b) * (vertPos[j | 0x02] - vertPos[j]);
                count++;
            }
        }

        //Z EDGES
        for (int i = 0; i < 4; ++i)
        {
            float a = vertValues[(i << 1)];
            float b = vertValues[(i << 1) | 0x01];
            if (a > 0 != b > 0)
            {
                avg += vertPos[i << 1] + adapt(a, b) * (vertPos[(i << 1) | 0x01] - vertPos[i << 1]);
                count++;
            }
        }

        avg /= Mathf.Max(1, count);

        //figure out what edge was crossed (axis)

        //Assign the sign of the edge according to how the flip occurs

        //The normals shouldn't have x,y,z as their parameters, but should instead reflect
        //the positions of the intermediate point on the edge.

        bool xCross = x1y1z0 > 0 != x1y1z1 > 0;
        bool yCross = x1y0z1 > 0 != x1y1z1 > 0;
        bool zCross = x0y1z1 > 0 != x1y1z1 > 0;

        if (xCross) _newedges[0] = new Edge(Vector3.zero, Vector3.zero, true, (vertValues[6] > 0) && !(vertValues[7] > 0)); //need to change intersection and normal later if we want interpolation
        if (yCross) _newedges[1] = new Edge(Vector3.zero, Vector3.zero, true, !(vertValues[5] > 0) && (vertValues[7] > 0));
        if (zCross) _newedges[2] = new Edge(Vector3.zero, Vector3.zero, true, (vertValues[3] > 0) && !(vertValues[7] > 0));

        Vector3 vertex = avg;

        vertices.Add(vertex); //vertex should be at the center of the cell
                              //set cell struct to include vertex index

        int index = (int)(x + sizeX * (y + sizeY * z));
        dualGrid[index].edges = _newedges;
        dualGrid[index].vertIndex = vertices.Count - 1;
    }

    //minimizes the error function to find the best vertex position
    //the triplet contains the information for both the intersections and the normals
    private Vector3 Solve_QEF(float x, float y, float z, ref Edge[] triplet) {
        return Vector3.one;
    }


    private void CreateQuads(int x, int y, int z) {
        int index = (int)(x + sizeX * (y + sizeY * z));
        if (dualGrid[index].vertIndex == -1) return;

        //This only checks one orientation of the quad. Need to flip flop indices based on how intersection occurs


        if (dualGrid[index].edges[0].crossed) {
            //Make a quad from neighboring x cells
            if (dualGrid[index].edges[0].sign)
            {


                indices.Add(dualGrid[index].vertIndex);
                indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * z)].vertIndex);
                indices.Add(dualGrid[(x + 1) + sizeX * ((y + 1) + sizeY * z)].vertIndex); //these indices might be screwed up
                indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * z)].vertIndex);
            }
            else {
                indices.Add(dualGrid[index].vertIndex);
                indices.Add(dualGrid[(x + sizeX * ((y + 1) + sizeY * z))].vertIndex);
                indices.Add(dualGrid[((x + 1) + sizeX * ((y + 1) + sizeY * z))].vertIndex);
                indices.Add(dualGrid[((x + 1) + sizeX * (y + sizeY * z))].vertIndex);

            }
        }
        if (dualGrid[index].edges[1].crossed)
        {
            //Make a quad from neighboring y cells
            if (dualGrid[index].edges[1].sign)
            {
                indices.Add(dualGrid[index].vertIndex);
                indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * z)].vertIndex);
                indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * (z + 1))].vertIndex); //these indices might be screwed up
                indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))].vertIndex);
            }
            else {
                indices.Add(dualGrid[index].vertIndex);
                indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))].vertIndex);
                indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * (z + 1))].vertIndex);
                indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * z)].vertIndex);

            }
        }
        if (dualGrid[index].edges[2].crossed)
        {
            //Make a quad from neighboring z cells
            if (dualGrid[index].edges[2].sign)
            {
                indices.Add(dualGrid[index].vertIndex);
                indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * z)].vertIndex);
                indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * (z + 1))].vertIndex);
                indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))].vertIndex);



            }
            else {


                indices.Add(dualGrid[index].vertIndex);
                indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))].vertIndex);
                indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * (z + 1))].vertIndex); //these indices might be screwed up
                indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * z)].vertIndex);
            }
        }
    }

    private Vector3 CartesianToSphere(Vector3 pos) {

        float radius = 10;

        float phi = Mathf.Acos(pos.z/radius);
        float theta = Mathf.Atan(pos.y/pos.x);
        

        
        return new Vector3(Mathf.Cos(theta)*Mathf.Sin(phi), Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(phi))*radius;
    }

    private Vector3 ShellElevate(Vector3 pos)
    {
        //simplexNoise.Seed = 1642;
        // Vector3 newpos = new Vector3 (pos.x,pos.y+ simplexNoise.CalcPixel3D(pos.x / 5, 0, pos.z / 5)*1, pos.z);
        //Assert.IsTrue(newpos.x != 0);
        Vector3 newpos = new Vector3(pos.x, pos.y, pos.z);
        return newpos;
    }


    private float function(Vector3 pos, float elevation) {
        float domainWarp = simplexNoise.CalcPixel3D(pos.x*5, 0, pos.z *5) * 2f;
        return 1 - Mathf.Abs((simplexNoise.CalcPixel3D(pos.x + domainWarp, 0, pos.z + domainWarp) * amplitude)) + (amplitude-(pos.y));
    }


    //returns the interpolation t value for points x0 and x1 where sign(x0) != sign(x1)
    private float adapt(float x0, float x1) {
        //Assert.IsTrue(x0 > 0 != x1 > 0);
        
        return (-x0) / (x1-x0);
    }
}
