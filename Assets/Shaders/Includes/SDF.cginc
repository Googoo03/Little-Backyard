#ifndef SDF_CGINC
#define SDF_CGINC

float Sphere(float3 p, float3 center, float r = 5) { return length(p - center) - r; }

struct SDF{
    int id;
    int invert;
    float3 center;
    float3 args; //for now, assume all basic SDFs will have only 3 args max

};

float EvaluateSDF(float3 p, SDF sdf){
        float value;
        switch(sdf.id){
            case 0:
                value = Sphere(p,sdf.center,sdf.args.x);
                break;
            default:
                value = Sphere(p,sdf.center);
                break;
        }
        return value;
    }

#endif