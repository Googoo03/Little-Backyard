using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Simplex;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using UnityEditor.PackageManager.Requests;
using Unity.Collections;
using chunk_events;
using Unity.Jobs;
using static UnityEditor.Searcher.SearcherWindow.Alignment;
using Unity.Burst;
using UnityEditor.PackageManager;
using DualContour;
using UnityEngine.UI;
using System.Data;
using Unity.Mathematics;
using System.Reflection;
using static UnityEngine.Rendering.VolumeComponent;


namespace DualContour
{

    public struct Edge
    {
        public bool crossed;
        public bool sign;

        public Edge(bool _crossed, bool _sign)
        {
            crossed = _crossed;
            sign = _sign;
        }
    };

    public struct cell
    {
        public int vertIndex;
        public Edge[] edges;

    };

    public struct seamNode {
        public int dualgrid; //entry
        public (int x, int y , int z) coord;
        public Vector3 vertex;
    }

    public struct seam {
        public NativeArray<NativeArray<int>> nodes; // x , y , z, xy, yz, xz, xyz


    };

    struct Seamrule {
        public Action<int,int,int> iteration;
    }

    /*
    Seamrule[] seamRules = {
        new Seamrule{
            iteration = (int sizeX, int sizeY, int sizeZ) => {
            int z = sizeZ-1;
            for(int x = 0; x < sizeX; ++x){
                for(int y = 0; y < sizeY; ++y){

                }
            }
        } },
    };*/

    enum BLOCKID : ushort { 
        GRASS = 0,
        STONE = 1,
        COPPER_ORE = 2,
        IRON_ORE = 3,
        AIR = 15
    }

    [BurstCompile]
    public struct QuadParallel : IJob
    {

        public int sizeX, sizeY, sizeZ;
        public NativeArray<int> dualGrid;
        public NativeList<int> indices;

        struct Trirule {
            public int axis;
            public int sign;
            public (int dx, int dy, int dz)[] vertpos;
        }

        static readonly Trirule[] rules = { 
            new Trirule { //x axis
                axis = 0x20,
                sign = 0x10,
                vertpos = new (int, int, int )[]{
                    (0,0,0), (1,0,0), (1,1,0),
                    (1,1,0), (0,1,0), (0,0,0)
                }
            },
            new Trirule { //y axis
                axis = 0x08,
                sign = 0x04,
                vertpos = new (int, int, int )[]{
                    (0,0,0), (1,0,0), (1,0,1),
                    (1,0,1), (0,0,1), (0,0,0)
                }
            },
            new Trirule { //z axis
                axis = 0x02,
                sign = 0x01,
                vertpos = new (int, int, int )[]{
                    (0,0,0), (0,1,0), (0,1,1),
                    (0,1,1), (0,0,1), (0,0,0)
                }
            },
        };

        public void Execute()
        {

            for (int x = 0; x < sizeX - 2; ++x)
            {

                for (int y = 0; y < sizeY - 2; ++y)
                {

                    for (int z = 0; z < sizeZ - 2; ++z)
                    {
                        int index = (int)(x + sizeX * (y + sizeY * z));
                        int dualGrid_index = dualGrid[index];

                        if (dualGrid_index == -1) continue;

                        foreach (Trirule rule in rules) {
                            if ((dualGrid_index & rule.axis) != rule.axis) continue;

                            var verts = (dualGrid_index & rule.sign) == rule.sign ? rule.vertpos : new (int, int, int)[]
                                {
                                    rule.vertpos[0], rule.vertpos[4], rule.vertpos[2],
                                    rule.vertpos[3], rule.vertpos[1], rule.vertpos[5]
                                };


                            for (int j = 0; j < 2; ++j)
                            {
                                var (dx, dy, dz) = verts[j];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0) continue;

                                (dx, dy, dz) = verts[j+1];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0) continue;

                                (dx, dy, dz) = verts[j+2];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0) continue;


                                for (int i = 0; i < 3; ++i)
                                {
                                    (dx, dy, dz) = verts[j*3 + i];
                                    indices.Add(dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] >> 6 & 0x7FFF);
                                }
                            }
                            
                        }
                    }
                }


            }
        }
    }

    [BurstCompile]
    public struct SeamVertexParallel : IJob {
        public NativeList<Vector3> vertsParallel;
        public int sizeX, sizeY, sizeZ;
        public int resolution;

        public NativeArray<int> dualGrid;

        public NativeArray<float> vertValues;
        public NativeArray<Vector3> vertPos;
        public NativeArray<Vector4> cellPacked;

        public bool block_voxel;

        float adapt(float x0, float x1) => (-x0) / (x1 - x0);

        public void DetermineVertex(int x, int y, int z) {
            int index = x + sizeX * (y + sizeY * z);
            dualGrid[index] = -1;
            int xPos, yPos, zPos;

            for (int i = 0; i < 8; ++i)
            {
                xPos = (x + ((i >> 2) & 0x01));
                yPos = (y + ((i >> 1) & 0x01));
                zPos = (z + (i & 0x01));


                vertPos[i] = cellPacked[(xPos + sizeX * (yPos + sizeY * zPos))];
                vertValues[i] = cellPacked[(xPos + sizeX * (yPos + sizeY * zPos))].w;
            }
            //calculate the adapt of only the edges that cross, rather than the whole thing
            //calculate the positions of the edges itself

            Vector3 avg = Vector3.zero;
            int count = 0;

            //replace Vector3 with dynamic sizing

            //we then identify where changes in the function are (sign changes)
            bool signChange = false;
            bool xCross;
            bool yCross;
            bool zCross;

            //set sign change if any edge is crossed
            signChange |= (vertValues[0] > 0) != (vertValues[4] > 0);
            signChange |= (vertValues[0] > 0) != (vertValues[1] > 0);
            signChange |= (vertValues[5] > 0) != (vertValues[1] > 0);
            signChange |= (vertValues[5] > 0) != (vertValues[4] > 0);
            signChange |= (vertValues[4] > 0) != (vertValues[6] > 0);
            signChange |= (vertValues[1] > 0) != (vertValues[3] > 0);
            signChange |= (vertValues[0] > 0) != (vertValues[2] > 0);
            signChange |= yCross = (vertValues[5] > 0) != (vertValues[7] > 0);
            signChange |= xCross = (vertValues[6] > 0) != (vertValues[7] > 0);
            signChange |= zCross = (vertValues[3] > 0) != (vertValues[7] > 0);
            signChange |= (vertValues[2] > 0) != (vertValues[3] > 0);
            signChange |= (vertValues[2] > 0) != (vertValues[6] > 0);

            if (signChange)
            {

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
                int j = 0;
                for (int i = 0; i < 4; ++i)
                {

                    if (i < 2)
                    {
                        j = i;
                    }
                    else
                    {
                        j = i == 2 ? 4 : 5;
                    }
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

                avg /= count > 1 ? count : 1;

                //figure out what edge was crossed (axis)

                //Assign the sign of the edge according to how the flip occurs

                //The normals shouldn't have x,y,z as their parameters, but should instead reflect
                //the positions of the intermediate point on the edge.

                int newedge = 0;

                if (xCross)
                {
                    newedge |= 1 << 5;
                    newedge |= (((vertValues[6] > 0) && !(vertValues[7] > 0)) ? 1 : 0) << 4;
                }
                if (yCross)
                {
                    newedge |= 1 << 3;
                    newedge |= ((!(vertValues[5] > 0) && (vertValues[7] > 0)) ? 1 : 0) << 2;
                }
                if (zCross)
                {
                    newedge |= 1 << 1;
                    newedge |= ((vertValues[3] > 0) && !(vertValues[7] > 0)) ? 1 : 0;
                }

                Vector3 vertex = block_voxel ? vertPos[0] : avg;

                vertsParallel.Add(vertex);

                //This should reference a biome texture and current elevation
                UInt16 voxelData = 0;
                //UnityEngine.Debug.Log("New Vert at " + vertPos[0]);
                dualGrid[index] = (newedge | ((vertsParallel.Length - 1) << 6) | (voxelData << 21));
            }
        }
        public void Execute()
        {
            int x, y, z;

            /*
            for (x = 0; x < sizeX  - 1; ++x)
            {
                for (y = 0; y < sizeY - 1; ++y)
                {
                    for (z = 0; z < sizeZ - 1; ++z)
                    {
                        DetermineVertex(x, y, z);
                        //UnityEngine.Debug.Log("x vert running");
                    }
                }
            }*/

            //z = sizeZ - resolution-2;

            
            for (x = 0; x < sizeX - 1; ++x)
            {
                for (y = 0; y < sizeY - 1; ++y)
                {
                    for (z= sizeZ - resolution - 1; z < sizeZ - 1; ++z)
                    {
                        DetermineVertex(x, y, z);
                        
                    }
                }
            }



            /////////////BROKEN THIS  LOOP
            ///
            x = sizeX - resolution - 1;
            for (x = sizeX - resolution - 2; x < sizeX - 1; ++x)
                    {
                for (y = 0; y < sizeY - 1; ++y)
                {

                    for (z = 0; z < sizeZ - 1; ++z)
                    {

                        UnityEngine.Debug.Log("x FACE running");
                        DetermineVertex(x, y, z);
                    }
                }
            }
            //////////////////////////////////
            ///

            for (x = 0; x < sizeX - 1; ++x)
            {
                for (z = 0; z < sizeZ - 1; ++z)
                {
                    for (y = sizeY - resolution - 1; y < sizeY - 1; ++y)
                    {
                        DetermineVertex(x, y, z);
                    }
                }
            }

            
            x = sizeX - resolution - 1;
            y = sizeY - resolution - 1;
            for (; x < sizeX - 1; ++x)
            {
                for (; y < sizeY - 1; ++y)
                {
                    for (z=0; z < sizeZ - resolution - 1; ++z)
                    {
                        DetermineVertex(x, y, z);
                    }
                }
            }
            
            z = sizeZ - resolution - 1;
            y = sizeY - resolution - 1;
            for (x = 0; x < sizeX - resolution - 1; ++x)
            {
                for (; y < sizeY - 1; ++y)
                {
                    for (; z < sizeZ - 1; ++z)
                    {
                        DetermineVertex(x, y, z);
                        UnityEngine.Debug.Log("zy vert running");
                    }
                }
            }
            
            x = sizeX - resolution - 1;
            z = sizeZ - resolution - 1;
            for (; x < sizeX - 1; ++x)
            {
                for (y = 0; y < sizeY - resolution - 1; ++y)
                {
                    for (; z < sizeZ - 1; ++z)
                    {
                        DetermineVertex(x, y, z);
                    }
                }
            }
            
            x = sizeX - resolution - 1;
            y = sizeY - resolution - 1;
            z = sizeZ - resolution - 1;
            for (; x < sizeX - 1; ++x)
            {
                for (; y < sizeY - 1; ++y)
                {
                    for (; z < sizeZ - 1; ++z)
                    {
                        DetermineVertex(x, y, z);
                        UnityEngine.Debug.Log("xyz vert running");
                    }
                }
            }

        }
    }



    [BurstCompile]
    public struct SeamQuadParallel : IJob
    {

        public int sizeX, sizeY, sizeZ;
        public NativeArray<int> dualGrid;
        public NativeList<int> indices;
        public NativeList<Vector3> vertices;

        struct Trirule
        {
            public int axis;
            public int sign;
            public (int dx, int dy, int dz)[] vertpos;
        }

        static readonly Trirule[] rules = {
            new Trirule { //x axis
                axis = 0x20,
                sign = 0x10,
                vertpos = new (int, int, int )[]{
                    (0,0,0), (1,0,0), (1,1,0),
                    (1,1,0), (0,1,0), (0,0,0)
                }
            },
            new Trirule { //y axis
                axis = 0x08,
                sign = 0x04,
                vertpos = new (int, int, int )[]{
                    (0,0,0), (1,0,0), (1,0,1),
                    (1,0,1), (0,0,1), (0,0,0)
                }
            },
            new Trirule { //z axis
                axis = 0x02,
                sign = 0x01,
                vertpos = new (int, int, int )[]{
                    (0,0,0), (0,1,0), (0,1,1),
                    (0,1,1), (0,0,1), (0,0,0)
                }
            },
        };

        public void Execute()
        {

            for (int x = 0; x < sizeX-1; ++x)
            {

                for (int y = 0; y < sizeY-1; ++y)
                {

                    for (int z = 0; z < sizeZ-1; ++z)
                    {
                        int index = (int)(x + sizeX * (y + sizeY * z));
                        int dualGrid_index = dualGrid[index];

                        if (dualGrid_index == -1) continue;

                        foreach (Trirule rule in rules)
                        {
                            if ((dualGrid_index & rule.axis) != rule.axis) continue;

                            var verts = (dualGrid_index & rule.sign) == rule.sign ? rule.vertpos : new (int, int, int)[]
                                {
                                    rule.vertpos[0], rule.vertpos[4], rule.vertpos[2],
                                    rule.vertpos[3], rule.vertpos[1], rule.vertpos[5]
                                };



                            for (int j = 0; j < 2; ++j)
                            {
                                
                                var (dx, dy, dz) = verts[j];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0 || dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == -1) continue;

                                (dx, dy, dz) = verts[j + 1];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0 || dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == -1) continue;

                                (dx, dy, dz) = verts[j + 2];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0 || dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == -1) continue;
                                

                                for (int i = 0; i < 3; ++i)
                                {
                                    (dx, dy, dz) = verts[j * 3 + i];
                                    if ((dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] >> 6 & 0x7FFF) >= vertices.Length) UnityEngine.Debug.Log(dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] >> 6 & 0x7FFF);
                                    indices.Add(dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] >> 6 & 0x7FFF);
                                }
                            }

                        }
                    }
                }


            }
        }
    }


    [BurstCompile]
    public struct VertexParallel : IJob {

        public NativeList<Vector3> vertsParallel;
        public int sizeX, sizeY, sizeZ;
        public int vert_index;

        public NativeArray<int> dualGrid;

        public NativeArray<float> vertValues;
        public NativeArray<Vector3> vertPos;
        public NativeArray<Vector4> cellPacked;

        public bool block_voxel;

        float adapt(float x0, float x1) => (-x0) / (x1 - x0);

        public void Execute() {
            int index = 0;

            for (int x = 0; x < sizeX-1; ++x)
            {
                for (int y = 0; y < sizeY-1; ++y)
                {
                    for (int z = 0; z < sizeZ-1; ++z)
                    {
                        index = x + sizeX * (y + sizeY * z);
                        if (dualGrid[index] == 0) dualGrid[index] = -1;
                        int xPos, yPos, zPos;

                        for (int i = 0; i < 8; ++i)
                        {
                            xPos = x == sizeX - 1 ? (x - ((i >> 2) & 0x01)) : (x + ((i >> 2) & 0x01));
                            yPos = y == sizeY - 1 ? (y - ((i >> 1) & 0x01)) : (y + ((i >> 1) & 0x01));
                            zPos = z == sizeZ - 1 ? (z - (i & 0x01)) : (z + (i & 0x01));


                            vertPos[i] = cellPacked[(xPos + sizeX * (yPos + sizeY * zPos))];
                            vertValues[i] = cellPacked[(xPos + sizeX * (yPos + sizeY * zPos))].w;
                        }
                        //calculate the adapt of only the edges that cross, rather than the whole thing
                        //calculate the positions of the edges itself

                        Vector3 avg = Vector3.zero;
                        int count = 0;

                        //replace Vector3 with dynamic sizing

                        //we then identify where changes in the function are (sign changes)
                        bool signChange = false;
                        bool xCross;
                        bool yCross;
                        bool zCross;

                        //set sign change if any edge is crossed
                        signChange |= (vertValues[0] > 0) != (vertValues[4] > 0);
                        signChange |= (vertValues[0] > 0) != (vertValues[1] > 0);
                        signChange |= (vertValues[5] > 0) != (vertValues[1] > 0);
                        signChange |= (vertValues[5] > 0) != (vertValues[4] > 0);
                        signChange |= (vertValues[4] > 0) != (vertValues[6] > 0);
                        signChange |= (vertValues[1] > 0) != (vertValues[3] > 0);
                        signChange |= (vertValues[0] > 0) != (vertValues[2] > 0);
                        signChange |= yCross = (vertValues[5] > 0) != (vertValues[7] > 0);
                        signChange |= xCross = (vertValues[6] > 0) != (vertValues[7] > 0);
                        signChange |= zCross = (vertValues[3] > 0) != (vertValues[7] > 0);
                        signChange |= (vertValues[2] > 0) != (vertValues[3] > 0);
                        signChange |= (vertValues[2] > 0) != (vertValues[6] > 0);

                        if (!signChange) continue;

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
                        int j = 0;
                        for (int i = 0; i < 4; ++i)
                        {
                            
                            if (i < 2)
                            {
                                j = i;
                            }
                            else {
                                j = i == 2 ? 4 : 5;
                            }
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

                        avg /= count > 1 ? count : 1;

                        //figure out what edge was crossed (axis)

                        //Assign the sign of the edge according to how the flip occurs

                        //The normals shouldn't have x,y,z as their parameters, but should instead reflect
                        //the positions of the intermediate point on the edge.

                        int newedge = 0;

                        if (xCross)
                        {
                            newedge |= 1 << 5;
                            newedge |= (((vertValues[6] > 0) && !(vertValues[7] > 0)) ? 1 : 0) << 4;
                        }
                        if (yCross)
                        {
                            newedge |= 1 << 3;
                            newedge |= ((!(vertValues[5] > 0) && (vertValues[7] > 0)) ? 1 : 0) << 2;
                        }
                        if (zCross)
                        {
                            newedge |= 1 << 1;
                            newedge |= ((vertValues[3] > 0) && !(vertValues[7] > 0)) ? 1 : 0;
                        }

                        Vector3 vertex = block_voxel ? vertPos[0] : avg;

                        vertsParallel.Add(vertex);
                        vert_index++;

                        //This should reference a biome texture and current elevation
                        UInt16 voxelData = 0;

                        dualGrid[index] = (newedge | ((vertsParallel.Length - 1) << 6) | (voxelData << 21));
                    }
                }
            }
        }
    }


    public class Dual_Contour
    {

        
        //MESH DIMENSIONS
        [SerializeField] private int sizeX;
        [SerializeField] private int sizeY;
        [SerializeField] private int sizeZ;
        [SerializeField] private float T;
        [SerializeField] private float lastT;
        [SerializeField] private float amplitude;
        [SerializeField] private int LOD_Level;
        [SerializeField] private int dir; //on spherical meshes, this defines the uv direction (on a uv sphere) and sign
        [SerializeField] private int resolution; //for seams, determines how many times it needs to subdivide to be on the same level as the highest fidelity neighbor

        //MESH MODE
        [SerializeField] private bool block_voxel;

        [SerializeField] private float CELL_SIZE; //determines how far in units all the cells in a particular axis should cover.

        [SerializeField] private float Ground;

        //Testing

        [SerializeField] private GameObject cube;

        //GLOBAL VARS
        Vector3 step;
        Vector3 offset;

        //VOXEL DATA
        UInt16[] voxel_data;

        private NativeList<int> indices;
        private NativeList<Vector3> vertices;

        private NativeArray<int> dualGrid;

        private NativeArray<Vector4> cellPacked;

        private NativeArray<Vector3> cellpos;
        private NativeArray<float> cellvalues;

        private NativeList<seamNode> seams;

        //NOISE FUNCTIONS TEMPORARY
        Noise simplexNoise = new Noise();

        //
        private Vector3 global;
        private Vector3 min;
        private Vector3 max;

        //EDIT VARS
        private float radius;

        //helper variables to speed up vertex placement
        Func<Vector3, int, Vector3> coordTransformFunction;

        public VertexParallel vertJob;
        public QuadParallel quadJob;
        public SeamQuadParallel seamQuadJob;
        public SeamVertexParallel seamVertexJob;
        

        public Dual_Contour(Vector3 _global, Vector3Int scale, Vector3 ioffset, int ilodLevel, int length, bool mode, float iradius, int idir) {
            global = _global;
            sizeX = scale.x;
            sizeY = scale.y;
            sizeZ = scale.z;
            CELL_SIZE = length;
            LOD_Level = ilodLevel;
            Ground = 1f;
            amplitude = 5;
            block_voxel = mode;
            radius = iradius;
            dir = idir;
            coordTransformFunction = Grid;
            resolution = 1;

            
            offset = -Vector3.one * CELL_SIZE * 0.5f;
            offset.x += ioffset.x * CELL_SIZE;
            offset.y += ioffset.y * CELL_SIZE;
            offset.z += ioffset.z * CELL_SIZE;
            
        }

        private float function(Vector3 pos, float y)
        {


            float domainWarp =  simplexNoise.CalcPixel3D(pos.x * 5, pos.y * 5, pos.z * 5) * 2f;

            float elevation = (-CELL_SIZE / 2) + (-offset).y + (y * step.y);

            if (elevation >= sizeY - 1) { return -1; }
            if (elevation <= 1) { return 1; }

            //float value = (1 - Mathf.Abs(simplexNoise.CalcPixel3D(pos.x + domainWarp, pos.y + domainWarp, pos.z + domainWarp))) * amplitude;// + Ground - elevation;
            //float value = Ground - elevation;
            float value = simplexNoise.CalcPixel3D(pos.x + domainWarp, pos.y + domainWarp, pos.z + domainWarp) * amplitude;


            return value;
        }

        ///INITIALIZE GRID INFORMATION UPON STARTUP/----------------------------------------------------------------------------

        private ushort DetermineVoxelData(Vector3 pos, int x,int elevation,int z)
        {
            float oreVal = 1;// simplexNoise.CalcPixel3D(pos.x * 3, pos.y * 3, pos.z * 3) * 2f;
            //Ground/Stone pass
            BLOCKID val;
            //These should include IDs for the given blocks by enumeration, rather than the plain ids themselves
            float cellvalue = cellPacked[x + sizeX * (elevation + sizeY * z)].w;//cellvalues[x + sizeX * (elevation + sizeY * z)];

            val = cellvalue > 0 ? BLOCKID.STONE : BLOCKID.AIR;
            if (val == BLOCKID.AIR) return (ushort)val;

            val = elevation < Ground ? BLOCKID.STONE : BLOCKID.GRASS;

            //Ore pass
            val = oreVal > 0.8f && val == BLOCKID.STONE ? BLOCKID.COPPER_ORE : val;
            val = oreVal < 0.3f && val == BLOCKID.STONE ? BLOCKID.IRON_ORE : val;

            //Other passes once the time comes
            return (ushort)val;
        }

        public void InitializeBounds() {
            min = global + offset;

            //be careful that cell_size is before it increments, in fact, it may be better if the seam just inherits from the parent chunk
            max = global + offset + (Vector3.one * CELL_SIZE * (1f / (1 << LOD_Level)));
        }

        [BurstCompile]
        public void InitializeGrid(bool seam,ref NativeList<Vector3> verts, ref NativeList<int> ind) {

            indices = ind;
            vertices = verts;

            //sets the offset of the grid by a power of two based on the LOD level
            //YES KEEP IT BEFORE IT INCREMENTS THE CELL_SIZE
           

            //UnityEngine.Debug.Log((seam ? ("seam") : ("chunk")) + " Offset at" + offset);

            if (seam) {
                sizeX++;
                sizeY++;
                sizeZ++;

                CELL_SIZE++; //want to cover n+1 units, where n is the initial length
            }

            sizeX *= resolution;
            sizeY *= resolution;
            sizeZ *= resolution;

            //determines the "step" between vertices to cover 1 unit total
            step = new Vector3(1f / (sizeX), 1f / (sizeY), 1f / (sizeZ));
            step *= (1f / (1 << LOD_Level));
            step *= CELL_SIZE;

            
            
            

            //BIGGEST BOTTLENECK ABOVE ALL
            cellPacked = new NativeArray<Vector4>(sizeX * sizeY * sizeZ, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            dualGrid = new NativeArray<int>(sizeX * sizeY * sizeZ, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            voxel_data = new UInt16[sizeX * sizeY * sizeZ];
            ///////////////////////////////

            int index = 0;
            Vector3 pos;
            Vector3 cellp;
            Vector4 packed_cell;

            
            seams = new NativeList<seamNode>(1,Allocator.Persistent);

            for (int z = 0; z < sizeZ; ++z)
            {
                for (int y = 0; y < sizeY; ++y)
                {
                    for (int x = 0; x < sizeX; ++x)
                    {
                        pos.x = offset.x + (x * step.x);
                        pos.y = offset.y + (y * step.y);
                        pos.z = offset.z + (z * step.z);

                        cellp = coordTransformFunction(pos, y);

                        
                        packed_cell = cellp;
                        packed_cell.w = function(global + cellp, y);

                        index = x + sizeX * (y + sizeY * z);
                        cellPacked[index] = packed_cell;
                        voxel_data[index] = (ushort)DetermineVoxelData(global + cellp, x, y, z);
                        //index++;
                    }
                }
            }
            
            vertJob = new VertexParallel
            {
                cellPacked = cellPacked,
                dualGrid = dualGrid,
                vertValues = new NativeArray<float>(8, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                vertPos = new NativeArray<Vector3>(8, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                vertsParallel = vertices,


                sizeX = sizeX,
                sizeY = sizeY,
                sizeZ = sizeZ,
                vert_index = 0,
                block_voxel = block_voxel
            };

            seamVertexJob = new SeamVertexParallel
            {
                cellPacked = cellPacked,
                dualGrid = dualGrid,
                vertValues = new NativeArray<float>(8, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                vertPos = new NativeArray<Vector3>(8, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                vertsParallel = vertices,
                resolution = resolution,

                sizeX = sizeX,
                sizeY = sizeY,
                sizeZ = sizeZ,
                block_voxel = block_voxel
            };

            quadJob = new QuadParallel
            {
                sizeX = sizeX,
                sizeY = sizeY,
                sizeZ = sizeZ,
                indices = indices,
                dualGrid = dualGrid

            };

            seamQuadJob = new SeamQuadParallel
            {
                sizeX = sizeX,
                sizeY = sizeY,
                sizeZ = sizeZ,
                indices = indices,
                dualGrid = dualGrid,
                vertices = vertices
            };

        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        public Vector3 GetMin() { return min; }
        public Vector3 GetMax() { return max; }

        public void SetResolution(int iresolution) { resolution = iresolution; }


        public void Generate(ref NativeList<Vector3> verts, ref NativeList<int> ind, ref UInt16[] v_data, ref int vert_index, ref int ind_index)
        {
            //initialize both the dual grid and the vertex grid

            indices = ind;
            vertices = verts;



            //This computes all the vertices except the edge cases (quite literally)
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            //int vert_index = 0;

            

            JobHandle vertScheduler = vertJob.Schedule();
            vertScheduler.Complete();
            

            //need to wait for all relevant chunks to finish?

            //then do seams

            //then do quads

            stopwatch.Stop();
            UnityEngine.Debug.Log("Vertices took " + stopwatch.ElapsedMilliseconds.ToString() + " milliseconds");

            stopwatch.Reset();
            stopwatch.Start();

            JobHandle quadScheduler = quadJob.Schedule();
            quadScheduler.Complete();

            stopwatch.Stop ();
            UnityEngine.Debug.Log("Indices took " + stopwatch.ElapsedMilliseconds.ToString() + " milliseconds");

            v_data = voxel_data;
        }

        public void UpdateVoxelData(ref List<chunk_event> points) {
            //Given a list of update points, that are assumed to be in range.

            Vector3 localPos;
            Vector3 currentCellPos;

            //Find the index of said points.
            points.ForEach(item => {

                localPos = (item.position - global - offset); //undo transformation to get grid position
                

                if (item.position.y == 0) return;

                for (int x = 0; x < sizeX; ++x)
                {
                    for (int y = 0; y < sizeY; ++y)
                    {
                        for (int z = 0; z < sizeZ; ++z)
                        {
                            currentCellPos = cellpos[(x + sizeX * (y + sizeY * z))] - offset;
                            if ((localPos - currentCellPos).magnitude < radius)
                            {
                                UpdateCellValueEntry(x, y, z);
                            }
                        }
                    }
                }
            });
        }

        private void UpdateCellValueEntry(int x, int y, int z) {
            if (x < 0 || x > sizeX || y < 1 || y > sizeY || z < 0 || z > sizeZ) return;

            cellvalues[(int)(x + sizeX * (y + sizeY * z))] = -1;
        }
        //////////////////////////////////////////////////////////////////////////////

        
        private Vector3 Grid(Vector3 pos, int elevation) { return pos; }


        public Vector3 FindTransformedCoord(Vector3 pos, int elevation) {
            return coordTransformFunction(pos, elevation);
        }


        private Vector3 CartesianToSphere(Vector3 pos, int elevation)
        {

            //Given a grid position, convert the point into a shell point
            //dir contains the u and v direction, which are masks for the x and z positions.
            Vector3 step = new Vector3(1f / (sizeX-1), 1f / (sizeY-1), 1f / (sizeZ - 1));
            step *= (1f / (1 << LOD_Level));
            step *= CELL_SIZE;


            int uSign = ((dir & 0x80) != 0) ? 1 : -1;
            int vSign = ((dir & 0x08) != 0) ? 1 : -1;
            Vector3 uaxis = new Vector3(((dir >> 6 & 0x7FFF) & 0x01), (dir >> 5) & 0x01, (dir >> 4) & 0x01) * (uSign);
            Vector3 vaxis = new Vector3(((dir >> 2) & 0x01), (dir >> 1) & 0x01, (dir) & 0x01) * (vSign);
            Vector3 wAxis = Vector3.Cross(vaxis, uaxis);
            float radius = CELL_SIZE / 2;

            //no idea why the step.x / 2. Why would it need to be pushed back half a unit? BECAUSE THE QUADS ARE CENTERED BY DEFAULT
            Vector3 newpos = uaxis * pos.x + vaxis * pos.z + wAxis * (radius - step.y / 2);

            return newpos.normalized * (radius + (elevation * step.y));
        }

        private Vector3 ShellElevate(Vector3 pos)
        {
            Vector3 newpos = new Vector3(pos.x, pos.y + simplexNoise.CalcPixel3D((global.x + pos.x) / 2, 0, (global.z + pos.z) / 2) * 20, pos.z);
            return newpos;
        }

        public VertexParallel GetVertexParallel() { return vertJob; }

        public QuadParallel GetQuadParallel() { return quadJob; }

        public SeamQuadParallel GetSeamQuadParallel() { return seamQuadJob; }

        public SeamVertexParallel GetSeamVertexParallel() { return seamVertexJob; }

        public ref NativeList<seamNode> GetSeamStruct() { return ref seams; }

        public ref NativeList<Vector3> GetVerticesStruct() { return ref vertices; }

        public int GetLODLevel() { return LOD_Level; }



        public static int GetNeighborRule(float3 currentMin, float3 currentMax, float3 neighborMin, float3 neighborMax, float epsilon = 0.0001f)
        {
            int rule = 0;

            bool xNeighbor = Mathf.Abs(neighborMin.x - currentMax.x) < epsilon &&
                                neighborMax.z <= currentMax.z + epsilon &&
                                neighborMax.z >= currentMin.z - epsilon &&
                                neighborMax.y <= currentMax.y + epsilon &&
                                neighborMax.y >= currentMin.y - epsilon &&
                                neighborMin.z <= currentMax.z + epsilon &&
                                neighborMin.z >= currentMin.z - epsilon &&
                                neighborMin.y <= currentMax.y + epsilon &&
                                neighborMin.y >= currentMin.y - epsilon;

            bool yNeighbor = Mathf.Abs(neighborMin.y - currentMax.y) < epsilon &&
                                neighborMax.z <= currentMax.z + epsilon &&
                                neighborMax.z >= currentMin.z - epsilon &&
                                neighborMax.x <= currentMax.x + epsilon &&
                                neighborMax.x >= currentMin.x - epsilon &&
                                neighborMin.z <= currentMax.z + epsilon &&
                                neighborMin.z >= currentMin.z - epsilon &&
                                neighborMin.x <= currentMax.x + epsilon &&
                                neighborMin.x >= currentMin.x - epsilon;

            bool zNeighbor = Mathf.Abs(neighborMin.z - currentMax.z) < epsilon &&
                                neighborMax.x <= currentMax.x + epsilon &&
                                neighborMax.x >= currentMin.x - epsilon &&
                                neighborMax.y <= currentMax.y + epsilon &&
                                neighborMax.y >= currentMin.y - epsilon &&
                                neighborMin.x <= currentMax.x + epsilon &&
                                neighborMin.x >= currentMin.x - epsilon &&
                                neighborMin.y <= currentMax.y + epsilon &&
                                neighborMin.y >= currentMin.y - epsilon;

            if (xNeighbor) rule |= 4;
            if (yNeighbor) rule |= 2;
            if (zNeighbor) rule |= 1;

            return rule;
        }




        public void ConstructSeamFromParentChunk(Dual_Contour current)
        {

            //current is the seam

            int x, y, z;
            int cx, cy, cz;

            int dgIndex;
            int newdualgrid = 0;
            Vector3 newVert;

            //find difference in LOD levels, for a start x,y,z index (beginning of chunk, not beginning of loop)

            //it is guaranteed at this point that the density of the seam >= density of chunk
            float relativeDensity = step.x / current.step.x;
            int indicesToCover = (int)relativeDensity;
            int startX = 0, startY = 0, startZ = 0;

            //apply offset to finding indices

            //CHANGE THIS SUCH THAT IT DOESNT RELY ON OFFSET AND STEP, SINCE THESE ARE CHANGED TO ACCOUNT FOR THE EXTRA LAYER
            startX += (int)((offset.x - current.offset.x) / current.step.x);
            startY += (int)((offset.y - current.offset.y) / current.step.y);
            startZ += (int)((offset.z - current.offset.z) / current.step.z);

            //z face
            z = sizeZ - 2;
            for (x = 0; x < sizeX - 1; ++x)
            {
                for (y = 0; y < sizeY - 1; ++y)
                {
                    newdualgrid = dualGrid[(x + sizeX * (y + sizeY * z))];
                    if (newdualgrid == -1) continue;
                    newVert = vertices[newdualgrid >> 6 & 0x7FFF];

                    current.vertices.Add(newVert);
                    newdualgrid = (newdualgrid & ~(0x7FFF << 6)) | ((current.vertices.Length - 1) << 6);

                    cz = (current.sizeZ-1) - resolution - 1;
                    for (cx = 0; cx < indicesToCover; ++cx)
                    {
                        for (cy = 0; cy < indicesToCover; ++cy)
                        {

                            //the x and y variables (NOT CX CY) need to be multiplied by something
                            dgIndex = ((startX + (x * indicesToCover) + cx) + current.sizeX * ((startY + (y * indicesToCover) + cy) + current.sizeY * cz));
                            current.dualGrid[dgIndex] = newdualgrid;
                        }
                    }

                }
            }

            //x face
            x = sizeX-2;
            for (y = 0; y < sizeY - 1; ++y)
            {
                for (z = 0; z < sizeZ - 1; ++z)
                {

                    newdualgrid = dualGrid[(x + sizeX * (y + sizeY * z))];
                    if (newdualgrid == -1) continue;
                    newVert = vertices[newdualgrid >> 6 & 0x7FFF];

                    current.vertices.Add(newVert);
                    newdualgrid = (newdualgrid & ~(0x7FFF << 6)) | ((current.vertices.Length - 1) << 6);

                    cx = (current.sizeX-1) - resolution - 1;
                    for (cy = 0; cy < indicesToCover; ++cy)
                    {
                        for (cz = 0; cz < indicesToCover; ++cz)
                        {

                            //the x and y variables (NOT CX CY) need to be multiplied by something
                            dgIndex = ((cx) + current.sizeX * ((startY + (y * indicesToCover) + cy) + current.sizeY * (startZ + (z * indicesToCover) + cz)));
                            current.dualGrid[dgIndex] = newdualgrid;
                        }

                    }
                }
            }

            //y face
            y = sizeY - 2;
            for (x = 0; x < sizeX - 1; ++x)
            {
                for (z = 0; z < sizeZ - 1; ++z)
                {

                    newdualgrid = dualGrid[(x + sizeX * (y + sizeY * z))];
                    if (newdualgrid == -1) continue;
                    newVert = vertices[newdualgrid >> 6 & 0x7FFF];

                    current.vertices.Add(newVert);
                    newdualgrid = (newdualgrid & ~(0x7FFF << 6)) | ((current.vertices.Length - 1) << 6);

                    cy = (current.sizeY - 1) - resolution - 1;
                    for (cx = 0; cx < indicesToCover; ++cx)
                    {
                        for (cz = 0; cz < indicesToCover; ++cz)
                        {

                            //the x and y variables (NOT CX CY) need to be multiplied by something
                            dgIndex = ((startX + (x * indicesToCover) + cx) + current.sizeX * (cy + current.sizeY * (startZ + z * indicesToCover + cz)));
                            current.dualGrid[dgIndex] = newdualgrid;
                        }

                    }
                }


            }
        }


        public void ConstructSeamNodes(Dual_Contour current) {
            
            Vector3 cmin = current.min;
            Vector3 cmax = current.max;

            int rule = GetNeighborRule(cmin,cmax, min, max);

            int x, y, z;

            //the in-plane offset when filling in subdivided space
            int cx, cy, cz;

            int dgIndex;
            int newdualgrid = 0;
            Vector3 newVert;

            //find difference in LOD levels, for a start x,y,z index (beginning of chunk, not beginning of loop)

            //it is guaranteed at this point that the density of the seam >= density of chunk
            float relativeDensity = step.x / current.step.x;
            int indicesToCover = (int)relativeDensity;
            int startX = 0, startY = 0, startZ = 0;

            //apply offset to finding indices
            startX += (int)((offset.x - current.offset.x) / current.step.x);
            startY += (int)((offset.y - current.offset.y) / current.step.y);
            startZ += (int)((offset.z - current.offset.z) / current.step.z);
            //apply a start and end index to loop over

            //core logic is already present. Just have to find indices.

            //all neighboring chunks will add to the outside of the array based on where they are as neighbors

            int start, end;


            switch (rule)
            {
                case 0:
                    break;
                case 1: //001
                    z = 0;
                    for ( x = 0; x < sizeX-1; ++x)
                    {
                        for ( y = 0; y < sizeY-1; ++y) {
                            newdualgrid = dualGrid[(x + sizeX * (y + sizeY * z))];
                            if (newdualgrid == -1) continue;
                            newVert = vertices[newdualgrid >> 6 & 0x7FFF];

                            current.vertices.Add(newVert);
                            newdualgrid = (newdualgrid & ~(0x7FFF << 6) ) | ((current.vertices.Length - 1) << 6);

                            cz = current.sizeZ - 1;
                            for (cx = 0; cx < indicesToCover; ++cx)
                            {
                                for (cy = 0; cy < indicesToCover; ++cy) {

                                    //the x and y variables (NOT CX CY) need to be multiplied by something
                                    dgIndex = ((startX + (x*indicesToCover) + cx) + current.sizeX * ((startY + (y*indicesToCover) + cy) + current.sizeY * cz));
                                    current.dualGrid[dgIndex] = newdualgrid;
                                }
                            }                            
                            
                        }
                    }
                    break;
                case 2: //010
                    
                    
                     y = 0;
                    for (x = 0; x < sizeX - 1; ++x)
                    {
                        for (z = 0; z < sizeZ - 1; ++z)
                        {

                            newdualgrid = dualGrid[(x + sizeX * (y + sizeY * z))];
                            if (newdualgrid == -1) continue;
                            newVert = vertices[newdualgrid >> 6 & 0x7FFF];

                            current.vertices.Add(newVert);
                            newdualgrid = (newdualgrid & ~(0x7FFF << 6)) | ((current.vertices.Length - 1) << 6);

                            cy = current.sizeY - 1;
                            for (cx = 0; cx < indicesToCover; ++cx)
                            {
                                for (cz = 0; cz < indicesToCover; ++cz)
                                {

                                    //the x and y variables (NOT CX CY) need to be multiplied by something
                                    dgIndex = ((startX + (x * indicesToCover) + cx) + current.sizeX * (cy + current.sizeY * (startZ + z*indicesToCover +cz) ));
                                    current.dualGrid[dgIndex] = newdualgrid;
                                }

                            }
                        }
                    }
                    break;
                case 3: //011
                    
                    
                    y = 0;
                    z = 0;
                    for (x = 0; x < sizeX - 1; ++x)
                    {

                        newdualgrid = dualGrid[(x + sizeX * (y + sizeY * z))];
                        if (newdualgrid == -1) continue;
                        newVert = vertices[newdualgrid >> 6 & 0x7FFF];

                        current.vertices.Add(newVert);
                        newdualgrid = (newdualgrid & ~(0x7FFF << 6)) | ((current.vertices.Length - 1) << 6);

                        cy = current.sizeY - 1;
                        cz = current.sizeZ - 1;
                        for (cx = 0; cx < indicesToCover; ++cx)
                        {

                            //the x and y variables (NOT CX CY) need to be multiplied by something
                            dgIndex = ((startX + (x * indicesToCover) + cx) + current.sizeX * (cy + current.sizeY * cz));
                            current.dualGrid[dgIndex] = newdualgrid;

                        }
                    }



                    break;
                case 4: //100
                    
                    
                    x = 0;
                    for (y = 0; y < sizeY - 1; ++y)
                    {
                        for (z = 0; z < sizeZ - 1; ++z)
                        {

                            newdualgrid = dualGrid[(x + sizeX * (y + sizeY * z))];
                            if (newdualgrid == -1) continue;
                            newVert = vertices[newdualgrid >> 6 & 0x7FFF];

                            current.vertices.Add(newVert);
                            newdualgrid = (newdualgrid & ~(0x7FFF << 6)) | ((current.vertices.Length - 1) << 6);

                            cx = current.sizeX - 1;
                            for (cy = 0; cy < indicesToCover; ++cy)
                            {
                                for (cz = 0; cz < indicesToCover; ++cz)
                                {

                                    //the x and y variables (NOT CX CY) need to be multiplied by something
                                    dgIndex = ((cx) + current.sizeX * ((startY + (y * indicesToCover) + cy) + current.sizeY * (startZ + (z * indicesToCover) + cz)  ));
                                    current.dualGrid[dgIndex] = newdualgrid;
                                }

                            }
                        }
                    }
                    break;
                case 5: //101
                    

                    

                    x = 0;
                    z = 0;
                    for (y = 0; y < sizeY - 1; ++y)
                    {

                        newdualgrid = dualGrid[(x + sizeX * (y + sizeY * z))];
                        if (newdualgrid == -1) continue;
                        newVert = vertices[newdualgrid >> 6 & 0x7FFF];

                        current.vertices.Add(newVert);
                        newdualgrid = (newdualgrid & ~(0x7FFF << 6)) | ((current.vertices.Length - 1) << 6);

                        cx = current.sizeX - 1;
                        cz = current.sizeZ - 1;
                        for (cy = 0; cy < indicesToCover; ++cy)
                        {

                            //the x and y variables (NOT CX CY) need to be multiplied by something
                            dgIndex = ((cx) + current.sizeX * ((startY + (y * indicesToCover) + cy) + current.sizeY * cz));
                            current.dualGrid[dgIndex] = newdualgrid;

                        }
                    }

                    break;
                case 6: //110
                    
                    x = 0;
                    y = 0;
                    for (z = 0; z < sizeZ - 1; ++z)
                    {

                        newdualgrid = dualGrid[(x + sizeX * (y + sizeY * z))];
                        if (newdualgrid == -1) continue;
                        newVert = vertices[newdualgrid >> 6 & 0x7FFF];

                        current.vertices.Add(newVert);
                        newdualgrid = (newdualgrid & ~(0x7FFF << 6)) | ((current.vertices.Length - 1) << 6);

                        cy = current.sizeY - 1;
                        cx = current.sizeX - 1;
                        for (cz = 0; cz < indicesToCover; ++cz)
                        {

                            //the x and y variables (NOT CX CY) need to be multiplied by something
                            dgIndex = ((cx) + current.sizeX * ((cy) + current.sizeY * (startZ + (z*indicesToCover) + cz)  ));
                            current.dualGrid[dgIndex] = newdualgrid;

                        }
                    }

                    break;
                case 7: //111
                    
                    x = 0;
                    y = 0;
                    z = 0;


                    newdualgrid = dualGrid[(x + sizeX * (y + sizeY * z))];
                    if (newdualgrid == -1) break;
                    newVert = vertices[newdualgrid >> 6 & 0x7FFF];

                    current.vertices.Add(newVert);
                    newdualgrid = (newdualgrid & ~(0x7FFF << 6)) | ((current.vertices.Length - 1) << 6);

                    cy = current.sizeY - 1;
                    cz = current.sizeZ - 1;
                    cx = current.sizeX - 1;

                    //the x and y variables (NOT CX CY) need to be multiplied by something
                    dgIndex = ((cx) + current.sizeX * ((cy) + current.sizeY * cz));
                    current.dualGrid[dgIndex] = newdualgrid;

                    break;
                default:
                    break;
            }
        }
    }
}
