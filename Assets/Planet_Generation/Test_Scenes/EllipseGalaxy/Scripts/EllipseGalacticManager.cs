using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using FloatingOrigin;

public class EllipseGalacticManager : MonoBehaviour
{

    [SerializeField] Floating_Origin_Transform floating_origin_transform;
    [SerializeField] Vector3 localScale;
    [SerializeField] ComputeShader stellarBodyComputeShader;

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

    //Noise Texture (will add to Noise Pipeline later)
    [SerializeField] private Texture3D noiseTexture;
    [SerializeField] private float frequency;
    [SerializeField] private float amplitude;
    [SerializeField] private float t;
    [SerializeField] private float starProcessionSpeed;
    [SerializeField] private float starSize;

    private Matrix4x4[] starMatrices;
    private ComputeBuffer starMatrixBuffer;
    private Matrix4x4[] nebulaMatrices;
    private ComputeBuffer nebulaMatrixBuffer;

    //GPU instance stars and modify positions via compute shader
    void Start()
    {

        starMatrices = new Matrix4x4[starLimit];
        starMatrixBuffer = new ComputeBuffer(starLimit, sizeof(float) * 16);

        nebulaMatrices = new Matrix4x4[nebulaLimit];
        nebulaMatrixBuffer = new ComputeBuffer(nebulaLimit, sizeof(float) * 16);

        LoadComputeShaderData();
    }



    // Update is called once per frame
    void Update()
    {
        if (starProcessionSpeed > 0)
        {
            t += Time.deltaTime * starProcessionSpeed;
            stellarBodyComputeShader.SetFloat("t", t);

            LoadStarData();
            int kernel = stellarBodyComputeShader.FindKernel("CSMain");
            int groupX = Mathf.CeilToInt(starLimit / 64.0f);
            stellarBodyComputeShader.Dispatch(kernel, groupX, 1, 1);

            starMatrixBuffer.GetData(starMatrices);

            LoadNebulaData();
            kernel = stellarBodyComputeShader.FindKernel("CSMain");
            groupX = Mathf.CeilToInt(nebulaLimit / 64.0f);
            stellarBodyComputeShader.Dispatch(kernel, groupX, 1, 1);

            nebulaMatrixBuffer.GetData(nebulaMatrices);

            stellarBodyComputeShader.SetMatrix("floating_origin_transform", floating_origin_transform.TRS);
        }

        floating_origin_transform = new Floating_Origin_Transform(Vector3.zero, Quaternion.identity, localScale);

        Graphics.RenderMeshInstanced(new RenderParams(starObj.instanceData.mat), starObj.instanceData.mesh, 0, starMatrices);
        Graphics.RenderMeshInstanced(new RenderParams(nebulaObj.instanceData.mat), nebulaObj.instanceData.mesh, 0, nebulaMatrices);
    }

    private void LoadStarData()
    {
        int kernel = stellarBodyComputeShader.FindKernel("CSMain");
        Quaternion starObjForward = Quaternion.Euler(starObj.instanceData.forward);

        stellarBodyComputeShader.SetBuffer(kernel, "matrices", starMatrixBuffer);

        //Star Body Data
        stellarBodyComputeShader.SetInt("starLimit", starLimit);
        stellarBodyComputeShader.SetVector("starObjForward", new Vector4(starObjForward.x, starObjForward.y, starObjForward.z, starObjForward.w));
        stellarBodyComputeShader.SetFloat("starObjSize", starObj.instanceData.size);
    }

    private void LoadNebulaData()
    {
        int kernel = stellarBodyComputeShader.FindKernel("CSMain");
        Quaternion nebulaObjForward = Quaternion.Euler(nebulaObj.instanceData.forward);

        stellarBodyComputeShader.SetBuffer(kernel, "matrices", nebulaMatrixBuffer);

        //Nebula Body Data
        stellarBodyComputeShader.SetInt("starLimit", nebulaLimit);
        stellarBodyComputeShader.SetVector("starObjForward", new Vector4(nebulaObjForward.x, nebulaObjForward.y, nebulaObjForward.z, nebulaObjForward.w));
        stellarBodyComputeShader.SetFloat("starObjSize", nebulaObj.instanceData.size);
    }

    private void LoadComputeShaderData()
    {
        int kernel = stellarBodyComputeShader.FindKernel("CSMain");

        stellarBodyComputeShader.SetMatrix("floating_origin_transform", floating_origin_transform.TRS);

        stellarBodyComputeShader.SetTexture(kernel, "NoiseTexture", noiseTexture);

        //Galaxy Data
        stellarBodyComputeShader.SetInt("numOrbits", numOrbits);
        stellarBodyComputeShader.SetFloat("processionTheta", processionTheta);
        stellarBodyComputeShader.SetFloat("galaxySize", galaxySize);
        stellarBodyComputeShader.SetVector("majorAxes", majorAxes);

        //Noise Modifiers
        stellarBodyComputeShader.SetFloat("frequency", frequency);
        stellarBodyComputeShader.SetFloat("amplitude", amplitude);

    }
}
