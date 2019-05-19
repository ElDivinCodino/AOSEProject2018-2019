using UnityLogic;
using UnityEngine;
using TMPro;

public class Box : Artifact
{
    public string type = "N.D.";

    public string kbPath = "KBs/prologfile";
    public string kbName = "kbName";


    public GameObject startingArea, destinationArea;
    public int index, startAreaIndex, shareAreaIndex, destinationAreaIndex;
    public Robot nextRobot;
    public bool stored;

    private TextMeshPro tmp;

    void Awake() 
    {
        // In Awake() because if put in Start() this changes will override those made in WarehouseManager.SpawnNextBox()
        startAreaIndex = shareAreaIndex = destinationAreaIndex = -1;
        nextRobot = null;
    }

    void Start()
    {
        Init(kbPath, kbName);
        stored = true;
        tmp = GetComponentInChildren<TextMeshPro>();
        tmp.text = "" + index;
    }

    public GameObject GetDestination()
    {
        return destinationArea;
    }
}
