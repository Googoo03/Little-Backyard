using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Poisson;

public class Skybox_Generator : MonoBehaviour
{
    PoissonDisc poisson = new PoissonDisc();
    [SerializeField] private Mesh mesh_plane;
    [SerializeField] private Material star_mat;
    [SerializeField] private int seed;
    [SerializeField] private int scaleFactor;
    private List<Matrix4x4> star_matrix = new List<Matrix4x4>(1);
    private List<Vector3> points = new List<Vector3>();

    void Start()
    {
        mesh_plane = (Resources.Load<GameObject>("Skybox/Star").GetComponent<MeshFilter>().sharedMesh);
        
        poisson.generatePoissonDisc3DSphere(ref points, 15, 300, 1, 64);
        for (int i = 0; i < points.Count; ++i) {

            Vector3 lookVec = points[i];
            Quaternion rot = Quaternion.LookRotation(-lookVec);
            Vector3 sca = Vector3.one * .01f;
            star_matrix.Add(Matrix4x4.TRS(points[i] + transform.position, rot, sca)); //transform rotation scale
        }
    }

    private void updatePositions() {
        Vector3 sca = Vector3.one * .01f;
        for (int i = 0; i < star_matrix.Count; ++i) {
            Vector3 lookVec = points[i];
            Quaternion rot = Quaternion.LookRotation(-lookVec);
            
            star_matrix[i] = Matrix4x4.TRS(points[i]*scaleFactor + transform.position, rot, sca*scaleFactor); //transform rotation scale
        }
    }

    private void Update()
    {
        //updatePositions();
        //Graphics.DrawMeshInstanced(mesh_plane, 0, star_mat, star_matrix);
    }
}
