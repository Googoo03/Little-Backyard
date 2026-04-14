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

    private GalacticSpatialHashing galaxySpatialHash;
    [SerializeField] private int hashGridSize;

    //GPU instance stars and modify positions via compute shader
    void Start()
    {
        galaxySpatialHash = new GalacticSpatialHashing(hashGridSize);

        starMatrices = new Matrix4x4[starLimit];
        starMatrixBuffer = new ComputeBuffer(starLimit, sizeof(float) * 16);
        starMatrixFOBuffer = new ComputeBuffer(starLimit, sizeof(float) * 16);

        nebulaMatrices = new Matrix4x4[nebulaLimit];
        nebulaMatrixBuffer = new ComputeBuffer(nebulaLimit, sizeof(float) * 16);
        nebulaMatrixFOBuffer = new ComputeBuffer(nebulaLimit, sizeof(float) * 16);

        LoadComputeShaderData();

        GenerateGalaxy();
        galaxySpatialHash.GenerateGalaxyHash(starMatrices);
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
        else if (!galaxySpatialHash.generatedHash)
        {
            galaxySpatialHash.generatedHash = true;
            galaxySpatialHash.GenerateGalaxyHash(starMatrices);
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
        Vector3 FOSectorPos = new Vector3(Mathf.FloorToInt(floating_origin_transform.TRS.GetColumn(3).x / hashGridSize),
        Mathf.FloorToInt(floating_origin_transform.TRS.GetColumn(3).y / hashGridSize),
        Mathf.FloorToInt(floating_origin_transform.TRS.GetColumn(3).z / hashGridSize));
        Vector3 floating_origin_pos = floating_origin_transform.TRS.GetColumn(3);

        Vector3 floatingOriginSectorPos = FOSectorPos;
        floatingOriginSectorPos *= hashGridSize;

        Gizmos.color = Color.magenta;
        Vector3 center = floatingOriginSectorPos + (hashGridSize * 0.5f * Vector3.one);
        Gizmos.DrawWireCube(-center + floating_origin_pos, hashGridSize * Vector3.one);





        //Draw a cube around each star in the sector. Draw a wireframe cube to see where the sector is in space
        Matrix4x4 closestStar = galaxySpatialHash.FindClosestStar(floating_origin_pos);
        if (closestStar != Matrix4x4.identity)
        {
            Gizmos.DrawLine(Vector3.zero, (Vector3)closestStar.GetColumn(3) + floating_origin_pos);
        }
        else
        {
            Debug.Log("No Stars Found in Sector");
        }
        List<Matrix4x4> starPositions = galaxySpatialHash?.GetStarPositionsInSector(FOSectorPos);
        if (starPositions == null) return;
        foreach (Matrix4x4 star in starPositions)
        {
            Gizmos.DrawCube((Vector3)star.GetColumn(3) + floating_origin_pos, galaxySize * starObj.instanceData.size * Vector3.one / 2);

        }
    }
}
