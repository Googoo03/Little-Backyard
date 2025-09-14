using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Simplex;
using SignedDistanceFields;
using Unity.Collections;
using chunk_events;
using Unity.Jobs;
using Unity.Burst;
using SparseVoxelOctree;



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


    enum BLOCKID : ushort
    {
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

        struct Trirule
        {
            public int axis;
            public int sign;
            //public (int dx, int dy, int dz)[] vertpos;
            public (int dx, int dy, int dz) v0;
            public (int dx, int dy, int dz) v1;
            public (int dx, int dy, int dz) v2;
            public (int dx, int dy, int dz) v3;
            public (int dx, int dy, int dz) v4;
            public (int dx, int dy, int dz) v5;

            // indexer to access like an array
            public (int dx, int dy, int dz) this[int i]
            {
                get => i switch
                {
                    0 => v0,
                    1 => v1,
                    2 => v2,
                    3 => v3,
                    4 => v4,
                    5 => v5,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        static readonly Trirule[] rules = {
            new Trirule { //x axis
                axis = 0x20,
                sign = 0x10,

                v0 = (0,0,0),
                v1 = (1,0,0),
                v2 = (1,1,0),
                v3 = (1,1,0),
                v4 = (0,1,0),
                v5 = (0,0,0)
            },
            new Trirule { //y axis
                axis = 0x08,
                sign = 0x04,

                v0 = (0,0,0),
                v1 = (1,0,0),
                v2 = (1,0,1),
                v3 = (1,0,1),
                v4 = (0,0,1),
                v5 = (0,0,0)

            },
            new Trirule { //z axis
                axis = 0x02,
                sign = 0x01,

                v0 = (0,0,0),
                v1 = (0,1,0),
                v2 = (0,1,1),
                v3 = (0,1,1),
                v4 = (0,0,1),
                v5 = (0,0,0)

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

                        foreach (Trirule rule in rules)
                        {
                            if ((dualGrid_index & rule.axis) != rule.axis) continue;

                            var verts = rule;
                            if ((dualGrid_index & rule.sign) != rule.sign)
                            {
                                verts = new Trirule
                                {
                                    axis = rule.axis,
                                    sign = rule.sign,
                                    v0 = rule[0],
                                    v1 = rule[4],
                                    v2 = rule[2],
                                    v3 = rule[3],
                                    v4 = rule[1],
                                    v5 = rule[5]
                                };
                            }

                            for (int j = 0; j < 2; ++j)
                            {
                                var (dx, dy, dz) = verts[j];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0) continue;

                                (dx, dy, dz) = verts[j + 1];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0) continue;

                                (dx, dy, dz) = verts[j + 2];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0) continue;



                                for (int i = 0; i < 3; ++i)
                                {
                                    (dx, dy, dz) = verts[j * 3 + i];
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
    public struct SeamVertexParallel : IJob
    {
        public NativeList<Vector3> vertsParallel;
        public int sizeX, sizeY, sizeZ;
        public int resolution;

        public NativeArray<int> dualGrid;

        public NativeArray<float> vertValues;
        public NativeArray<Vector3> vertPos;
        public NativeArray<Vector4> cellPacked;

        public bool block_voxel;

        float adapt(float x0, float x1) => (-x0) / (x1 - x0);

        public void DetermineVertex(int x, int y, int z)
        {
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
                dualGrid[index] = (newedge | ((vertsParallel.Length - 1) << 6) | (voxelData << 21));
            }
        }
        public void Execute()
        {
            int x, y, z;
            int spacing = 3 * resolution - 1;

            for (x = 0; x < sizeX - 1; ++x)
            {
                for (y = 0; y < sizeY - 1; ++y)
                {
                    for (z = sizeZ - spacing - 1; z < sizeZ - 1; ++z)
                    {
                        DetermineVertex(x, y, z);

                    }
                }
            }



            /////////////BROKEN THIS  LOOP
            ///
            for (x = sizeX - spacing - 1; x < sizeX - 1; ++x)
            {
                for (y = 0; y < sizeY - 1; ++y)
                {

                    for (z = 0; z < sizeZ - 1; ++z)
                    {
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
                    for (y = sizeY - spacing - 1; y < sizeY - 1; ++y)
                    {
                        DetermineVertex(x, y, z);
                    }
                }
            }


            x = sizeX - spacing - 1;
            y = sizeY - spacing - 1;
            for (; x < sizeX - 1; ++x)
            {
                for (; y < sizeY - 1; ++y)
                {
                    for (z = 0; z < sizeZ - spacing - 1; ++z)
                    {
                        DetermineVertex(x, y, z);
                    }
                }
            }

            z = sizeZ - spacing - 1;
            y = sizeY - spacing - 1;
            for (x = 0; x < sizeX - spacing - 1; ++x)
            {
                for (; y < sizeY - 1; ++y)
                {
                    for (; z < sizeZ - 1; ++z)
                    {
                        DetermineVertex(x, y, z);
                    }
                }
            }

            x = sizeX - spacing - 1;
            z = sizeZ - spacing - 1;
            for (; x < sizeX - 1; ++x)
            {
                for (y = 0; y < sizeY - spacing - 1; ++y)
                {
                    for (; z < sizeZ - 1; ++z)
                    {
                        DetermineVertex(x, y, z);
                    }
                }
            }

            x = sizeX - spacing - 1;
            y = sizeY - spacing - 1;
            z = sizeZ - spacing - 1;
            for (; x < sizeX - 1; ++x)
            {
                for (; y < sizeY - 1; ++y)
                {
                    for (; z < sizeZ - 1; ++z)
                    {
                        DetermineVertex(x, y, z);
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
            //public (int dx, int dy, int dz)[] vertpos;
            public (int dx, int dy, int dz) v0;
            public (int dx, int dy, int dz) v1;
            public (int dx, int dy, int dz) v2;
            public (int dx, int dy, int dz) v3;
            public (int dx, int dy, int dz) v4;
            public (int dx, int dy, int dz) v5;

            // indexer to access like an array
            public (int dx, int dy, int dz) this[int i]
            {
                get => i switch
                {
                    0 => v0,
                    1 => v1,
                    2 => v2,
                    3 => v3,
                    4 => v4,
                    5 => v5,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        static readonly Trirule[] rules = {
            new Trirule { //x axis
                axis = 0x20,
                sign = 0x10,

                v0 = (0,0,0),
                v1 = (1,0,0),
                v2 = (1,1,0),
                v3 = (1,1,0),
                v4 = (0,1,0),
                v5 = (0,0,0)
            },
            new Trirule { //y axis
                axis = 0x08,
                sign = 0x04,

                v0 = (0,0,0),
                v1 = (1,0,0),
                v2 = (1,0,1),
                v3 = (1,0,1),
                v4 = (0,0,1),
                v5 = (0,0,0)

            },
            new Trirule { //z axis
                axis = 0x02,
                sign = 0x01,

                v0 = (0,0,0),
                v1 = (0,1,0),
                v2 = (0,1,1),
                v3 = (0,1,1),
                v4 = (0,0,1),
                v5 = (0,0,0)

            },
        };

        public void Execute()
        {
            for (int x = 0; x < sizeX - 1; ++x)
            {
                for (int y = 0; y < sizeY - 1; ++y)
                {
                    for (int z = 0; z < sizeZ - 1; ++z)
                    {
                        int index = x + sizeX * (y + sizeY * z);
                        int dualGridValue = dualGrid[index];

                        if (dualGridValue == -1) continue; // No vertex for this cell

                        foreach (Trirule rule in rules)
                        {
                            if ((dualGridValue & rule.axis) != rule.axis) continue;

                            var verts = rule;
                            if ((dualGridValue & rule.sign) != rule.sign)
                            {
                                // Flip winding
                                verts = new Trirule
                                {
                                    axis = rule.axis,
                                    sign = rule.sign,
                                    v0 = rule[0],
                                    v1 = rule[4],
                                    v2 = rule[2],
                                    v3 = rule[3],
                                    v4 = rule[1],
                                    v5 = rule[5]
                                };
                            }

                            for (int j = 0; j < 2; ++j)
                            {
                                var (dx, dy, dz) = verts[j];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == -1 || dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0) continue;

                                (dx, dy, dz) = verts[j + 1];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == -1 || dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0) continue;

                                (dx, dy, dz) = verts[j + 2];
                                if (dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == -1 || dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] == 0) continue;

                                /*
                                (dx, dy, dz) = verts[j];
                                indices.Add(dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] >> 6 & 0x7FFF);
                                indices.Add(0);
                                indices.Add(1);
                                */


                                for (int i = 0; i < 3; ++i)
                                {
                                    (dx, dy, dz) = verts[j * 3 + i];
                                    if ((dualGrid[(x + dx) + sizeX * ((y + dy) + sizeY * (z + dz))] >> 6 & 0x7FFF) > vertices.Length)
                                    {

                                        indices.Add(0);
                                        continue;
                                    }

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
    public struct VertexParallel : IJob
    {

        public NativeList<Vector3> vertsParallel;
        public int sizeX, sizeY, sizeZ;
        public int vert_index;

        public NativeArray<int> dualGrid;

        public NativeArray<float> vertValues;
        public NativeArray<Vector3> vertPos;
        public NativeArray<Vector4> cellPacked;

        public bool block_voxel;

        readonly float Adapt(float x0, float x1) => (-x0) / (x1 - x0);

        public void Execute()
        {
            int index = 0;

            for (int x = 0; x < sizeX - 1; ++x)
            {
                for (int y = 0; y < sizeY - 1; ++y)
                {
                    for (int z = 0; z < sizeZ - 1; ++z)
                    {
                        index = x + sizeX * (y + sizeY * z);
                        dualGrid[index] = -1;
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
                                avg += vertPos[i] + Adapt(a, b) * (vertPos[i | 0x04] - vertPos[i]);
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
                                avg += vertPos[j] + Adapt(a, b) * (vertPos[j | 0x02] - vertPos[j]);
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
                                avg += vertPos[i << 1] + Adapt(a, b) * (vertPos[(i << 1) | 0x01] - vertPos[i << 1]);
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

    public struct Trirule
    {
        public int axis;
        public int sign;
        //public (int dx, int dy, int dz)[] vertpos;
        public int v0;
        public int v1;
        public int v2;
        public int v3;
        public int v4;
        public int v5;

        // indexer to access like an array
        public int this[int i]
        {
            get => i switch
            {
                0 => v0,
                1 => v1,
                2 => v2,
                3 => v3,
                4 => v4,
                5 => v5,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }


    public class Dual_Contour
    {

        [SerializeField] bool IsSeam;

        //MESH DIMENSIONS
        [SerializeField] private Vector3Int baseSize;
        [SerializeField] private float baseCELL_SIZE;

        [SerializeField] private int sizeX;
        [SerializeField] private int sizeY;
        [SerializeField] private int sizeZ;
        [SerializeField] private float T;
        [SerializeField] private float lastT;
        [SerializeField] private float amplitude;
        [SerializeField] private int LOD_Level;
        [SerializeField] private int dir; //on spherical meshes, this defines the uv direction (on a uv sphere) and sign
        [SerializeField] private int resolution; //for seams, determines how many times it needs to subdivide to be on the same level as the highest fidelity neighbor

        //REFRESH
        [SerializeField] private bool initialized = false;

        //MESH MODE
        [SerializeField] private bool block_voxel;

        [SerializeField] private float CELL_SIZE; //determines how far in units all the cells in a particular axis should cover.

        [SerializeField] private float Ground;

        //GLOBAL VARS
        Vector3 step;
        Vector3 offset;

        //VOXEL DATA
        UInt16[] voxel_data;

        private List<int> indices;
        private List<Vector3> vertices;
        private NativeArray<int> dualGrid;
        private NativeArray<Vector4> cellPacked;
        private NativeArray<Vector3> cellpos;
        private NativeArray<float> cellvalues;


        float[] vertValues = new float[8];
        Vector3[] vertPos = new Vector3[8];

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


        public Dual_Contour(Vector3 _global, Vector3Int scale, Vector3 ioffset, int ilodLevel, int length, bool mode, float iradius, int idir)
        {
            global = _global;
            baseSize = scale;
            sizeX = scale.x;
            sizeY = scale.y;
            sizeZ = scale.z;
            baseCELL_SIZE = length;
            CELL_SIZE = length;
            LOD_Level = ilodLevel;
            Ground = 32;
            amplitude = 3;
            block_voxel = mode;
            radius = iradius;
            dir = idir;
            coordTransformFunction = CartesianToSphere;
            resolution = 1;
            IsSeam = false;


            offset = 0.5f * CELL_SIZE * -Vector3.one;
            offset += ioffset * CELL_SIZE;

        }

        public Dual_Contour()
        {


        }

        private float function(Vector3 pos, float y)
        {

            float frequency = 1f;
            float domainWarp = 0;
            Vector3 worldPos = pos - global;

            float elevation = (-CELL_SIZE / 2) + (-offset).y + (y * step.y);
            Vector3 spherePos = pos;

            float radius = pos.y;//worldPos.magnitude;
            float caveVal = simplexNoise.CalcPixel3D(pos.x * frequency, pos.y * frequency, pos.z * frequency) * amplitude;
            float value = simplexNoise.CalcPixel3D((spherePos.x + domainWarp) * frequency, (spherePos.y + domainWarp) * frequency, (spherePos.z + domainWarp) * frequency) * 10 - radius + 10;
            //if(value < 0) value += caveVal;
            //value *= caveVal;
            //float value = Ground - radius;
            //UnityEngine.Debug.Log("Value is: " + value);

            return value;
        }

        ///INITIALIZE GRID INFORMATION UPON STARTUP/----------------------------------------------------------------------------

        public void SetVertexList(List<Vector3> v) { vertices = v; }
        private ushort DetermineVoxelData(Vector3 pos, int x, int elevation, int z)
        {
            float oreVal = 1;// simplexNoise.CalcPixel3D(pos.x * 3, pos.y * 3, pos.z * 3) * 2f;
                             //Ground/Stone pass
            BLOCKID val;
            //These should include IDs for the given blocks by enumeration, rather than the plain ids themselves
            float cellvalue = cellPacked[x + sizeX * (elevation + sizeY * z)].w;

            val = cellvalue > 0 ? BLOCKID.STONE : BLOCKID.AIR;
            if (val == BLOCKID.AIR) return (ushort)val;

            val = elevation < Ground ? BLOCKID.STONE : BLOCKID.GRASS;

            //Ore pass
            val = oreVal > 0.8f && val == BLOCKID.STONE ? BLOCKID.COPPER_ORE : val;
            val = oreVal < 0.3f && val == BLOCKID.STONE ? BLOCKID.IRON_ORE : val;

            //Other passes once the time comes
            return (ushort)val;
        }

        public void InitializeBounds(Vector3 parentOffset)
        {
            min = offset;

            //be careful that cell_size is before it increments, in fact, it may be better if the seam just inherits from the parent chunk
            max = offset + ((1f / (1 << LOD_Level)) * CELL_SIZE * Vector3.one);
        }

        [BurstCompile]
        public void InitializeGrid(bool seam, ref NativeList<Vector3> verts, ref NativeList<int> ind)
        {

            //indices = ind;
            //vertices = verts;

            //Clear data if refreshing
            //if (vertices.Length > 0) vertices.Clear();
            //if (indices.Length > 0) indices.Clear();
            if (cellPacked != null) cellPacked.Dispose();
            if (dualGrid != null) dualGrid.Dispose();

            sizeX = baseSize.x;
            sizeY = baseSize.y;
            sizeZ = baseSize.z;
            CELL_SIZE = baseCELL_SIZE;

            if (seam)
            {
                IsSeam = true;
                CELL_SIZE += (CELL_SIZE / sizeX); //want to cover n+1 units, where n is the initial length
                sizeX++;
                sizeY++;
                sizeZ++;
            }

            sizeX *= resolution;
            sizeY *= resolution;
            sizeZ *= resolution;

            //LOD_Level += resolution - 1;

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

                    }
                }
            }

            vertJob = new VertexParallel
            {
                cellPacked = cellPacked,
                dualGrid = dualGrid,
                vertValues = new NativeArray<float>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                vertPos = new NativeArray<Vector3>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                vertsParallel = new NativeList<Vector3>(Allocator.TempJob),


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
                vertValues = new NativeArray<float>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                vertPos = new NativeArray<Vector3>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                vertsParallel = new NativeList<Vector3>(Allocator.TempJob),
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
                indices = new NativeList<int>(Allocator.TempJob),
                dualGrid = dualGrid

            };

            seamQuadJob = new SeamQuadParallel
            {
                sizeX = sizeX,
                sizeY = sizeY,
                sizeZ = sizeZ,
                indices = new NativeList<int>(Allocator.TempJob),
                dualGrid = dualGrid,
                vertices = new NativeList<Vector3>(Allocator.TempJob)
            };

        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        public Vector3 GetMin() { return min; }
        public Vector3 GetMax() { return max; }

        //returns the center based on base cartestian grid coordinates
        public Vector3 GetCenter() { return (min + max) / 2; }

        //returns the center based on transformed coordinates
        public Vector3 GetCornerCenter()
        {
            int lastIndex = ((sizeX - 1) + sizeX * (sizeY - 1 + sizeY * (sizeZ - 1)));
            return ((Vector3)cellPacked[0] + (Vector3)cellPacked[lastIndex]) / 2;
        }

        public void SetResolution(int iresolution) { resolution = iresolution; }

        /*
        public void Generate(ref NativeList<Vector3> verts, ref NativeList<int> ind, ref UInt16[] v_data, ref int vert_index, ref int ind_index)
        {
            //initialize both the dual grid and the vertex grid

            indices = ind;
            vertices = verts;

            //This computes all the vertices except the edge cases (quite literally)
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            JobHandle vertScheduler = vertJob.Schedule();
            vertScheduler.Complete();

            stopwatch.Stop();
            UnityEngine.Debug.Log("Vertices took " + stopwatch.ElapsedMilliseconds.ToString() + " milliseconds");

            stopwatch.Reset();
            stopwatch.Start();

            JobHandle quadScheduler = quadJob.Schedule();
            quadScheduler.Complete();

            stopwatch.Stop();
            UnityEngine.Debug.Log("Indices took " + stopwatch.ElapsedMilliseconds.ToString() + " milliseconds");

            v_data = voxel_data;
        }*/

        public void UpdateVoxelData(ref List<chunk_event> points)
        {
            //Given a list of update points, that are assumed to be in range.

            Vector3 localPos;
            Vector3 currentCellPos;

            //Find the index of said points.
            points.ForEach(item =>
            {

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

        private void UpdateCellValueEntry(int x, int y, int z)
        {
            if (x < 0 || x > sizeX || y < 1 || y > sizeY || z < 0 || z > sizeZ) return;

            cellvalues[(int)(x + sizeX * (y + sizeY * z))] = -1;
        }
        //////////////////////////////////////////////////////////////////////////////

        private Vector3 CartesianToSphere(Vector3 pos, int elevation)
        {

            //Given a grid position, convert the point into a shell point
            //dir contains the u and v direction, which are masks for the x and z positions.

            float radius = 32768;

            int uSign = ((dir & 0x80) != 0) ? 1 : -1;
            int vSign = ((dir & 0x08) != 0) ? 1 : -1;
            Vector3 uaxis = new Vector3(((dir >> 6 & 0x7FFF) & 0x01), (dir >> 5) & 0x01, (dir >> 4) & 0x01) * (uSign);
            Vector3 vaxis = new Vector3(((dir >> 2) & 0x01), (dir >> 1) & 0x01, (dir) & 0x01) * (vSign);
            Vector3 wAxis = Vector3.Cross(vaxis, uaxis);


            //no idea why the step.x / 2. Why would it need to be pushed back half a unit? BECAUSE THE QUADS ARE CENTERED BY DEFAULT
            Vector3 newpos = uaxis * pos.x + vaxis * pos.z + wAxis * (radius - step.y / 2);

            return newpos.normalized * (radius + (elevation * step.y) + offset.y);
        }

        Vector3 CubeToSphereDirection(Vector3 p)
        {
            float x = p.x;
            float y = p.y;
            float z = p.z;

            float x2 = x * x;
            float y2 = y * y;
            float z2 = z * z;

            return new Vector3(
                x * Mathf.Sqrt(1f - (y2 / 2f) - (z2 / 2f) + (y2 * z2) / 3f),
                y * Mathf.Sqrt(1f - (z2 / 2f) - (x2 / 2f) + (x2 * z2) / 3f),
                z * Mathf.Sqrt(1f - (x2 / 2f) - (y2 / 2f) + (x2 * y2) / 3f)
            );
        }

        private Vector3 GridToSphere(Vector3 pos, int elevation)
        {
            float r = pos.magnitude; // radial distance
            return CubeToSphereDirection(pos.normalized) * r;
        }

        //returns in radians the polar coords of a given cartesian coordinate
        private Vector3 CartesianToPolarCoords(Vector3 Relativecartesian)
        {
            float r = Relativecartesian.magnitude;
            float theta = Mathf.Atan2(Relativecartesian.y, Relativecartesian.x);
            float phi = Mathf.Acos(Relativecartesian.z / r);
            return new Vector3(r, theta, phi);
        }

        private Vector3 Grid(Vector3 pos, int elevation) { return pos; }

        public Vector3 FindTransformedCoord(Vector3 pos, int elevation) { return coordTransformFunction(pos, elevation); }

        private Vector3 ShellElevate(Vector3 pos) { return new Vector3(pos.x, pos.y + simplexNoise.CalcPixel3D((global.x + pos.x) / 2, 0, (global.z + pos.z) / 2) * 20, pos.z); }

        public VertexParallel GetVertexParallel() { return vertJob; }

        public QuadParallel GetQuadParallel() { return quadJob; }

        public SeamQuadParallel GetSeamQuadParallel() { return seamQuadJob; }

        public SeamVertexParallel GetSeamVertexParallel() { return seamVertexJob; }

        public void SetGlobal(Vector3 g) { global = g; }

        public Vector3 GetStep() { return step; }

        public int GetLODLevel() { return LOD_Level; }

        public float GetSideLength() { return max.x - min.x; }

        public Vector3 GetOffset() { return offset; }

        float Adapt(float x0, float x1) => (-x0) / (x1 - x0);

        public void SVOVertex(SVONode node)
        {
            //these are being allocated every frame, which is bad


            int xPos, yPos, zPos;

            for (int i = 0; i < 8; ++i)
            {
                xPos = node.position.x + ((i >> 2) & 0x01) * node.size;
                yPos = node.position.y + ((i >> 1) & 0x01) * node.size;
                zPos = node.position.z + (i & 0x01) * node.size;

                //evaluate the position and value of each vertex in the unit cube
                vertPos[i] = new Vector3(xPos, yPos, zPos);
                vertValues[i] = vertPos[i].y - 10; //function(vertPos[i], yPos); //base SDF
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

            if (!signChange) return;

            //X EDGES NEW 
            for (int i = 0; i < 4; ++i)
            {
                float a = vertValues[i];
                float b = vertValues[i | 0x04];
                if (a > 0 != b > 0)
                {
                    avg += vertPos[i] + Adapt(a, b) * (vertPos[i | 0x04] - vertPos[i]);
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
                    avg += vertPos[j] + Adapt(a, b) * (vertPos[j | 0x02] - vertPos[j]);
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
                    avg += vertPos[i << 1] + Adapt(a, b) * (vertPos[(i << 1) | 0x01] - vertPos[i << 1]);
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

            vertices.Add(vertex);
            //This should reference a biome texture and current elevation
            UInt16 voxelData = 0;

            node.dualVertexIndex = newedge | ((vertices.Count - 1) << 6) | (voxelData << 21);


        }





        public void SVOQuad(SVONode node, List<int> indices)
        {
            SVONode zNeighbor = node.GetNeighborLOD(1);
            SVONode yNeighbor = node.GetNeighborLOD(2);
            SVONode yzNeighbor = yNeighbor?.GetNeighborLOD(1);
            SVONode xNeighbor = node.GetNeighborLOD(4);
            SVONode xzNeighbor = zNeighbor?.GetNeighborLOD(4);
            SVONode xyNeighbor = xNeighbor?.GetNeighborLOD(2);
            SVONode[] neighbors = { node, zNeighbor, yNeighbor, yzNeighbor, xNeighbor, xzNeighbor, xyNeighbor };

            Trirule[] rules = {
            new() { //x axis
                axis = 0x20,
                sign = 0x10,

                v0 = 0,
                v1 = 4,
                v2 = 6,
                v3 = 6,
                v4 = 2,
                v5 = 0
            },
            new () { //y axis
                axis = 0x08,
                sign = 0x04,

                v0 = 0,
                v1 = 4,
                v2 = 5,
                v3 = 5,
                v4 = 1,
                v5 = 0

            },
            new () { //z axis
                axis = 0x02,
                sign = 0x01,

                v0 = 0,
                v1 = 2,
                v2 = 3,
                v3 = 3,
                v4 = 1,
                v5 = 0

            },
        };

            int dualGrid_index = node.dualVertexIndex;

            foreach (Trirule rule in rules)
            {
                if ((dualGrid_index & rule.axis) != rule.axis) continue;

                var verts = rule;
                if ((dualGrid_index & rule.sign) != rule.sign)
                {
                    verts = new Trirule
                    {
                        axis = rule.axis,
                        sign = rule.sign,
                        v0 = rule[0],
                        v1 = rule[4],
                        v2 = rule[2],
                        v3 = rule[3],
                        v4 = rule[1],
                        v5 = rule[5]
                    };
                }
                //get entire face of neighbor node.

                //if neighbor along certain axis is not a leaf, gather the face

                //then, we need to repeat the logic for neighbor detection as many times as there are children in the faces. This would go on the outside
                //

                //OR IF THE NEIGHBOR ALONG AN AXIS IS NOT A LEAF, REVERSE such that its now going from high to low rather than low to high.
                //have a traverse leaf action along the specific face. Pass in the base node as the negative neighbor


                //two triangles to make a quad
                for (int j = 0; j < 2; ++j)
                {

                    int directionAxis = j == 0 ? rule.v1 : rule.v4;

                    List<SVONode> face = neighbors[directionAxis]?.GetFace(directionAxis);
                    if (face == null) continue;
                    for (int k = 0; k < face.Count; ++k)
                    {
                        zNeighbor = directionAxis == 1 ? face[k] : node.GetNeighborLOD(1);
                        yNeighbor = directionAxis == 2 ? face[k] : node.GetNeighborLOD(2);
                        xNeighbor = directionAxis == 4 ? face[k] : node.GetNeighborLOD(4);

                        yzNeighbor = directionAxis == 2 ? yNeighbor?.GetNeighborLOD(1) : zNeighbor?.GetNeighborLOD(2);
                        xzNeighbor = directionAxis == 4 ? xNeighbor?.GetNeighborLOD(1) : zNeighbor?.GetNeighborLOD(4);
                        xyNeighbor = directionAxis == 4 ? xNeighbor?.GetNeighborLOD(2) : yNeighbor?.GetNeighborLOD(4);

                        neighbors = new SVONode[] { node, zNeighbor, yNeighbor, yzNeighbor, xNeighbor, xzNeighbor, xyNeighbor };

                        List<SVONode> diagonal = neighbors[verts[2]]?.GetFace(rule.v2);

                        int diagonalSize = diagonal != null ? diagonal.Count : 0;
                        for (int l = 0; l < diagonalSize; ++l)
                        {
                            if (diagonal[l].IsEmpty()) continue;
                            var neighbor = neighbors[verts[j * 3]];

                            SVONode n0 = neighbors[verts[j * 3]];
                            SVONode n1 = neighbors[verts[j * 3 + 1]];
                            SVONode n2 = neighbors[verts[j * 3 + 2]];

                            if (n0 == null || n1 == null || n2 == null) continue;
                            if (n0.IsEmpty() || n1.IsEmpty() || n2.IsEmpty()) continue;


                            for (int i = 0; i < 3; ++i)
                            {
                                neighbor = neighbors[verts[j * 3 + i]];

                                if (!neighbor.isLeaf)
                                {
                                    //UnityEngine.Debug.Log("vert " + (j * 3 + i) + "on direction " + directionAxis);
                                    neighbor = diagonal[l];

                                }

                                if ((neighbor?.dualVertexIndex >> 6 & 0x7fff) > vertices.Count)
                                {
                                    indices.Add(0);
                                    continue;
                                }
                                indices.Add(neighbor.dualVertexIndex >> 6 & 0x7FFF);
                            }
                        }
                    }

                }

            }
        }

    }
}
