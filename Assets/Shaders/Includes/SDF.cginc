#ifndef SDF_CGINC
#define SDF_CGINC

float Sphere(float3 p, float3 center, float r = 5) { return length(p - center) - r; }



#endif