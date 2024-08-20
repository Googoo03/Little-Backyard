using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Poisson;
using System;

public class GalacticBox : MonoBehaviour
{
    [SerializeField] private float _size;
    [SerializeField] private PoissonDisc starGenerator;

    [SerializeField] private Event_Manager_Script event_manager;

    [SerializeField] private GameObject star;
    [SerializeField] private List<GameObject> stars;
    [SerializeField] private List<Vector3> starPositions;
    [SerializeField] private Vector3 galacticPosition;
    [SerializeField] private UInt64 seed;
    private bool _generate = false;

    //for testing purposes
    [SerializeField] private Color _boundingBoxColor;
    void OnDrawGizmos()
    {
        // Draw a semitransparent red cube at the transforms position
        Gizmos.color = _boundingBoxColor;
        Gizmos.DrawCube(transform.position, Vector3.one * _size);
    }

    public void setEventManager(Event_Manager_Script e_m) { event_manager = e_m; }

    public void setSize(float size) { _size = size; }

    public void setColor(Color col) { _boundingBoxColor = col; }

    public void setGenerate(bool gen) { _generate = gen; }

    public void setGalacticPosition(Vector3 newpos) { galacticPosition = newpos; }

    public Vector3 getGalacticPosition() { return galacticPosition; }

    private void Start()
    {
        ////Initializing Star Generation
        starGenerator = new PoissonDisc();
        starPositions = new List<Vector3>();
        stars = new List<GameObject> ();
        _generate = false;

        generateStarPositions();

        //It would be a good idea to have the stars be under the control of their respective boxes. So whenever
        //it generates, it already knows which stars to reference.

        //Creates the pool of stars for the given box at the start.
        for (int i = 0; i < starPositions.Count; ++i) { //this will be replaced with object pooling
            GameObject instantiatedStar = Instantiate(star, starPositions[i]*(_size/2f) + transform.position, Quaternion.identity);
            instantiatedStar.transform.parent = transform;
            instantiatedStar.GetComponent<SolarSystemGeneration>().setEventManager(event_manager);
            stars.Add(instantiatedStar);
        }
    }

    private void Update()
    {
        if (_generate) { 
            generateStarPositions();
            for (int i = 0; i < stars.Count; ++i) {
                if (i < starPositions.Count)
                {
                    stars[i].SetActive(true);
                    stars[i].transform.position = starPositions[i] * (_size / 2f) + transform.position;
                }
                else {
                    stars[i].SetActive(false);
                }
                
            }
        }
    }

    private void generateStarPositions() {
        _generate = false;
        starPositions.Clear();
        //Reset seed. Needs to take into account the galactic seed, its galactic position, and anything else.
        var hash = new Hash128();
        hash.Append(galacticPosition.x);
        hash.Append(galacticPosition.y);
        hash.Append(galacticPosition.z);

        seed = (UInt64)hash.GetHashCode();

        starGenerator.setSeedPRNG((int)seed);

        //Generate new stars with the new seed
        starGenerator.generatePoissonDisc3DSphere(ref starPositions, 5, 40, 16, 64);
    }


}
