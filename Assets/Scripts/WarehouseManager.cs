using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityLogic;
using TMPro;

public class WarehouseManager : Agent
{
    // Singleton pattern
    [HideInInspector]
    public static WarehouseManager instance = null;

    public struct Areas
    {
        public Area area;
        public Transform robotPlace;

        public Areas(Area a, Transform t)
        {
            area = a;
            robotPlace = t;
        }
    }

    // Collection, for each robot, of all the pending pickups it is performing or has to perform
    public Dictionary<Robot, List<Areas>> pendingJobs = new Dictionary<Robot, List<Areas>>();

    public GameObject box;
    public TextMeshProUGUI text;
    public string kbPath = "KBs/PrologFile";
    public string kbName = "KbName";

    [HideInInspector]
    public string ingameLogger = "";

    [HideInInspector]
    public int boxToBeRetrievedIndex, nextPickUpAreaIndex, nextStorageAreaIndex;

    private List<Box> boxesSpawned = new List<Box>();

    [SerializeField]
    Area[] pickupAreas;
    [SerializeField]
    Area[] storageAreas;
    [SerializeField]
    Area[] shareAreas;
    [SerializeField]
    Rail[] rails;
    [SerializeField]
    Robot[] robots;

    EdtInterface edt;
    float simulationTime = 0.00f;

    float coroutineTime;

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    void Start()
    {
        Init(kbPath, kbPath);
        edt = GetComponent<EdtInterface>();

        foreach (Robot r in robots)
        {
            pendingJobs.Add(r, new List<Areas>());
        }

        InitializeAreasIndexes();
    }

    void Update()
    {
        simulationTime += Time.deltaTime;
    }

    public Area CreateBox()
    {
        GameObject newBox = Instantiate(box, storageAreas[nextPickUpAreaIndex].transform.GetChild(0).transform);
        newBox.GetComponent<Box>().index = boxesSpawned.Count;
        newBox.GetComponent<Box>().startAreaIndex = newBox.GetComponent<Box>().destinationAreaIndex = nextPickUpAreaIndex;
        newBox.GetComponent<Box>().startingArea = newBox.GetComponent<Box>().destinationArea = storageAreas[nextPickUpAreaIndex].transform.GetChild(1).gameObject;

        ingameLogger += '\n' + "Box " + newBox.GetComponent<Box>().index + " created at PickupArea " + newBox.GetComponent<Box>().startAreaIndex;

        boxesSpawned.Add(newBox.GetComponent<Box>());

        nextPickUpAreaIndex = nextStorageAreaIndex = -1;

        edt.PostBoxInfo("BoxCreated", newBox.GetComponent<Box>().index, newBox.GetComponent<Box>().startAreaIndex, -1);

        return storageAreas[newBox.GetComponent<Box>().startAreaIndex];
    }

    public Area DeliverNextBoxArea()
    {
        GameObject newBox = Instantiate(box, pickupAreas[nextPickUpAreaIndex].transform.GetChild(0).transform);
        newBox.GetComponent<Box>().index = boxesSpawned.Count;
        newBox.GetComponent<Box>().startAreaIndex = nextPickUpAreaIndex;
        newBox.GetComponent<Box>().destinationAreaIndex = nextStorageAreaIndex;
        newBox.GetComponent<Box>().startingArea = pickupAreas[nextPickUpAreaIndex].transform.GetChild(1).gameObject;
        newBox.GetComponent<Box>().destinationArea = storageAreas[nextStorageAreaIndex].transform.GetChild(1).gameObject;

        ingameLogger += '\n' + "Box " + newBox.GetComponent<Box>().index + " from PickupArea " + newBox.GetComponent<Box>().startAreaIndex + " to StorageArea " + newBox.GetComponent<Box>().destinationAreaIndex;

        boxesSpawned.Add(newBox.GetComponent<Box>());

        nextPickUpAreaIndex = nextStorageAreaIndex = -1;

        return pickupAreas[newBox.GetComponent<Box>().startAreaIndex];
    }

    public Area RetrieveNextBoxArea()
    {
        Box box = boxesSpawned[boxToBeRetrievedIndex];
        box.startAreaIndex = box.destinationAreaIndex;
        box.destinationAreaIndex = nextStorageAreaIndex;
        box.startingArea = storageAreas[box.startAreaIndex].transform.GetChild(1).gameObject;
        box.destinationArea = pickupAreas[box.destinationAreaIndex].transform.GetChild(1).gameObject;

        ingameLogger += '\n' + "Box " + box.index + " from StorageArea " + box.startAreaIndex + " to PickupArea " + box.destinationAreaIndex;

        return storageAreas[box.startAreaIndex];
    }

    public Area MoveNextBoxArea()
    {
        Box box = boxesSpawned[boxToBeRetrievedIndex];
        box.startAreaIndex = box.destinationAreaIndex;
        box.destinationAreaIndex = nextStorageAreaIndex;
        box.startingArea = storageAreas[box.startAreaIndex].transform.GetChild(1).gameObject;
        box.destinationArea = storageAreas[box.destinationAreaIndex].transform.GetChild(1).gameObject;

        ingameLogger += '\n' + "Box " + box.index + " from StorageArea " + box.startAreaIndex + " to StorageArea " + box.destinationAreaIndex;

        return storageAreas[box.startAreaIndex];
    }

    public IEnumerator RobotsBackHome()
    {
        foreach (Robot r in robots)
        {
            r.DelBelief("is_busy");
            r.AddDesire("go_home");
        }

        // wait for every robot starting the journey
        yield return new WaitForSeconds(2);

        // wait until everyone is arrived
        bool allArrived = false;

        while (!allArrived)
        {
            allArrived = true;

            foreach (Robot r in robots)
            {
                if (Vector3.Distance(r.GetHomeBasePosition(), r.transform.position) > 0.2f)
                {
                    allArrived = false;
                }
            }

            yield return null;
        }

        // When here, all robots are arrived home
        edt.PostRobotInfo("AllArrived");
    }

    public void StoredBox(Area area, Box box)
    {
        string tag = "";

        if (area.type == "StorageArea" && box.startingArea.GetComponentInParent<Area>().type != "StorageArea")
        {
            tag = "BoxStored";
        }
        else if (area.type == "StorageArea" && box.startingArea.GetComponentInParent<Area>().type == "StorageArea")
        {
            tag = "BoxMoved";
        }
        else if (area.type == "ShareArea")
        {
            // Do something
        }
        else if (area.type == "PickupArea")
        {
            tag = "BoxRetrieved";
        }

        edt.PostBoxInfo(tag, box.index, box.startAreaIndex, box.destinationAreaIndex);
    }

    public Area FindShareArea(Robot requestingRobot, Box boxToBeDelivered)
    {
        // The rail of the shared area where the box will be stored
        Rail rail1 = requestingRobot.rail;

        // All the rails of the storage area where the box have to be stored at the end  /////////////////////////////
        List<Rail> possibleRails = new List<Rail>();                                                                //
                                                                                                                    //
        foreach (Rail rail in rails)                                                                                //
        {                                                                                                           //
            if (rail != rail1)                                                                                      //
            {
                if (rail.railInfo.ContainsKey(boxToBeDelivered.destinationArea.GetComponentInParent<Area>()))
                {
                    possibleRails.Add(rail);
                }                                                                                                 //
            }                                                                                                       //
        }                                                                                                           //
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////

        // See if there is a shared area between rail1 and one of the possibleRails, if so pick the first
        foreach (KeyValuePair<Area, Transform> area in rail1.railInfo)
        {
            if (area.Key.type == "ShareArea")
            {
                foreach (Rail possibleRail in possibleRails)
                {
                    if (possibleRail.railInfo.ContainsKey(area.Key))
                    {
                        boxToBeDelivered.nextRobot = possibleRail.railRobot;
                        return area.Key;
                    }
                }
            }
        }

        // If no possibleRail shares a sharedArea with rail1 (i.e. 2 shared areas are needed) just move the box to the first rail with a common shared area
        foreach (Rail rail in rails)
        {
            if (rail != rail1)
            {
                foreach (KeyValuePair<Area, Transform> area in rail1.railInfo)
                {
                    if (area.Key.type == "ShareArea" && rail.railInfo.ContainsKey(area.Key))
                    {
                        boxToBeDelivered.nextRobot = rail.railRobot;
                        return area.Key;
                    }
                }
            }
        }

        // Should never be here
        Debug.LogError("No area found: check the code");
        return null;
    }

    public void PrintLog(string str)
    {
        Debug.Log(str);
    }

    public Robot GetAppropriateRobot(Area area, Box box)
    {
        if (area.type == "PickupArea")
        {
            foreach (Rail rail in rails)
            {
                foreach (KeyValuePair<Area, Transform> a in rail.railInfo)
                {
                    if (a.Key.type == "PickupArea" && a.Key.index == box.startAreaIndex)
                    {
                        box.nextRobot = rail.railRobot;
                        return rail.railRobot;
                    }
                }
            }
        }
        else if (area.type == "ShareArea")
        {
            return box.nextRobot;
        }
        else if (area.type == "StorageArea")
        {
            Robot next = null;

            foreach (Rail rail in rails)
            {
                if (rail.railInfo.ContainsKey(area))
                {
                    next = rail.railRobot;

                    // if this rail contains both starting end destination area, just return
                    if (rail.railInfo.ContainsKey(box.destinationArea.transform.parent.gameObject.GetComponent<Area>()))
                    {
                        box.nextRobot = next;
                        return next;
                    }
                }
            }

            box.nextRobot = next;
            return next;
        }

        // Should never be here
        Debug.LogError("Check the GetAppropriateRobot() method");
        return null;
    }

    public void EnqueueJob(Area area, Robot r)
    {
        pendingJobs[r].Add(new Areas(area, r.rail.railInfo[area]));
    }

    // Check if a robot has completed all its jobs
    public bool IsDone(Robot r)
    {
        if (pendingJobs[r].Count > 0)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    // Get the next goal a Robot has to achieve and in which area the next box is placed
    public void GetNextJob(Robot r)
    {
        if (pendingJobs[r].Count > 0)
        {
            r.AddDesire("pick_up");

            switch (pendingJobs[r][0].area.type)
            {
                case "PickupArea":
                    r.AddBelief("box_at_pickup_area");
                    break;
                case "ShareArea":
                    r.AddBelief("box_at_share_area");
                    break;
                case "StorageArea":
                    r.AddBelief("box_at_storage_area");
                    break;
                default:
                    Debug.LogError("Should never be here!");
                    break;
            }

            WarehouseManager.instance.pendingJobs[r].RemoveAt(0);
        }
        else
        {
            // Should never be here
            Debug.LogError("No jobs to get! ERROR.");
        }
    }

    private void InitializeAreasIndexes()
    {
        for (int i = 0; i < pickupAreas.Length; i++)
        {
            pickupAreas[i].index = i;
        }

        for (int i = 0; i < shareAreas.Length; i++)
        {
            shareAreas[i].index = i;
        }

        for (int i = 0; i < storageAreas.Length; i++)
        {
            storageAreas[i].index = i;
        }
    }

    public IEnumerator ChangeText(string txt, float duration, bool blinking, float blinkingTime)
    {
        text.text = txt;

        float coroutineTime = 0;
        Color originalColor = text.color;

        while (duration > coroutineTime)
        {
            coroutineTime += Time.deltaTime;

            if (blinking)
            {
                originalColor.a = Mathf.Sin(coroutineTime * blinkingTime);

                text.color = originalColor;
            }
            else
            {
                originalColor.a = coroutineTime / duration;

                text.color = originalColor;
            }

            yield return null;
        }

        // Make text remain by setting it to 1
        originalColor.a = 1;

        text.color = originalColor;
    }
}
