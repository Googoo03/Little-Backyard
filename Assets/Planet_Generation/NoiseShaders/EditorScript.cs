using ProceduralNoiseProject;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

//[CustomEditor(typeof(Wave))]
public class EditorScript : MonoBehaviour
{
    //Wave wave;
    int perlinNoiseHandle;
    int resolution = 32;
    public RenderTexture texture;
    Mesh mesh;
    ComputeShader perlinNoise;

    public int octaves = 1;
    public float frequency = 1;
    public float persistance = 0.5f;

    public float lacunarity = 2;
    private void Start()
    {

        texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true
        };
        texture.Create();

        perlinNoise = (ComputeShader)Resources.Load("Simplex Noise");

        //Get vertex data from mesh
        mesh = transform.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = new Vector3[mesh.vertices.Length];
        for (int i = 0; i < vertices.Length; ++i) {
            vertices[i] = transform.TransformPoint(mesh.vertices[i]);
        }

        //Create buffer to send to GPU
        ComputeBuffer verts = new ComputeBuffer(vertices.Length, sizeof(float) *3);
        verts.SetData(vertices);

        
        perlinNoiseHandle = perlinNoise.FindKernel("CSMain");
        perlinNoise.SetInt("seed", 0);
        //perlinNoise.SetFloat("_DisplacementStrength", 2);
        perlinNoise.SetTexture(perlinNoiseHandle, "Result", texture);
        perlinNoise.SetBuffer(perlinNoiseHandle, "vertexBuffer", verts);


        perlinNoise.Dispatch(perlinNoiseHandle, resolution/4, resolution/4, 1);
        transform.GetComponent<Renderer>().material.SetTexture("_HeightMap", texture);

        verts.Release();
    }

    /*private void Update()
    {
        Vector3[] vertices = new Vector3[mesh.vertices.Length];
        for (int i = 0; i < vertices.Length; ++i)
        {
            vertices[i] = transform.TransformPoint(mesh.vertices[i]);
        }

        ComputeBuffer verts = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        verts.SetData(vertices);

        perlinNoise.SetBuffer(perlinNoiseHandle, "vertexBuffer", verts);
        perlinNoise.SetFloat("octaves", octaves);
        perlinNoise.SetFloat("frequency", frequency);
        perlinNoise.SetFloat("persistance", persistance);
        perlinNoise.SetFloat("lacunarity", lacunarity);

        perlinNoise.Dispatch(perlinNoiseHandle, resolution / 4, resolution / 4, 1);
        transform.GetComponent<Renderer>().material.SetTexture("_HeightMap", texture);

        verts.Release();
    }*/

   /* public override void OnInspectorGUI()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.ObjectField("Texture", texture, typeof(Texture), false);
        EditorGUILayout.EndHorizontal();
    }*/
}