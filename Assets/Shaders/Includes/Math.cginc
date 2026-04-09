#ifndef MATH_CGINC
#define MATH_CGINC

static const float PI = 3.14159265359;
static const float TAU = PI * 2;
static const float maxFloat = 3.402823466e+38;

float2 raySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir) {
		float3 offset = rayOrigin - sphereCentre;
		float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
		float b = 2 * dot(offset, rayDir);
		float c = dot (offset, offset) - sphereRadius * sphereRadius;
		float d = b * b - 4 * a * c; // Discriminant from quadratic formula

		// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
		if (d > 0) {
			float s = sqrt(d);
			float dstToSphereNear = max(0, (-b - s) / (2 * a));
			float dstToSphereFar = (-b + s) / (2 * a);

			// Ignore intersections that occur behind the ray
			if (dstToSphereFar >= 0) {
				return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
			}
		}
		// Ray did not intersect sphere
		return float2(maxFloat, 0);
}

float2 raySpherePoint(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir) {
		float3 offset = rayOrigin - sphereCentre;
		float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
		float b = 2 * dot(offset, rayDir);
		float c = dot (offset, offset) - sphereRadius * sphereRadius;
		float d = b * b - 4 * a * c; // Discriminant from quadratic formula

		// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
		if (d > 0) {
			float s = sqrt(d);
			float dstToSphereNear = max(0, (-b - s) / (2 * a));
			float dstToSphereFar = (-b + s) / (2 * a);

			// Ignore intersections that occur behind the ray
			if (dstToSphereFar >= 0) {
				return float2(dstToSphereNear, dstToSphereFar);
			}
		}
		// Ray did not intersect sphere
		return float2(maxFloat, 0);
}

//Gives back hit information of a ray intersecting a sphere shell (sphere inside sphere that counts as negative space)
//shellThicknessRatio is the percent of the original radius that the inner shell radius has
float2 raySphereShell(float3 sphereCentre, float sphereRadius, float shellThicknessRatio, float3 rayOrigin, float3 rayDir){
	float2 hitOuter = raySpherePoint(sphereCentre, sphereRadius, rayOrigin, rayDir);
	float2 hitInner = raySpherePoint(sphereCentre, sphereRadius * shellThicknessRatio, rayOrigin, rayDir);

	if(hitOuter.x == maxFloat) return float2(maxFloat, 0); // No intersection with shell at all

    float distToShell = hitOuter.x;
    float distToShellEnd = hitOuter.y;

    if(hitInner.x != maxFloat) //if hit inner sphere
    {
        // If we start outside inner sphere → cut front
		if(hitInner.x > hitOuter.x)
        {
            distToShellEnd = min(hitOuter.y, hitInner.x);
        }
        else
        {
            // we are inside inner sphere → start at exit
            distToShell = hitInner.y;
            distToShellEnd = hitOuter.y;
        }
    }

    return float2(distToShell, distToShellEnd - distToShell);
}

float sinCustom(float x){
	//return the taylor expansion to second degree
	float a = x+PI/2.0, b = TAU;
    x = ((a>0)?a-b*((int)(a/b)):(-a+b*(((int)(a/b)))))-PI/2.0;
    if (x > PI/2.0)
        x = PI - x;
	//cut down x to range [0,2pi]

	float x3 = x*x*x;
	float x5 = x3*x*x;
	return x - x3/6 + x5/120;
}

float cosCustom(float x){
	return sinCustom(x + PI/2);
}


#endif