using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Poisson
{
    public class PoissonDisc
    {
        private uint startSeed;
        private const float _2PI = 6.28f;

        public void setSeedPRNG(int seed)
        {
            startSeed = (uint)seed;
        }

        uint PRNG()
        {
            //c is the increment, a is the factor, the seed is our starting factor, m is modulus
            uint a = 22695477;
            uint m = 256;
            uint c = 1;
            //if (startSeed == 0) startSeed = seed;
            uint val = ((startSeed * a) + c) % m;
            startSeed = val;
            return val;
        }

        private bool meetsDistanceThreshold(int radius, ref bool[] hashgrid,int startIndex,int maxX, int maxY) {
            int halfradius = radius / 2;
            for (int i = -halfradius; i < halfradius; ++i) {
                for (int j = -halfradius; j < halfradius; ++j)
                {
                    //do bounds check for x and y, if outside, skip

                    ////////////////////////
                    int index = startIndex + (maxX * j) + i;
                    if (index > (maxX * maxY)-1 || index < 0) continue;
                    if (!hashgrid[index])
                    {
                        continue;
                    }
                    else {
                        return false;
                    }
                }
            }
            return true;
        }

        //This assumes that the hashgrid is 3 dimensional
        private bool meetsDistanceThreshold3D(int radius, ref bool[] hashgrid, int startIndex, int maxX, int maxY)
        {
            int halfradius = radius / 2;
            for (int i = -halfradius; i < halfradius; ++i)
            {
                for (int j = -halfradius; j < halfradius; ++j)
                {
                    for (int k = -halfradius; k < halfradius; ++k)
                    {
                        //do bounds check for x and y and z, if outside, skip

                        ////////////////////////
                        int index = startIndex + (maxX*maxY*k)+(maxX * j) + i;
                        if (index > (maxX * maxY * maxX) - 1 || index < 0) continue; //skip anything that is out of bounds

                        if (hashgrid[index]) return false; //if you find anything in range, throw out

                    }
                }
            }
            return true;
        }

        public void generatePoissonDisc(ref List<Vector3> points, int k, int num, int maxX, int maxY, int radius) { // doesn't take in a reference vector field

            //start with empty list, grow as needed
            bool[] hashgrid = new bool[maxX*maxY];
            

            int index = 0; //current index of reference point
            int bool_index; //index of new point in bool list
            int points_placed = 0;

            float rand;
            float x, y;
            

            x = maxX / 2;
            y = maxY / 2;
            points.Add(new Vector3(x, 0, y));
            bool_index = ((int)y * maxX) + (int)x;
            hashgrid[bool_index] = true;

            while (index < points.Count && points_placed < num) { //while we havent placed enough points and havent reached the end of our array

                for (int i = 0; i < k; i++) {
                    //generate new random number from 0 to 2pi
                    rand = ((float)PRNG() / 256.0f) * _2PI;


                    

                    //figure out new point x,y position
                    x = Mathf.Max(points[index].x+(Mathf.Cos(rand)*radius),0);
                    y = Mathf.Max(points[index].z+(Mathf.Sin(rand)*radius),0);
                    x = Mathf.Min(x, maxX);
                    y = Mathf.Min(y, maxY-1);
                    //check if its valid, if so, add it, if not, skip it
                    //float dist = Mathf.Sqrt((x - points[index].x) * (x - points[index].x) + (y - points[index].y) * (y - points[index].y));
                    bool_index = ((int)Mathf.Max(y-1,0) *maxX) + (int)x;
                    if (meetsDistanceThreshold(radius,ref hashgrid,bool_index,maxX,maxY)) {
                        hashgrid[bool_index] = true;
                        Vector3 newpoint = new Vector3(x, 0, y);
                        points.Add(newpoint);
                        points_placed++;
                    }
                    //find out what hash grid it belongs to. If it's already true, then skip, otherwise set it true and add it to points

                }
                index++;
            }
            return;
        }

        private Vector3 interpolate(Vector3 a, Vector3 b, float t) {
            return a + (b - a)*t;
        }

        public void generatePoissonDisc(ref List<Vector3> points, ref Vector3[] vertices, int k, int num, int maxX, int maxY, int radius) //takes in a reference vector field
        { // doesn't take in a reference vector field

            //start with empty list, grow as needed
            bool[] hashgrid = new bool[maxX * maxY];
            


            int index = 0; //current index of reference point
            int bool_index; //index of new point in bool list
            int points_placed = 0;

            float rand;
            float x, y;
            float index_x, index_y;
            float next_x, next_y;

            bool found = false;

            x = maxX / 2;
            y = maxY / 2;
            next_x = x; next_y = y;

            index_x = next_x; index_y = next_y;

            /////CALCULATE FIRST POINT FROM CENTER BUT DONT ADD CENTER/////////////////
            rand = ((float)PRNG() / 256.0f) * _2PI;

            //figure out new point x,y position
            x = Mathf.Max(index_x + (Mathf.Cos(rand) * radius), 0);
            y = Mathf.Max(index_y + (Mathf.Sin(rand) * radius), 0);
            x = Mathf.Min(x, maxX);
            y = Mathf.Min(y, maxY - 1);
            ///////////////////////////////////

            bool_index = ((int)Mathf.Max(y - 1, 0) * maxX) + (int)x;
            points.Add(vertices[bool_index]);
            hashgrid[bool_index] = true;


            while (index < points.Count && points_placed < num)
            { //while we havent placed enough points and havent reached the end of our array
                found = false;
                for (int i = 0; i < k; i++)
                {
                    //generate new random number from 0 to 2pi
                    rand = ((float)PRNG() / 256.0f) * _2PI;




                    //figure out new point x,y position
                    x = Mathf.Max(index_x + (Mathf.Cos(rand) * radius), 0);
                    y = Mathf.Max(index_y + (Mathf.Sin(rand) * radius), 0);
                    x = Mathf.Min(x, maxX);
                    y = Mathf.Min(y, maxY - 1);

                    //check if its valid, if so, add it, if not, skip it
                    bool_index = ((int)Mathf.Max(y - 1, 0) * maxX) + (int)x;
                    int interpolate_index = ((int)Mathf.Max(y, 0) * maxX) + (int)(x+1);

                    if (meetsDistanceThreshold(radius, ref hashgrid, bool_index, maxX, maxY))
                    {
                        if (!found) {
                            next_x = x; next_y = y;
                            found = true;
                        }
                        hashgrid[bool_index] = true;
                        
                        Vector3 newpoint = interpolate_index < (maxX*maxY)-1 ? interpolate( vertices[bool_index], vertices[interpolate_index],x-(int)x) : vertices[bool_index];
                        points.Add(newpoint);
                        points_placed++;
                    }
                    //find out what hash grid it belongs to. If it's already true, then skip, otherwise set it true and add it to points

                }
                index++;
                index_x = next_x;
                index_y = next_y;
            }
            return;
        }

        public void generatePoissonDisc3DSphere(ref List<Vector3> points, int k, int num, int radius, int resolution) //takes in a reference vector field
        { // doesn't take in a reference vector field
            int max_theta = resolution;
            int max_phi = resolution;
            //start with empty list, grow as needed
            bool[] hashgrid = new bool[resolution*resolution*resolution];



            int index = 0; //current index of reference point
            int bool_index; //index of new point in bool list
            int points_placed = 0;

            float theta;
            float phi;

            float x, y, z;
            float index_theta, index_phi;
            float next_theta, next_phi;

            bool found = false;

            theta = 0;
            phi = 0;

            next_theta = theta; next_phi = phi;

            index_theta = next_theta; index_phi = next_phi;


            ////CALCULATE ON PHI THETA PLANE, MAP TO SPHERE

            /////CALCULATE FIRST POINT FROM CENTER BUT DONT ADD CENTER/////////////////
            theta = ((float)PRNG() / 256.0f) * _2PI; //0 - 2pi float
            phi = ((float)PRNG() / 256.0f) * _2PI;


            //figure out new point x,y position
            x = Mathf.Sin(phi) * Mathf.Cos(theta);
            y = Mathf.Sin(phi) * Mathf.Sin(theta);
            z = Mathf.Cos(phi);
            ///////////////////////////////////
            ///

            //Find neighborhood of points to determine distance

            //since x,y,z = -1 to 1 we need to shift so that x,y,z are in range 0 to resolution-1
            int boolX = (int)((x + 1) * ((resolution - 1) * 0.5f));
            int boolY = (int)((y + 1) * ((resolution - 1) * 0.5f));
            int boolZ = (int)((z + 1) * ((resolution - 1) * 0.5f));
            //x + y*WIDTH + Z*WIDTH*DEPTH
            bool_index = boolX + (boolY*resolution) + (boolZ*resolution*resolution);
            points.Add(new Vector3(x,y,z));
            hashgrid[bool_index] = true;


            while (index < points.Count && points_placed < num)
            { //while we havent placed enough points and havent reached the end of our array
                found = false;
                for (int i = 0; i < k; i++)
                {
                    //generate new random number from 0 to 2pi
                    //rand_theta = ((int)PRNG() / resolution);
                    //rand_phi = ((int)PRNG() / resolution);

                    //calculate new theta, phi position
                    theta = ((float)PRNG() / 256.0f) * _2PI; //0 - 2pi float
                    phi = ((float)PRNG() / 256.0f) * _2PI;



                    //figure out new point x,y,z position
                    x = Mathf.Sin(phi) * Mathf.Cos(theta);
                    y = Mathf.Sin(phi) * Mathf.Sin(theta);
                    z = Mathf.Cos(phi);

                    boolX = (int)((x + 1) * ((resolution - 1) * 0.5f));
                    boolY = (int)((y + 1) * ((resolution - 1) * 0.5f));
                    boolZ = (int)((z + 1) * ((resolution - 1) * 0.5f));

                    //check if its valid, if so, add it, if not, skip it
                    bool_index = boolX + (boolY * resolution) + (boolZ * resolution * resolution);

                    ///ADD DISTANCE THRESHOLD LATER
                    ///
                    if (meetsDistanceThreshold3D(radius, ref hashgrid, bool_index, max_phi, max_theta))
                    {
                        if (!found)
                        {
                            next_theta = theta; next_phi = phi;
                            found = true;
                        }
                        hashgrid[bool_index] = true;

                        Vector3 newpoint = new Vector3(x, y, z);
                        points.Add(newpoint);
                        points_placed++;
                    }

                    //find out what hash grid it belongs to. If it's already true, then skip, otherwise set it true and add it to points

                }
                index++;
                index_theta = next_theta;
                index_phi = next_phi;
            }
            return;
        }

    }
}
