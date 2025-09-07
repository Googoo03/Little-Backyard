using DualContour;
using QuadTree;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Chunk_Manager : MonoBehaviour
{
    // Start is called before the first frame update
    private QuadTreeNode quadTree;
    [SerializeField] private float baseLength;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private int LODLevel;
    [SerializeField] private GameObject chunkPrefab;
    [SerializeField] private bool next;
    [SerializeField] private bool prev;

    void Start()
    {   
        mainCamera = Camera.main;
        ChunkConfig rootConfig = new ChunkConfig(0,201,Vector2.zero, Vector3Int.one * 16, false, transform, baseLength);
        quadTree = new QuadTreeNode(rootConfig, chunkPrefab, null);
        quadTree.GetGameObject().GetComponent<DC_Chunk>().InitializeDualContourBounds();
        quadTree.GetGameObject().GetComponent<DC_Chunk>().InitializeDualContour();
        quadTree.RenderChunk();
    }

    // Update is called once per frame
    void Update()
    {
        
        if (next) {
            next = false;
            DFSNextLod(quadTree);
            LODLevel++;
        }

        if (prev) {
            prev = false;
            DFSPrevLod(quadTree);
            LODLevel -= LODLevel > 0 ? 1 : 0;
        }

        //Automatic subdivision based on distance to camera
        DFSCheckDistance(quadTree);
    }

    public void DFSNextLod(QuadTreeNode node) {

        if (!node.HasChildren()) { node.NextLOD(); return; }

        List<QuadTreeNode> children = node.GetChildren();
        for (int i = 0; i < children.Count; ++i) {
            //if (i % 2 == 0) continue;
            QuadTreeNode child = children[i];
            DFSNextLod(child);
        }
        
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = new Color(1,1,1,0.5f);
        DFSGizmos(quadTree);
    }

    //temporary, shows the bounding boxes of each leaf
    public void DFSGizmos(QuadTreeNode node) {
        Dual_Contour nodeDC = node.GetGameObject().GetComponent<DC_Chunk>().GetDC();
        if (!node.HasChildren())
        {
            float size = nodeDC.GetSideLength();
            Gizmos.DrawWireCube(nodeDC.GetCenter(), Vector3.one * size);
            return;
        }

        foreach (QuadTreeNode child in node.GetChildren())
        {
            DFSGizmos(child);
        }
    }

    public void DFSCheckDistance(QuadTreeNode node) {

        Dual_Contour nodeDC = node.GetGameObject().GetComponent<DC_Chunk>().GetDC();
        float distance = (nodeDC.GetCornerCenter() + transform.position - mainCamera.transform.position).magnitude;
        if (!node.HasChildren() && !node.IsEmpty() && node.GetLODLevel() < 8 && ( (distance < (baseLength / (1 << node.GetChunkConfig().lodLevel))) || node.GetGameObject().GetComponent<DC_Chunk>().subdivide    )) {
            node.NextLOD();
            node.GetGameObject().SetActive(false);
            node.GetSeamGameObject().SetActive(false);
            return;
        }

        foreach (QuadTreeNode child in node.GetChildren()) {
            DFSCheckDistance(child);
        }
        
    }

    public void DFSPrevLod(QuadTreeNode node) {

        if (!node.HasChildren() || node.GetLODLevel() == LODLevel-1) { node.PrevLOD(); return; }

        List<QuadTreeNode> children = node.GetChildren();
        foreach (QuadTreeNode child in children) { DFSPrevLod(child); }
        
    }
}
