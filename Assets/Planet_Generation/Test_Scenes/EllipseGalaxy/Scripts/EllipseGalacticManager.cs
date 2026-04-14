using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using FloatingOrigin;
using System;

public class EllipseGalacticManager : Manager
{
    [SerializeField] ComputeShader stellarBodyComputeShader;

    //Star limit
    [SerializeField] private int starLimit;
    [SerializeField] private int nebulaLimit;


    [SerializeField] StarScriptableObj starObj;
    [SerializeField] StarScriptableObj nebulaObj;

    //Procession modifier, galaxy size
    [Range(0, 0.5f)]
    [SerializeField] private float processionTheta;

    [Range(1, 100)]
    [SerializeField] private int numOrbits;
    [SerializeField] private Vector2 majorAxes;
    [SerializeField] private float galaxySize;

    //Noise Texture (will add to Noise Pipeline later)
    [SerializeField] private Texture3D noiseTexture;
    [SerializeField] private float frequency;
    [SerializeField] private float amplitude;
    [SerializeField] private float starProcessionSpeed;

    private float t;
    private Matrix4x4[] starMatrices;
    private ComputeBuffer starMatrixBuffer;
    private ComputeBuffer starMatrixFOBuffer;
    private Matrix4x4[] nebulaMatrices;
    private ComputeBuffer nebulaMatrixBuffer;
    private ComputeBuffer nebulaMatrixFOBuffer;

    private Dictionary<Vector3, List<Matrix4x4>> galaxyHash = new();
    private bool generatedHash = false;
    [SerializeField] private int hashGridSize;
    [SerializeField] private Vector3 neighborIndex;

    //GPU instance stars and modify positions via compute shader
    void Start()
    {

        starMatrices = new Matrix4x4[starLimit];
        starMatrixBuffer = new ComputeBuffer(starLimit, sizeof(float) * 16);
        starMatrixFOBuffer = new ComputeBuffer(starLimit, sizeof(float) * 16);

        nebulaMatrices = new Matrix4x4[nebulaLimit];
        nebulaMatrixBuffer = new ComputeBuffer(nebulaLimit, sizeof(float) * 16);
        nebulaMatrixFOBuffer = new ComputeBuffer(nebulaLimit, sizeof(float) * 16);

        LoadComputeShaderData();

        GenerateGalaxy();
        GenerateGalaxyHash();
    }

    void OnDestroy()
    {
        if (starMatrixBuffer != null)
        {
            starMatrixBuffer.Release();
            starMatrixBuffer = null;
        }
        if (starMatrixFOBuffer != null)
        {
            starMatrixFOBuffer.Release();
            starMatrixFOBuffer = null;
        }
        if (nebulaMatrixBuffer != null)
        {
            nebulaMatrixBuffer.Release();
            nebulaMatrixBuffer = null;
        }
        if (nebulaMatrixFOBuffer != null)
        {
            nebulaMatrixFOBuffer.Release();
            nebulaMatrixFOBuffer = null;
        }
    }



    // Update is called once per frame
    void Update()
    {
        if (starProcessionSpeed > 0)
        {
            GenerateGalaxy();
        }
        else if (!generatedHash)
        {
            generatedHash = true;
            GenerateGalaxyHash();
        }

        Floating_Origin_Manager.Instance.DispatchFloatingOriginShader(starMatrixBuffer, starMatrixFOBuffer, starMatrices, this);
        Floating_Origin_Manager.Instance.DispatchFloatingOriginShader(nebulaMatrixBuffer, nebulaMatrixFOBuffer, nebulaMatrices, this);

        Graphics.RenderMeshInstanced(new RenderParams(starObj.instanceData.mat), starObj.instanceData.mesh, 0, starMatrices);
        Graphics.RenderMeshInstanced(new RenderParams(nebulaObj.instanceData.mat), nebulaObj.instanceData.mesh, 0, nebulaMatrices);
    }



    private void GenerateGalaxy()
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

    private void GenerateGalaxyHash()
    {
        galaxyHash.Clear();
        foreach (Matrix4x4 star in starMatrices)
        {
            Vector3 starPos = star.GetColumn(3);
            Vector3 key = new Vector3(Mathf.Floor(starPos.x / hashGridSize), Mathf.Floor(starPos.y / hashGridSize), Mathf.Floor(starPos.z / hashGridSize));

            if (!galaxyHash.TryGetValue(key, out var list))
            {
                list = new List<Matrix4x4>();
                galaxyHash[key] = list;
            }

            galaxyHash[key].Add(star);
        }
    }

    private void LoadStarData()
    {
        int kernel = stellarBodyComputeShader.FindKernel("CSMain");
        Quaternion starObjForward = Quaternion.Euler(starObj.instanceData.forward);

        stellarBodyComputeShader.SetBuffer(kernel, "matrices", starMatrixBuffer);

        //Star Body Data
        stellarBodyComputeShader.SetBool("billboard", false);
        stellarBodyComputeShader.SetInt("starLimit", starLimit);
        stellarBodyComputeShader.SetVector("starObjForward", new Vector4(starObjForward.x, starObjForward.y, starObjForward.z, starObjForward.w));
        stellarBodyComputeShader.SetFloat("starObjSize", starObj.instanceData.size * galaxySize / 10);
    }

    private void LoadNebulaData()
    {
        int kernel = stellarBodyComputeShader.FindKernel("CSMain");
        Quaternion nebulaObjForward = Quaternion.Euler(nebulaObj.instanceData.forward);

        stellarBodyComputeShader.SetBuffer(kernel, "matrices", nebulaMatrixBuffer);

        stellarBodyComputeShader.SetVector("floating_origin_position", floating_origin_transform.TRS.GetColumn(3));

        //Nebula Body Data
        stellarBodyComputeShader.SetBool("billboard", true);
        stellarBodyComputeShader.SetInt("starLimit", nebulaLimit);
        stellarBodyComputeShader.SetFloat("starObjSize", nebulaObj.instanceData.size * galaxySize / 10);
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
        stellarBodyComputeShader.SetFloat("amplitude", amplitude * galaxySize / 10);

    }

    //Test Function to debug the hashmap system
    public void OnDrawGizmos()
    {
        Vector3 floatingOriginSectorPos = new Vector3(Mathf.FloorToInt(floating_origin_transform.TRS.GetColumn(3).x / hashGridSize),
        Mathf.FloorToInt(floating_origin_transform.TRS.GetColumn(3).y / hashGridSize),
        Mathf.FloorToInt(floating_origin_transform.TRS.GetColumn(3).z / hashGridSize));

        Vector3 key = -(floatingOriginSectorPos + Vector3.one);

        floatingOriginSectorPos += Vector3.one;
        floatingOriginSectorPos *= hashGridSize;


        Vector3 floating_origin_pos = floating_origin_transform.TRS.GetColumn(3);

        if (!galaxyHash.ContainsKey(key)) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(-floatingOriginSectorPos + floating_origin_pos + (hashGridSize * Vector3.one / 2), hashGridSize * Vector3.one);
        foreach (Matrix4x4 star in galaxyHash[key])
        {
            Gizmos.DrawCube((Vector3)star.GetColumn(3) + floating_origin_pos, galaxySize * starObj.instanceData.size * Vector3.one / 2);
        }
    }
}
