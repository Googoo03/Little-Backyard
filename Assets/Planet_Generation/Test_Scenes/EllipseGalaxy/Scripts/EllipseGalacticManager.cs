using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class EllipseGalacticManager : MonoBehaviour
{
    // Start is called before the first frame update

    //Star limit
    [SerializeField] private int starLimit;
    [SerializeField] private int nebulaLimit;
    [Range(1, 100)]
    [SerializeField] private int numOrbits;
    [SerializeField] StarScriptableObj starObj;
    [SerializeField] StarScriptableObj nebulaObj;

    //Procession modifier, galaxy size
    [Range(0, 0.5f)]
    [SerializeField] private float processionTheta;
    [SerializeField] private Vector2 majorAxes;
    [SerializeField] private float galaxySize;
    [SerializeField] private Vector3 galaxyCenter;

    //Noise Texture (will add to Noise Pipeline later)
    [SerializeField] private Texture3D noiseTexture;
    [SerializeField] private float frequency;
    [SerializeField] private float amplitude;
    [SerializeField] private float t;
    [SerializeField] private float starProcessionSpeed;
    [SerializeField] private float starSize;

    private Matrix4x4[] starMatrices;
    private Matrix4x4[] nebulaMatrices;

    //GPU instance stars and modify positions via compute shader
    void Start()
    {
        starMatrices = new Matrix4x4[starLimit];
        nebulaMatrices = new Matrix4x4[nebulaLimit];
    }

    // Update is called once per frame
    void Update()
    {
        t += Time.deltaTime * starProcessionSpeed;
        GenerateStellarBody(starObj, starLimit, ref starMatrices);
        GenerateStellarBody(nebulaObj, nebulaLimit, ref nebulaMatrices);
        starObj.instanceData.mat.enableInstancing = true;
        Graphics.RenderMeshInstanced(new RenderParams(starObj.instanceData.mat), starObj.instanceData.mesh, 0, starMatrices);
        Graphics.RenderMeshInstanced(new RenderParams(nebulaObj.instanceData.mat), nebulaObj.instanceData.mesh, 0, nebulaMatrices);
    }

    private void GenerateStellarBody(StarScriptableObj starObj, int starLimit, ref Matrix4x4[] matrices)
    {
        int starsPerOrbit = starLimit / numOrbits;
        float sizePerOrbit = galaxySize / numOrbits;
        float procession = processionTheta;
        float size = sizePerOrbit;

        Vector3 starPos;

        //need elliptical model
        for (int j = 0; j < numOrbits; ++j)
        {
            for (int i = 0; i < starsPerOrbit; ++i)
            {
                float angle = 2 * 3.14f * ((float)i / (float)starsPerOrbit);
                Vector3 originalPos = CalculateStarPosition(galaxyCenter, procession, angle, majorAxes.x * size, majorAxes.y * size);
                starPos = CalculateStarPosition(galaxyCenter, procession, angle - t, majorAxes.x * size, majorAxes.y * size) + GetNoiseModifier(originalPos);

                matrices[j * (starsPerOrbit) + i] = Matrix4x4.TRS(starPos, Quaternion.Euler(starObj.instanceData.forward), Vector3.one * starObj.instanceData.size);

            }
            size += sizePerOrbit;
            procession += processionTheta;
        }

    }

    private Vector3 GetNoiseModifier(Vector3 position)
    {
        position /= noiseTexture.width; //normalize to texture
        Color val = (noiseTexture.GetPixelBilinear(position.x * frequency, position.y * frequency, position.z * frequency) - (Color.white / 2f)) * amplitude;
        Vector3 displacementVector = new Vector3(val.r, val.g, val.b);
        displacementVector -= (Vector3.one / 2f) * amplitude;
        return displacementVector;
    }

    private Vector3 CalculateStarPosition(Vector3 position, float theta, float t, float a, float b)
    {
        //Theta is procession value

        //t is radians

        float sint = Mathf.Sin(t);
        float cost = Mathf.Cos(t);
        float sintheta = Mathf.Sin(theta);
        float costheta = Mathf.Cos(theta);

        return new Vector3((a * cost * costheta) - (b * sintheta * sint), 0, (a * sintheta * cost) + (b * costheta * sint));
    }
}
