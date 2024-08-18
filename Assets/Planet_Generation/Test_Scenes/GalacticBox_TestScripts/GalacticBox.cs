using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GalacticBox : MonoBehaviour
{
    [SerializeField] private float _size;

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


}
