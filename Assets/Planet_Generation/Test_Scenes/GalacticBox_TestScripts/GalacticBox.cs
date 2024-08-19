using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Poisson;

public class GalacticBox : MonoBehaviour
{
    [SerializeField] private float _size;
    [SerializeField] private PoissonDisc starGenerator;

    [SerializeField] private GameObject star;
    [SerializeField] private List<GameObject> stars;
    [SerializeField] private List<Vector3> starPositions;
    private bool _generate;

    //for testing purposes
    [SerializeField] private Color _boundingBoxColor;
    void OnDrawGizmos()
    {
        // Draw a semitransparent red cube at the transforms position
        Gizmos.color = _boundingBoxColor;
        Gizmos.DrawCube(transform.position, Vector3.one * _size);
    }

    public void setSize(float size) { _size = size; }

    public void setColor(Color col) { _boundingBoxColor = col; }

    public void setGenerate(bool gen) { _generate = gen; }

    private void Start()
    {
        ////Initializing Star Generation
        starGenerator = new PoissonDisc();
        starPositions = new List<Vector3>();

        generateStarPositions();

        //It would be a good idea to have the stars be under the control of their respective boxes. So whenever
        //it generates, it already knows which stars to reference.

        //Creates the pool of stars for the given box at the start.
        for (int i = 0; i < starPositions.Count; ++i) { //this will be replaced with object pooling
            GameObject instantiatedStar = Instantiate(star, starPositions[i]*(_size/2f) + transform.position, Quaternion.identity);
            instantiatedStar.transform.parent = transform;
            stars.Add(instantiatedStar);
        }
    }

    private void Update()
    {
        if (_generate) { 
            generateStarPositions();
            for (int i = 0; i < starPositions.Count; ++i) {
                stars[i].transform.position = starPositions[i] * (_size / 2f) + transform.position;
            }
        }
    }

    private void generateStarPositions() {
        _generate = false;
        starPositions.Clear();
        //Reset seed. Needs to take into account the galactic seed, its galactic position, and anything else.
        starGenerator.setSeedPRNG(Random.Range(0, 10000));

        //Generate new stars with the new seed
        starGenerator.generatePoissonDisc3DSphere(ref starPositions, 5, 10, (int)(_size / 2f), 64);
    }


}
