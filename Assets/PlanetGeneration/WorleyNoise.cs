using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Xml.Schema;


namespace Worley {
    public class WorleyNoise {

        public int _seed;
        public byte[] _permutation;
        private bool inverse;

        private readonly byte[] PermOriginal = {
            151,160,137,91,90,15,
            131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
            190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
            88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
            77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
            102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
            135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
            5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
            223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
            129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
            251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
            49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
            138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180,
            151,160,137,91,90,15,
            131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
            190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
            88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
            77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
            102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
            135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
            5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
            223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
            129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
            251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
            49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
            138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
        };

        public int Seed {
            get => _seed; set {
                if (value == 0)
                {
                    _permutation = new byte[PermOriginal.Length];
                    PermOriginal.CopyTo(_permutation, 0);
                }
                else {
                    _permutation = new byte[512];
                    var random = new Random(value);
                    random.NextBytes(_permutation);
                }

                _seed = value;
            }

        }
        public WorleyNoise(bool inverse) {
            this.inverse = inverse;
            _permutation = new byte[PermOriginal.Length];
            PermOriginal.CopyTo(_permutation, 0);
        }

        public float Calculate(float x, float y, float z, float scale) {
            //the scale should make the 1x1x1 cube be smaller accordingly
            float maxDistance = 1.41f / scale;
            Vector3[] points = new Vector3[27];
            var random = new Random(Seed);


            //first figure out the 1x1x1 cube the point is situated in
            var xs = (float)(Math.Round(x * scale) / scale);
            var ys = (float)(Math.Round(y * scale) / scale);
            var zs = (float)(Math.Round(z * scale) / scale);

            //create the 27 cube kernel around the point
            for (int i = -1; i < 2; ++i) {
                for (int j = -1; j < 2; ++j)
                {
                    for (int k = -1; k < 2; ++k)
                    {
                        int index = ((i + 1) * 3 * 3) + ((j + 1) * 3) + (k + 1);//((3 * i) + j)* k; //this may be a wrong calculation
                        points[index] = new Vector3(xs + (i / scale), ys + (j / scale), zs + (k / scale));
                    }
                }
            }

            //NEED TO PROPERLY SHIFT THE POINTS BY AN APPROPRIATE VECTOR SUCH THAT 
            //IT IS REPLICABLE AND ENERGY EFFICIENT
            


            //HERE
            for (int i = 0; i < points.Length; ++i) {
                //for each point we want to offset it 
                //such that each input will always give the same output


                //NEED A DIFFERENT METHOD. THIS NO WORK


                int xx = (int)(points[i].X * 1000000) % _permutation.Length;
                int yy = (int)(points[i].Y * 1000000) % _permutation.Length;
                int zz = (int)(points[i].Z * 1000000) % _permutation.Length;

                Vector3 offset = new Vector3(
                    _permutation[xx] / 255f,
                    _permutation[(yy + _permutation[xx]) % _permutation.Length] / 255f,
                    _permutation[(zz + _permutation[(yy + _permutation[xx]) % _permutation.Length]) % _permutation.Length ] / 255f
                    );
                points[i] += (offset / scale);

            }
            ///////////////

            
            Vector3 position = new Vector3(x, y, z);
            //compare the point locations with the x,y,z and give a value between -1 and 1 based on it. or perhaps between 0 and 1?
            float closestDistance = 1;
            for (int i = 0; i < points.Length; ++i) {

                float distance = Vector3.Distance(points[i], position);
                if (distance < closestDistance) {
                    closestDistance = distance;
                    
                }

            }
            closestDistance = (closestDistance == 1 ? 1 : 1 - MathF.Pow(2, -10 * closestDistance));


            float val = closestDistance;
            //val *= multiplier;
            return inverse == true ? 1-val : val;
        }

        float sigmoidFunction(float x) {
            float steepness = 2;
            return 1 / (1 + MathF.Exp( (-x * steepness)+(steepness/2) ));
        }

        int getLeastSigDigits(float num)
        {
            return (byte)num & (byte)(-num);
        }
    }

    
}