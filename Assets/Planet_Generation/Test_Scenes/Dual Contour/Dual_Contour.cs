using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    [SerializeField] private Material mat;

    [SerializeField] private const float CELL_SIZE = 1;

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
        Generate();
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
        dualGrid = new cell[(sizeX)*(sizeY)*sizeZ];
        //vertices = new List<Vector3>();
        indices = new List<int>();

        if(!rend) rend = this.gameObject.AddComponent<MeshRenderer>();


        if(!mf) mf = this.gameObject.AddComponent<MeshFilter>();

        if(m) m.Clear();
        m = mf.sharedMesh = new Mesh();

        

        List<Vector3> verts = new List<Vector3>();

        for (int x = 0; x < sizeX; ++x) {
            for (int y = 0; y < sizeY; ++y)
            {
                for (int z = 0; z < sizeZ; ++z)
                {
                    dualGrid[x + sizeX * (y + sizeY * z)].vertIndex = -1;
                    Find_Best_Vertex(function, x, y, z);
                }
            }
        }

        for (int x = 1; x < sizeX-1; ++x)
        {
            for (int y = 1; y < sizeY-1; ++y)
            {
                for (int z = 1; z < sizeZ - 1; ++z)
                {
                    CreateQuads(x,y,z);
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

    private void Find_Best_Vertex(Func<float,float,float,float> f, float x, float y, float z) {

        //we find evaluate the function at all four points
        //this is an opportunity for optimization in the future
        float x0y0z0 = f(x,y,z);
        float x0y0z1 = f(x,y,z+CELL_SIZE);
        float x0y1z0 = f(x,y+CELL_SIZE,z);
        float x0y1z1 = f(x, y+CELL_SIZE, z + CELL_SIZE);
        float x1y0z0 = f(x+CELL_SIZE,y,z);
        float x1y0z1 = f(x+CELL_SIZE, y, z + CELL_SIZE);
        float x1y1z0 = f(x+CELL_SIZE, y+CELL_SIZE, z);
        float x1y1z1 = f(x+CELL_SIZE, y+CELL_SIZE, z + CELL_SIZE);

        //calculate the adapt of only the edges that cross, rather than the whole thing
        //calculate the positions of the edges itself
        int ycount = 0;
        int xcount = 0;
        int zcount = 0;

        float yAverageT = 0;
        float xAverageT = 0;
        float zAverageT = 0;

        Vector3 avg = Vector3.zero;
        Vector3 orig = Vector3.zero;
        int count = 0;

        //X EDGES
        if ((x0y0z0 > 0 != x1y0z0 > 0)) {
            avg += new Vector3(x, y, z) + adapt(x0y0z0, x1y0z0) * (new Vector3(x + CELL_SIZE, y, z) - new Vector3(x,y,z));
            count++;
        }
        if ((x0y0z1 > 0 != x1y0z1 > 0))
        {
            orig = new Vector3(x, y, z + CELL_SIZE);
            avg += orig + adapt(x0y0z1, x1y0z1) * (new Vector3(x + CELL_SIZE, y, z+CELL_SIZE) - orig);
            count++;
        }
        if ((x0y1z0 > 0 != x1y1z0 > 0))
        {
            orig = new Vector3(x, y+CELL_SIZE, z);
            avg += orig + adapt(x0y1z0, x1y1z0) * (new Vector3(x + CELL_SIZE, y+CELL_SIZE, z) - orig);
            count++;
        }
        if ((x0y1z1 > 0 != x1y1z1 > 0))
        {
            orig = new Vector3(x, y + CELL_SIZE, z+CELL_SIZE);
            avg += orig + adapt(x0y1z1, x1y1z1) * (new Vector3(x + CELL_SIZE, y + CELL_SIZE, z+CELL_SIZE) - orig);
            count++;
        }

        //Y EDGES
        if ((x0y1z0 > 0 != x0y0z0 > 0))
        {
            orig = new Vector3(x, y, z);
            avg += orig + adapt(x0y0z0, x0y1z0) * (new Vector3(x, y+CELL_SIZE, z) - orig);
            count++;
        }
        if ((x1y1z0 > 0 != x1y0z0 > 0))
        {
            orig = new Vector3(x+CELL_SIZE, y, z);
            avg += orig + adapt(x1y0z0, x1y1z0) * (new Vector3(x+CELL_SIZE, y + CELL_SIZE, z) - orig);
            count++;
        }
        if ((x0y1z1 > 0 != x0y0z1 > 0))
        {
            orig = new Vector3(x, y, z+CELL_SIZE);
            avg += orig + adapt(x0y0z1, x0y1z1) * (new Vector3(x, y + CELL_SIZE, z+CELL_SIZE) - orig);
            count++;
        }
        if ((x1y1z1 > 0 != x1y0z1 > 0))
        {
            orig = new Vector3(x+CELL_SIZE, y, z+CELL_SIZE);
            avg += orig + adapt(x1y0z1, x1y1z1) * (new Vector3(x+CELL_SIZE, y + CELL_SIZE, z+CELL_SIZE) - orig);
            count++;
        }

        //Z EDGES
        if ((x0y0z1 > 0 != x0y0z0 > 0))
        {
            orig = new Vector3(x, y, z);
            avg += orig + adapt(x0y0z0, x0y0z1) * (new Vector3(x, y, z+CELL_SIZE) - orig);
            count++;
        }
        if ((x1y0z1 > 0 != x1y0z0 > 0))
        {
            orig = new Vector3(x+CELL_SIZE, y, z);
            avg += orig + adapt(x1y0z0, x1y0z1) * (new Vector3(x+CELL_SIZE, y , z+CELL_SIZE) - orig);
            count++;
        }
        if ((x0y1z1 > 0 != x0y1z0 > 0))
        {
            orig = new Vector3(x, y+CELL_SIZE, z);
            avg += orig + adapt(x0y1z0, x0y1z1) * (new Vector3(x, y + CELL_SIZE, z+CELL_SIZE) - orig);
            count++;
        }
        if ((x1y1z1 > 0 != x1y1z0 > 0))
        {
            orig = new Vector3(x+CELL_SIZE, y+CELL_SIZE, z);
            avg += orig + adapt(x1y1z0, x1y1z1) * (new Vector3(x+CELL_SIZE, y + CELL_SIZE, z+CELL_SIZE) - orig);
            count++;
        }
        avg /= Mathf.Max(1,count);

        /*xAverageT += (x0y0z0 > 0 != x1y0z0 > 0) ? adapt(x0y0z0, x1y0z0) : 0;
         * 
        //xAverageT += (x0y0z1 > 0 != x1y0z1 > 0) ? adapt(x0y0z1, x1y0z1) : 0;
        //xAverageT += (x0y1z0 > 0 != x1y1z0 > 0) ? adapt(x0y1z0, x1y1z0) : 0;
        //xAverageT += (x0y1z1 > 0 != x1y1z1 > 0) ? adapt(x0y1z1, x1y1z1) : 0;
        xcount += (x0y0z0 > 0 != x1y0z0 > 0) ? 1 : 0; 
        xcount += (x0y0z1 > 0 != x1y0z1 > 0) ? 1 : 0; 
        xcount += (x0y1z0 > 0 != x1y1z0 > 0) ? 1 : 0;
        xcount += (x0y1z1 > 0 != x1y1z1 > 0) ? 1 : 0;

        //yAverageT += (x0y1z0 > 0 != x0y0z0 > 0) ? adapt(x0y0z0, x0y1z0) : 0;
        //yAverageT += (x1y1z0 > 0 != x1y0z0 > 0) ? adapt(x1y0z0, x1y1z0) : 0;
        //yAverageT += (x0y1z1 > 0 != x0y0z1 > 0) ? adapt(x0y0z1, x0y1z1) : 0;
        //yAverageT += (x1y1z1 > 0 != x1y0z1 > 0) ? adapt(x1y0z1, x1y1z1) : 0;
        ycount += (x0y1z0 > 0 != x0y0z0 > 0) ? 1 : 0;
        ycount += (x1y1z0 > 0 != x1y0z0 > 0) ? 1 : 0;
        ycount += (x0y1z1 > 0 != x0y0z1 > 0) ? 1 : 0;
        ycount += (x1y1z1 > 0 != x1y0z1 > 0) ? 1 : 0;

        //zAverageT += (x0y0z1 > 0 != x0y0z0 > 0) ? adapt(x0y0z0, x0y0z1) : 0;
        //zAverageT += (x1y0z1 > 0 != x1y0z0 > 0) ? adapt(x1y0z0, x1y0z1) : 0;
        //zAverageT += (x0y1z1 > 0 != x0y1z0 > 0) ? adapt(x0y1z0, x0y1z1) : 0;
        //zAverageT += (x1y1z1 > 0 != x1y1z0 > 0) ? adapt(x1y1z0, x1y1z1) : 0;
        zcount += (x0y0z1 > 0 != x0y0z0 > 0) ? 1 : 0;
        zcount += (x1y0z1 > 0 != x1y0z0 > 0) ? 1 : 0;
        zcount += (x0y1z1 > 0 != x0y1z0 > 0) ? 1 : 0;
        zcount += (x1y1z1 > 0 != x1y1z0 > 0) ? 1 : 0;

        */

        float xavg = xAverageT / Mathf.Max(xcount,1);
        float yavg = yAverageT / Mathf.Max(ycount,1);
        float zavg = zAverageT / Mathf.Max(zcount,1);

        //we then identify where changes in the function are (sign changes)
        bool signChange = false;
        List<Vector3> changes = new List<Vector3>();
        Edge[] _newedges = new Edge[3]; //0 is reserved for x, 1 for y, 2 for z
        Vector3[] _newnormals = new Vector3[3];



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

        //figure out what edge was crossed (axis)

        //Assign the sign of the edge according to how the flip occurs

        //The normals shouldn't have x,y,z as their parameters, but should instead reflect
        //the positions of the intermediate point on the edge.

        bool xCross = x1y1z0 > 0 != x1y1z1 > 0;
        bool yCross = x1y0z1 > 0 != x1y1z1 > 0;
        bool zCross = x0y1z1 > 0 != x1y1z1 > 0;
        float half_Cell = CELL_SIZE * 0.5f;

        if (xCross) _newedges[0] = new Edge(new Vector3(x + CELL_SIZE, y, z + adapt(x1y0z0, x1y0z1)), simplexNoise.Compute3DGradient(x+CELL_SIZE,y,z+ adapt(x1y0z0,x1y0z1)), true, !(x1y1z0 > 0)&& (x1y1z1 > 0)); //need to change intersection and normal later if we want interpolation
        if (yCross) _newedges[1] = new Edge(new Vector3(x + CELL_SIZE, y + adapt(x1y0z1, x1y1z1), z + CELL_SIZE), simplexNoise.Compute3DGradient(x + CELL_SIZE, y+adapt(x1y0z1,x1y1z1), z+CELL_SIZE), true, (x1y0z1 > 0) && !(x1y1z1 > 0)); 
        if (zCross) _newedges[2] = new Edge(new Vector3(x + adapt(x0y0z1, x1y0z1), y, z + CELL_SIZE), simplexNoise.Compute3DGradient(x+adapt(x0y0z1,x1y0z1), y, z+CELL_SIZE), true, !(x0y1z1 > 0) && (x1y1z1 > 0));



        //add a vertex
        //Vector3 vertex = new Vector3(x, y, z) + (Vector3.one * CELL_SIZE * 0.5f);
        //Vector3 vertex = Solve_QEF(x, y, z, ref _newedges);
        Vector3 surfacenet = new Vector3(
            xavg,
            yavg, 
            zavg);
        //Vector3 vertex = new Vector3(x,y,z);
        //Vector3 vertex = avg;//new Vector3(x, y, z) + (surfacenet*T);
        //Vector3 vertex = new Vector3(x, y, z) + new Vector3(adapt(x1y0z0, x1y0z1),  adapt(x1y0z1, x1y1z1),adapt(x0y0z1, x1y0z1));

        Vector3 vertex = new Vector3(x, y, z);


        vertices.Add(vertex); //vertex should be at the center of the cell
        //set cell struct to include vertex index
        



        int index = (int)(x + sizeX * (y + sizeY * z));
        dualGrid[index].edges = _newedges;
        dualGrid[index].vertIndex = vertices.Count-1;
    }

    //minimizes the error function to find the best vertex position
    //the triplet contains the information for both the intersections and the normals
    private Vector3 Solve_QEF(float x, float y, float z, ref Edge[] triplet) {
        return Vector3.one;
    }


    private void CreateQuads(int x,int y, int z) {
        int index = (int)(x + sizeX * (y + sizeY * z));
        if (dualGrid[index].vertIndex == -1) return;

        //This only checks one orientation of the quad. Need to flip flop indices based on how intersection occurs


        if (dualGrid[index].edges[0].crossed) {
            //Make a quad from neighboring x cells
            if (dualGrid[index].edges[0].sign)
            {


                indices.Add(dualGrid[index].vertIndex);
                indices.Add(dualGrid[(x+1) + sizeX * (y + sizeY * z)].vertIndex);
                indices.Add(dualGrid[(x+1) + sizeX * ((y+1) + sizeY * z)].vertIndex); //these indices might be screwed up
                indices.Add(dualGrid[x + sizeX * ((y+1) + sizeY * z)].vertIndex);
            }
            else {
                indices.Add(dualGrid[index].vertIndex);
                indices.Add(dualGrid[(x + sizeX * ((y+1) + sizeY * z))].vertIndex);
                indices.Add(dualGrid[((x+1) + sizeX * ((y + 1) + sizeY * z))].vertIndex);
                indices.Add(dualGrid[((x+1) + sizeX * (y + sizeY * z))].vertIndex);

            }
        }
        if (dualGrid[index].edges[1].crossed)
        {
            //Make a quad from neighboring y cells
            if (dualGrid[index].edges[1].sign)
            {
                indices.Add(dualGrid[index].vertIndex);
                indices.Add(dualGrid[(x+1) + sizeX * (y + sizeY * z)].vertIndex);
                indices.Add(dualGrid[(x+1) + sizeX * (y + sizeY * (z+1))].vertIndex); //these indices might be screwed up
                indices.Add(dualGrid[x + sizeX * (y + sizeY * (z+1))].vertIndex);
            }
            else {
                indices.Add(dualGrid[index].vertIndex);
                indices.Add(dualGrid[x + sizeX * (y + sizeY * (z+1))].vertIndex);
                indices.Add(dualGrid[(x+1) + sizeX * (y + sizeY * (z+1))].vertIndex);
                indices.Add(dualGrid[(x+1) + sizeX * (y + sizeY * z)].vertIndex);
                
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



    private float function(float x, float y,float z) {
        float domainWarp = simplexNoise.CalcPixel3D(x*5, y*5, z*5) * 2f;
        return (simplexNoise.CalcPixel3D(x+domainWarp, y+domainWarp, z + domainWarp)*5) + y-3;
    }


    //returns the interpolation t value for points x0 and x1 where sign(x0) != sign(x1)
    private float adapt(float x0, float x1) {
        //Assert.IsTrue(x0 > 0 != x1 > 0);
        
        return (-x0) / (x1-x0);
    }

    /*private void OnDrawGizmos()
    {
        if (vertices.Count < 1) return;
        foreach (var item in vertices)
        {
            Gizmos.DrawSphere(item, 0.1f);
        }
    }*/
}
