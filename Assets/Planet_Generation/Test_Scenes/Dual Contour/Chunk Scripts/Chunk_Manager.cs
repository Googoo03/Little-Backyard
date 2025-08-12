using QuadTree;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Chunk_Manager : MonoBehaviour
{
    // Start is called before the first frame update
    private QuadTreeNode quadTree;
    [SerializeField] private int LODLevel;
    [SerializeField] private GameObject chunkPrefab;
    [SerializeField] private bool next;
    [SerializeField] private bool prev;

    void Start()
    {   
        ChunkConfig rootConfig = new ChunkConfig(0,65,Vector2.zero, Vector3Int.one * 32);
        quadTree = new QuadTreeNode(rootConfig, chunkPrefab, null);
        quadTree.GetGameObject().GetComponent<DC_Chunk>().InitializeDualContour();
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
    }

    public void DFSNextLod(QuadTreeNode node) {

        if (!node.HasChildren()) { node.NextLOD(); return; }

        List<QuadTreeNode> children = node.GetChildren();
        foreach (QuadTreeNode child in children) { DFSNextLod(child); }
    }

    public void DFSPrevLod(QuadTreeNode node) {

        if (!node.HasChildren() || node.GetLODLevel() == LODLevel-1) { node.PrevLOD(); return; }

        List<QuadTreeNode> children = node.GetChildren();
        foreach (QuadTreeNode child in children) { DFSPrevLod(child); }
        
    }
}
