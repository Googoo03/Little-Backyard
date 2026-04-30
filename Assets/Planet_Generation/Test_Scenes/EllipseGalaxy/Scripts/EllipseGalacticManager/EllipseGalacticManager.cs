using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using FloatingOrigin;
using System;
using Unity.Collections;

public class EllipseGalacticManager : Manager
{
    [SerializeField] ComputeShader stellarBodyComputeShader;

    //Star limit
    [SerializeField] private int starLimit;
    [SerializeField] private int nebulaLimit;


    [SerializeField] public StarScriptableObj starObj;
    [SerializeField] public StarScriptableObj nebulaObj;

    //Procession modifier, galaxy size
    [Range(0, 0.5f)]
    [SerializeField] private float processionTheta;

    [Range(1, 100)]
    [SerializeField] private int numOrbits;
    [SerializeField] private Vector2 majorAxes;
    [SerializeField] public float galaxySize;

    //Noise Texture (will add to Noise Pipeline later)
    [SerializeField] private Texture3D noiseTexture;
    [SerializeField] private float frequency;
    [SerializeField] private float amplitude;
    [SerializeField] private float starProcessionSpeed;

    private float t;
    private NativeArray<Matrix4x4> starMatrices;
    private ComputeBuffer starMatrixBuffer;
    private ComputeBuffer starMatrixFOBuffer;
    private NativeArray<Matrix4x4> nebulaMatrices;
    private ComputeBuffer nebulaMatrixBuffer;
    private ComputeBuffer nebulaMatrixFOBuffer;
    private ComputeBuffer StarArgsBuffer;
    private ComputeBuffer NebulaArgsBuffer;
    private uint[] starArgs;
    private uint[] nebulaArgs;

    private GalacticSpatialHashing galaxySpatialHash;
    [SerializeField] private int hashGridSize;

    private object lockObj = new();
    public static EllipseGalacticManager Instance { get; private set; }

    //GPU instance stars and modify positions via compute shader
    void Start()
    {
        Instance = this;
        galaxySpatialHash = new GalacticSpatialHashing(hashGridSize);

        starMatrices = new NativeArray<Matrix4x4>(starLimit, Allocator.Persistent);
        starMatrixBuffer = new ComputeBuffer(starLimit, sizeof(float) * 16);
        starMatrixFOBuffer = new ComputeBuffer(starLimit, sizeof(float) * 16);

        nebulaMatrices = new NativeArray<Matrix4x4>(nebulaLimit, Allocator.Persistent);
        nebulaMatrixBuffer = new ComputeBuffer(nebulaLimit, sizeof(float) * 16);
        nebulaMatrixFOBuffer = new ComputeBuffer(nebulaLimit, sizeof(float) * 16);

        starArgs = new uint[5] {
        starObj.instanceData.mesh.GetIndexCount(0),
        (uint)starLimit, // start at 0 → compute shader will fill this
        starObj.instanceData.mesh.GetIndexStart(0),
        starObj.instanceData.mesh.GetBaseVertex(0),
        0
        };
        nebulaArgs = new uint[5] {
        nebulaObj.instanceData.mesh.GetIndexCount(0),
        (uint)nebulaLimit, // start at 0 → compute shader will fill this
        nebulaObj.instanceData.mesh.GetIndexStart(0),
        nebulaObj.instanceData.mesh.GetBaseVertex(0),
        0
        };
        StarArgsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        NebulaArgsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

        StarArgsBuffer.SetData(starArgs);
        NebulaArgsBuffer.SetData(nebulaArgs);

        LoadComputeShaderData();

        GenerateGalaxy();
    }



    // Update is called once per frame
    void Update()
    {
        LoadComputeShaderData();
        if (starProcessionSpeed > 0)
        {
            GenerateGalaxy();
        }



        Floating_Origin_Manager.Instance.DispatchFloatingOriginShader(starMatrixBuffer, starMatrixFOBuffer, this, starLimit);
        Floating_Origin_Manager.Instance.DispatchFloatingOriginShader(nebulaMatrixBuffer, nebulaMatrixFOBuffer, this, nebulaLimit);

        starObj.instanceData.mat.SetBuffer("_Matrices", starMatrixFOBuffer);


        Graphics.DrawMeshInstancedIndirect(
            starObj.instanceData.mesh,
            0,
            starObj.instanceData.mat,
            new Bounds(Vector3.zero, Vector3.one * 100000000f),
            StarArgsBuffer
        );

        nebulaObj.instanceData.mat.SetBuffer("_Matrices", nebulaMatrixFOBuffer);

        Graphics.DrawMeshInstancedIndirect(
            nebulaObj.instanceData.mesh,
            0,
            nebulaObj.instanceData.mat,
            new Bounds(Vector3.zero, Vector3.one * 100000000f),
            NebulaArgsBuffer
        );


        //Graphics.RenderMeshInstanced(new RenderParams(starObj.instanceData.mat), starObj.instanceData.mesh, 0, starMatrices);
        //Graphics.RenderMeshInstanced(new RenderParams(nebulaObj.instanceData.mat), nebulaObj.instanceData.mesh, 0, nebulaMatrices);
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

    private void GenerateGalaxy()
    {
        t += Time.deltaTime * starProcessionSpeed;
        stellarBodyComputeShader.SetFloat("t", t);



        LoadStarData();
        int kernel = stellarBodyComputeShader.FindKernel("CSMain");
        int groupX = Mathf.CeilToInt(starLimit / 64.0f);
        stellarBodyComputeShader.Dispatch(kernel, groupX, 1, 1);

        AsyncGPUReadback.Request(starMatrixBuffer, (request) =>
        {
            if (request.hasError)
            {
                Debug.LogError("GPU readback error");
                return;
            }


            var temp = request.GetData<Matrix4x4>();
            lock (lockObj)
            {
                starMatrices.CopyFrom(temp);
                if (!galaxySpatialHash.generatedHash)
                {
                    galaxySpatialHash.generatedHash = true;
                    galaxySpatialHash.GenerateGalaxyHash(starMatrices);
                }
            }
        });

        LoadNebulaData();
        kernel = stellarBodyComputeShader.FindKernel("CSMain");
        groupX = Mathf.CeilToInt(nebulaLimit / 64.0f);
        stellarBodyComputeShader.Dispatch(kernel, groupX, 1, 1);

        AsyncGPUReadback.Request(nebulaMatrixBuffer, (request) =>
        {
            if (request.hasError)
            {
                Debug.LogError("GPU readback error");
                return;
            }

            var temp = request.GetData<Matrix4x4>();
            lock (lockObj)
            {
                nebulaMatrices.CopyFrom(temp);
            }
        });

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
        stellarBodyComputeShader.SetFloat("starObjSize", starObj.instanceData.size * galaxySize);
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
        stellarBodyComputeShader.SetFloat("starObjSize", nebulaObj.instanceData.size * galaxySize);
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
        stellarBodyComputeShader.SetFloat("frequency", frequency / (galaxySize / 10));
        stellarBodyComputeShader.SetFloat("amplitude", amplitude * (galaxySize / 10));

    }

    //Returns identity matrix if nothing found, otherwise returns the matrix of the star
    public Matrix4x4 GetClosestStar()
    {
        return galaxySpatialHash.FindClosestStar(floating_origin_transform.TRS.GetColumn(3));
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
        Matrix4x4 closestStar = galaxySpatialHash != null ? galaxySpatialHash.FindClosestStar(floating_origin_pos) : Matrix4x4.identity;
        if (closestStar != Matrix4x4.identity)
        {
            Gizmos.DrawLine(Vector3.zero, (Vector3)closestStar.GetColumn(3) + floating_origin_pos);
        }
    }
}
