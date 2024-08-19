using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

public class GalacticManager : MonoBehaviour
{
    //For initializing the galactic boxes
    [SerializeField] private GameObject galacticBox;
    [SerializeField] private float boxScale;

    [SerializeField] private Camera _playerCamera;
    //[SerializeField] private int shiftDirection;
    //[SerializeField] private Vector3 _galacticOrigin;
    [SerializeField] private List<GameObject> boxes;


    void Start()
    {
        InitializeGalacticBoxes();
    }

    // Update is called once per frame
    void Update()
    {
        if (!inBox())
        {
            //reset the player to opposite side
            int direction =resetPlayerPos();
            Assert.IsTrue(Mathf.Abs(direction) < 4);

            //reset the boxes to make it seemless
            shiftBoxes(direction);
        }
    }



    void InitializeGalacticBoxes() {
        //_galacticOrigin = -(Vector3.one * boxScale / 2f);
        //the -1 and 2 make sure the center box is bounded at origin
        for (int i = -1; i < 2; ++i)
        {
            for (int j = -1; j < 2; ++j)
            {
                for (int k = -1; k < 2; ++k)
                {
                    Vector3 boxPosition = new Vector3(i, j, k) * boxScale;
                    GameObject box = Instantiate(galacticBox, boxPosition/*- (Vector3.one * boxScale/2f)*/, Quaternion.identity);

                    box.name = "Galactic_Box (" + i + "_" + j + "_" + k + ")";
                    GalacticBox boxScript = box.GetComponent<GalacticBox>();
                    boxScript.setSize(boxScale);
                    boxScript.setColor(new Color(i / 3f, j / 3f, k / 3f, 0.5f));

                    boxes.Add(box);
                }
            }
        }
    }

    int resetPlayerPos() {
        //called when the player exceeds one of the 6 bounding box planes

        //this is not ideal, need to come up with a better solution later
        bool meetsX = Mathf.Abs(_playerCamera.transform.position.x) < boxScale / 2;
        bool meetsY = Mathf.Abs(_playerCamera.transform.position.y) < boxScale / 2;
        bool meetsZ = Mathf.Abs(_playerCamera.transform.position.z) < boxScale / 2;

        if (!meetsX) {
            float bound = _playerCamera.transform.position.x < 0 ? boxScale : -boxScale;
            _playerCamera.transform.position += new Vector3(bound, 0, 0);
            return bound < 0 ? -1 : 1;
        }
        if (!meetsY)
        {
            float bound = _playerCamera.transform.position.y < 0 ? boxScale : -boxScale;
            _playerCamera.transform.position += new Vector3(0, bound, 0);
            return bound < 0 ? -2 : 2;
        }
        if (!meetsZ)
        {
            float bound = _playerCamera.transform.position.z < 0 ? boxScale : -boxScale;
            _playerCamera.transform.position += new Vector3(0, 0, bound);
            return bound < 0 ? -3 : 3;
        }
        return int.MaxValue;
    }

    void shiftBoxes(int direction) {
        //what is a shift in a particular direction going to look like?

        //if the player moves positive, the boxes shift negative by 1 box length
        //any box that is outside the 3x3 limit wraps around to the front
        for (int i = 0; i < boxes.Count; ++i) {
            Vector3 shiftDirection = Vector3.zero;
            shiftDirection[Mathf.Abs(direction)-1] = boxScale * (direction < 0 ? -1 : 1); //should give a positive boxScale for the given direction
            boxes[i].transform.position += shiftDirection;

            //wrapping in a given direction
            if (Mathf.Abs(boxes[i].transform.position[Mathf.Abs(direction)-1]) > 1.1f*boxScale)
            {
                //not sure if the math is right
                float bound = boxes[i].transform.position[Mathf.Abs(direction)-1] < 0 ? 3*boxScale : -3*boxScale;
                Vector3 wrapDirection = Vector3.zero;
                wrapDirection[Mathf.Abs(direction) - 1] = bound;
                boxes[i].transform.position += wrapDirection;

                //This lets the box generate new star positions when they wrap
                boxes[i].GetComponent<GalacticBox>().setGenerate(true);
            }
        }
    }

    private bool inBox() {
        bool meetsX = Mathf.Abs(_playerCamera.transform.position.x) < boxScale / 2;
        bool meetsY = Mathf.Abs(_playerCamera.transform.position.y) < boxScale / 2;
        bool meetsZ = Mathf.Abs(_playerCamera.transform.position.z) < boxScale / 2;

        return meetsX && meetsY && meetsZ;
    }
}
