using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser_Class : MonoBehaviour
{
    // Start is called before the first frame update
    private float t;
    private bool startAnim;
    private bool endAnim;

    public void setStartAnim(bool start) { startAnim = start; }
}
