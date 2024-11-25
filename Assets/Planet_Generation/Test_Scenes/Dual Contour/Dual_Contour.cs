using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Simplex;
using UnityEngine.Assertions;


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
        [SerializeField] private int dir;

        //MESH MODE
        [SerializeField] private bool block_voxel;

        [SerializeField] private float CELL_SIZE;

        [SerializeField] private float Ground;


        List<Vector3> vertices;
        List<int> indices;

        private cell[] dualGrid;

        //NOISE FUNCTIONS TEMPORARY
        Noise simplexNoise = new Noise();

        //
        private Vector3 global;

        public Dual_Contour(Vector3 _global, Vector3Int scale, int length, bool mode) {
            global = _global;
            sizeX = scale.x;
            sizeY = scale.y;
            sizeZ = scale.z;
            CELL_SIZE = length;
            LOD_Level = 0;
            Ground = 2;
            amplitude = 5;
            block_voxel = mode;
        }

        public void Generate(ref List<Vector3> verts, ref List<int> ind)
        {
            //initialize both the dual grid and the vertex grid
            dualGrid = new cell[(sizeX) * (sizeY) * sizeZ];
            indices = ind;

            vertices = verts;

            for (int x = 0; x < sizeX; ++x)
            {
                for (int y = 0; y < sizeY; ++y)
                {
                    for (int z = 0; z < sizeZ; ++z)
                    {
                        dualGrid[x + sizeX * (y + sizeY * z)].vertIndex = -1;
                        Find_Best_Vertex(function, Grid, x, y, z);
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

            verts = vertices;
            ind = indices;

        }

        private void Find_Best_Vertex(Func<Vector3, float, float> f, Func<Vector3, int, Vector3> spaceTransform, int x, int y, int z)
        {


            //determines the "step" between vertices to cover 1 unit total
            Vector3 step = new Vector3(1f / (sizeX), 1f / (sizeY), 1f / (sizeZ));
            step *= (1f / (1 << LOD_Level));
            step *= CELL_SIZE;

            //sets the offset of the grid by a power of two based on the LOD level
            Vector3 offset = -Vector3.one * (1f / (1 << (LOD_Level + 1))) * (CELL_SIZE);

            //We have 8 vertices, and need to store them for later use
            Vector3 pos = offset + new Vector3(x * step.x, y * step.y, z * step.z);


            float[] vertValues = new float[8];
            Vector3[] vertPos = new Vector3[8];
            for (int i = 0; i < 8; ++i)
            {
                vertPos[i] = spaceTransform(pos + new Vector3(step.x * ((i >> 2) & 0x01), step.y * ((i >> 1) & 0x01), step.z * (i & 0x01)), y + ((i & 2) == 2 ? 1 : 0));
                vertValues[i] = f(global + vertPos[i], y + ((i & 2) == 2 ? 1 : 0));
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
            signChange |= (vertValues[0] > 0) != (vertValues[4] > 0);
            signChange |= (vertValues[0] > 0) != (vertValues[1] > 0);
            signChange |= (vertValues[5] > 0) != (vertValues[1] > 0);
            signChange |= (vertValues[5] > 0) != (vertValues[4] > 0);
            signChange |= (vertValues[4] > 0) != (vertValues[6] > 0);
            signChange |= (vertValues[1] > 0) != (vertValues[3] > 0);
            signChange |= (vertValues[0] > 0) != (vertValues[2] > 0);
            signChange |= (vertValues[5] > 0) != (vertValues[7] > 0);
            signChange |= (vertValues[6] > 0) != (vertValues[7] > 0);
            signChange |= (vertValues[3] > 0) != (vertValues[7] > 0);
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

            bool xCross = vertValues[6] > 0 != vertValues[7] > 0;
            bool yCross = vertValues[5] > 0 != vertValues[7] > 0;
            bool zCross = vertValues[3] > 0 != vertValues[7] > 0;

            if (xCross) _newedges[0] = new Edge(true, (vertValues[6] > 0) && !(vertValues[7] > 0)); //need to change intersection and normal later if we want interpolation
            if (yCross) _newedges[1] = new Edge(true, !(vertValues[5] > 0) && (vertValues[7] > 0));
            if (zCross) _newedges[2] = new Edge(true, (vertValues[3] > 0) && !(vertValues[7] > 0));

            Vector3 vertex =  block_voxel ? vertPos[0] : avg;

            vertices.Add(vertex); //vertex should be at the center of the cell
                                  //set cell struct to include vertex index

            int index = (int)(x + sizeX * (y + sizeY * z));
            dualGrid[index].edges = _newedges;
            dualGrid[index].vertIndex = vertices.Count - 1;
        }

        

        private void CreateQuads(int x, int y, int z)
        {
            int index = (int)(x + sizeX * (y + sizeY * z));
            if (dualGrid[index].vertIndex == -1) return;

            //This only checks one orientation of the quad. Need to flip flop indices based on how intersection occurs
            bool notSatisfy = false;

            if (dualGrid[index].edges[0].crossed)
            {
                //Make a quad from neighboring x cells
                if (dualGrid[index].edges[0].sign)
                {
                    notSatisfy = false;
                    notSatisfy |= dualGrid[index].vertIndex == -1;
                    notSatisfy |= dualGrid[(x + 1) + sizeX * (y + sizeY * z)].vertIndex == -1;
                    notSatisfy |= dualGrid[(x + 1) + sizeX * ((y + 1) + sizeY * z)].vertIndex == -1;
                    notSatisfy |= dualGrid[x + sizeX * ((y + 1) + sizeY * z)].vertIndex == -1;



                    if (!notSatisfy)
                    {
                        indices.Add(dualGrid[index].vertIndex);
                        indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * z)].vertIndex);
                        indices.Add(dualGrid[(x + 1) + sizeX * ((y + 1) + sizeY * z)].vertIndex); //these indices might be screwed up
                        indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * z)].vertIndex);
                    }
                }
                else
                {

                    notSatisfy = false;
                    notSatisfy |= dualGrid[index].vertIndex == -1;
                    notSatisfy |= dualGrid[(x) + sizeX * ((y + 1) + sizeY * z)].vertIndex == -1;
                    notSatisfy |= dualGrid[(x + 1) + sizeX * ((y + 1) + sizeY * z)].vertIndex == -1;
                    notSatisfy |= dualGrid[(x + 1) + sizeX * ((y) + sizeY * z)].vertIndex == -1;

                    if (!notSatisfy)
                    {
                        indices.Add(dualGrid[index].vertIndex);
                        indices.Add(dualGrid[(x + sizeX * ((y + 1) + sizeY * z))].vertIndex);
                        indices.Add(dualGrid[((x + 1) + sizeX * ((y + 1) + sizeY * z))].vertIndex);
                        indices.Add(dualGrid[((x + 1) + sizeX * (y + sizeY * z))].vertIndex);
                    }

                }
            }
            if (dualGrid[index].edges[1].crossed)
            {
                //Make a quad from neighboring y cells
                if (dualGrid[index].edges[1].sign)
                {
                    notSatisfy = false;
                    notSatisfy |= dualGrid[index].vertIndex == -1;
                    notSatisfy |= dualGrid[(x + 1) + sizeX * (y + sizeY * z)].vertIndex == -1;
                    notSatisfy |= dualGrid[(x + 1) + sizeX * ((y) + sizeY * (z + 1))].vertIndex == -1;
                    notSatisfy |= dualGrid[x + sizeX * ((y) + sizeY * (z + 1))].vertIndex == -1;



                    if (!notSatisfy)
                    {
                        indices.Add(dualGrid[index].vertIndex);
                        indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * z)].vertIndex);
                        indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * (z + 1))].vertIndex); //these indices might be screwed up
                        indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))].vertIndex);
                    }
                }
                else
                {

                    notSatisfy = false;
                    notSatisfy |= dualGrid[index].vertIndex == -1;
                    notSatisfy |= dualGrid[(x) + sizeX * (y + sizeY * (z + 1))].vertIndex == -1;
                    notSatisfy |= dualGrid[(x + 1) + sizeX * ((y) + sizeY * (z + 1))].vertIndex == -1;
                    notSatisfy |= dualGrid[(x + 1) + sizeX * ((y) + sizeY * z)].vertIndex == -1;

                    if (!notSatisfy)
                    {
                        indices.Add(dualGrid[index].vertIndex);
                        indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))].vertIndex);
                        indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * (z + 1))].vertIndex);
                        indices.Add(dualGrid[(x + 1) + sizeX * (y + sizeY * z)].vertIndex);
                    }

                }
            }
            if (dualGrid[index].edges[2].crossed)
            {
                //Make a quad from neighboring z cells
                if (dualGrid[index].edges[2].sign)
                {

                    notSatisfy = false;
                    notSatisfy |= dualGrid[index].vertIndex == -1;
                    notSatisfy |= dualGrid[(x) + sizeX * ((y + 1) + sizeY * z)].vertIndex == -1;
                    notSatisfy |= dualGrid[(x) + sizeX * ((y + 1) + sizeY * (z + 1))].vertIndex == -1;
                    notSatisfy |= dualGrid[x + sizeX * ((y) + sizeY * (z + 1))].vertIndex == -1;

                    if (!notSatisfy)
                    {
                        indices.Add(dualGrid[index].vertIndex);
                        indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * z)].vertIndex);
                        indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * (z + 1))].vertIndex);
                        indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))].vertIndex);
                    }



                }
                else
                {

                    notSatisfy = false;
                    notSatisfy |= dualGrid[index].vertIndex == -1;
                    notSatisfy |= dualGrid[(x) + sizeX * (y + sizeY * (z + 1))].vertIndex == -1;
                    notSatisfy |= dualGrid[(x) + sizeX * ((y + 1) + sizeY * (z + 1))].vertIndex == -1;
                    notSatisfy |= dualGrid[x + sizeX * ((y + 1) + sizeY * z)].vertIndex == -1;

                    if (!notSatisfy)
                    {
                        indices.Add(dualGrid[index].vertIndex);
                        indices.Add(dualGrid[x + sizeX * (y + sizeY * (z + 1))].vertIndex);
                        indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * (z + 1))].vertIndex); //these indices might be screwed up
                        indices.Add(dualGrid[x + sizeX * ((y + 1) + sizeY * z)].vertIndex);
                    }
                }
            }
        }

        private Vector3 Grid(Vector3 pos, int elevation) { return pos; }

        private Vector3 CartesianToSphere(Vector3 pos, int elevation)
        {

            //Given a grid position, convert the point into a shell point
            //dir contains the u and v direction, which are masks for the x and z positions.
            Vector3 step = new Vector3(1f / (sizeX), 1f / (sizeY), 1f / (sizeZ));
            step *= (1f / (1 << LOD_Level));
            step *= CELL_SIZE;


            int uSign = ((dir & 0x80) != 0) ? 1 : -1;
            int vSign = ((dir & 0x08) != 0) ? 1 : -1;
            Vector3 uaxis = new Vector3(((dir >> 6) & 0x01), (dir >> 5) & 0x01, (dir >> 4) & 0x01) * (uSign);
            Vector3 vaxis = new Vector3(((dir >> 2) & 0x01), (dir >> 1) & 0x01, (dir) & 0x01) * (vSign);
            Vector3 wAxis = Vector3.Cross(vaxis, uaxis);
            float radius = CELL_SIZE / 2;

            //no idea why the step.x / 2. Why would it need to be pushed back half a unit? BECAUSE THE QUADS ARE CENTERED BY DEFAULT
            Vector3 newpos = uaxis * pos.x + vaxis * pos.z + wAxis * (radius - step.x / 2);

            return newpos.normalized * (radius + (elevation * step.y));
            //return newpos;

        }

        private Vector3 ShellElevate(Vector3 pos)
        {
            //simplexNoise.Seed = 1642;
            // Vector3 newpos = new Vector3 (pos.x,pos.y+ simplexNoise.CalcPixel3D(pos.x / 5, 0, pos.z / 5)*1, pos.z);
            //Assert.IsTrue(newpos.x != 0);
            Vector3 newpos = new Vector3(pos.x, pos.y + simplexNoise.CalcPixel3D((global.x + pos.x) / 2, 0, (global.z + pos.z) / 2) * 20, pos.z);
            return newpos;
        }


        private float function(Vector3 pos, float elevation)
        {

            Vector3 step = new Vector3(1f / (sizeX), 1f / (sizeY), 1f / (sizeZ));
            step *= (1f / (1 << LOD_Level));
            step *= CELL_SIZE;
            float domainWarp = simplexNoise.CalcPixel3D(pos.x * 5, pos.y * 5, pos.z * 5) * 2f;
            float cave = simplexNoise.CalcPixel3D(pos.x * 2, pos.y * 2, pos.z * 2) * elevation / (sizeY * 0.5f);

            if (elevation >= sizeY - 1) { return -1; }
            if (elevation <= 1) { return 1; }
            //simplexNoise.setScale(0.005f + (elevation*step.y*0.1f));

            //pos = (pos - global).normalized * CELL_SIZE / 2 + global;
            float value = (1 - Mathf.Abs(simplexNoise.CalcPixel3D(pos.x + domainWarp, pos.y + domainWarp, pos.z + domainWarp))) * amplitude + Ground - elevation;
            //value = cave > 0.5f ? -1 : value;

            return value;
        }


        //returns the interpolation t value for points x0 and x1 where sign(x0) != sign(x1)
        private float adapt(float x0, float x1)
        {
            //Assert.IsTrue(x0 > 0 != x1 > 0);

            return (-x0) / (x1 - x0);
        }
    }
}
