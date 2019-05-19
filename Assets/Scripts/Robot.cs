using UnityLogic;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using TMPro;
using System.Collections.Generic;

public class Robot : Agent
{
    public string kbPath = "KBs/PrologFile";
    public string kbName = "KbName";

    public Rail rail;
    public float speed;
    public Transform homeBase;

    private Rigidbody rb;
    public bool isMoving, pauseMovement;
    private bool moveForward;
    private Transform destination;
    private Material originalIdentifier;

    private GameObject carriedBox;

    private TextMeshPro text;

    void Start()
    {
        Init(kbPath, kbPath);
        rb = GetComponent<Rigidbody>();
        text = GetComponentInChildren<TextMeshPro>();
        originalIdentifier = transform.GetChild(3).gameObject.GetComponent<Renderer>().material;
        pauseMovement = false;
    }

    void FixedUpdate()
    {
        if (isMoving && !pauseMovement)
        {
            if (Vector3.Distance(transform.position, destination.position) < 0.1f)
            {
                isMoving = !isMoving;
            }
            else if (moveForward)
            {
                rb.MovePosition(transform.position + transform.forward * speed * Time.fixedDeltaTime);
            }
            else if (!moveForward)
            {
                rb.MovePosition(transform.position - transform.forward * speed * Time.fixedDeltaTime);
            }
        }
    }

    public IEnumerator Goto(Area a)
    {
        // set the exact square where the robot has to stop
        destination = rail.railInfo[a];

        text.text = "" + a.index;

        transform.GetChild(3).gameObject.GetComponent<Renderer>().material = rail.railInfo[a].parent.GetComponent<Renderer>().material;

        // check if the destination is ahead or behind the robot in order to decide if move forward or backward
        if (Vector3.Angle(transform.forward, destination.position - transform.position) < 90)
        {
            moveForward = true;
        }
        else
        {
            moveForward = false;
        }

        isMoving = true;

        // Do not exit from the coroutine until the robot is arrived at destination (i.e. isMoving is switched to false)
        while (isMoving)
        {
            yield return null;
        }

        text.text = "";
    }

    public IEnumerator GoHome()
    {
        // set the exact square where the robot has to stop
        destination = homeBase;

        text.text = "Home";

        transform.GetChild(3).gameObject.GetComponent<Renderer>().material = originalIdentifier;

        // check if the destination is ahead or behind the robot in order to decide if move forward or backward
        if (Vector3.Angle(transform.forward, destination.position - transform.position) < 90)
        {
            moveForward = true;
        }
        else
        {
            moveForward = false;
        }

        isMoving = true;

        // Do not exit from the coroutine until the robot is arrived at destination (i.e. isMoving is switched to false)
        while (isMoving)
        {
            yield return null;
        }

        text.text = "";
    }

    public void PrintLog(string i)
    {
        Debug.Log(i);
    }

    public void PickUp()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 10);

        GameObject go = null;

        foreach (Collider coll in hitColliders)
        {
            if (coll.gameObject.GetComponent<Box>() != null && coll.gameObject.GetComponent<Box>().nextRobot == this && carriedBox == null)
            {
                go = coll.gameObject;
                go.transform.SetParent(transform.GetChild(1));
                go.transform.localPosition = Vector3.up * 1.7f;
                carriedBox = go;
                carriedBox.GetComponent<Box>().stored = false;
            }
        }

        // if the pickup is happening from a share area, reset the share area index to -1 and the nextRobot to null
        if (go != null && go.GetComponent<Box>().shareAreaIndex != -1)
        {
            go.GetComponent<Box>().shareAreaIndex = -1;
        }

        // since the previous nextRobot has just picked up the box, I can reset nextRobot to null
        go.GetComponent<Box>().nextRobot = null;
    }

    // Store the box at its destination
    public void Store()
    {
        carriedBox.transform.SetParent(destination.parent.GetChild(0));
        carriedBox.transform.localPosition = Vector3.zero;

        WarehouseManager.instance.StoredBox(destination.GetComponentInParent<Area>(), carriedBox.GetComponent<Box>());

        carriedBox.GetComponent<Box>().stored = true;
        carriedBox = null;
    }

    public bool CheckIfDone()
    {
        return WarehouseManager.instance.IsDone(this);
    }

    public void GetNextJob()
    {
        WarehouseManager.instance.GetNextJob(this);
    }

    // Use sensors in order to check if there is a box nearby
    public IEnumerator WaitForBox()
    {
        bool boxFound = false;

        while (!boxFound)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 10);

            foreach (Collider coll in hitColliders)
            {
                Box b = coll.gameObject.GetComponent<Box>();

                if (b != null && b.stored == true && b.nextRobot == this)
                    boxFound = true;
            }

            yield return null;
        }
    }

    // Is the destination area in my rail? If yes, set this as the storage destination and return true, otherwise return null
    public Area FindDestinationArea()
    {
        Area area = null;

        Area dest = carriedBox.GetComponent<Box>().destinationArea.GetComponentInParent<Area>();

        if (rail.railInfo.ContainsKey(dest))
        {
            area = dest;
        }

        return area;
    }

    public Area FindShareArea()
    {
        Area area = null;

        // if robot is searching for an area where to temporarily store the box
        if (carriedBox != null)
        {
            area = WarehouseManager.instance.FindShareArea(this, carriedBox.GetComponent<Box>());

            carriedBox.GetComponent<Box>().shareAreaIndex = area.index;
        }
        // else, the robot is searching for an area where it has a box to pick up
        else
        {
            foreach (Area a in rail.railInfo.Keys)
            {
                if (a.type == "ShareArea")
                {
                    // if this storage area has a box, and its destination area index does not match with the index of the area, it means that the warehouse has changed it, forwarding the pack to another place
                    foreach (GameObject box in a.boxes)
                    {
                        if (a.HasBox() && box.GetComponent<Box>().nextRobot == this)
                            return a;
                    }
                }
            }
        }

        return area;
    }

    public Area FindStorageArea()
    {
        foreach (Area a in rail.railInfo.Keys)
        {
            if (a.type == "StorageArea")
            {
                // if this storage area has a box, and it has a nextRobot parameter set, it means that the warehouse wants to forward the pack to another place
                foreach (GameObject box in a.boxes)
                {
                    if (a.HasBox() && box.GetComponent<Box>().nextRobot == this)
                        return a;
                }
            }
        }

        Debug.LogError(this.gameObject.name + ": should never be here!");
        return null;
    }

    public Area FindPickupArea()
    {
        foreach (KeyValuePair<Area, Transform> area in rail.railInfo)
        {
            if (area.Key.type == "PickupArea")
            {
                return area.Key;
            }
        }

        Debug.LogError(this.gameObject.name + ": should never be here!");
        return null;
    }

    private void OnTriggerEnter(Collider other)
    {
        Junction junction = other.gameObject.GetComponent<Junction>();
        if (junction != null && isMoving)
        {
            // If my triggering collider senses a robot, it means that there is a possible clash
            if (junction.isOccupiedBy != null && junction.isOccupiedBy != this)
            {
                // I have to pause the navigation
                pauseMovement = true;

                StartCoroutine(WaitForJunctionAvailability(junction));
            }
            else
            {
                junction.isOccupiedBy = this;
            }
        }
    }

    private IEnumerator WaitForJunctionAvailability(Junction junction)
    {
        while(junction.isOccupiedBy != null)
        {
            yield return null;
        }

        junction.isOccupiedBy = this;
        pauseMovement = false;
    }

    private void OnTriggerExit(Collider other)
    {
        Junction junction = other.gameObject.GetComponent<Junction>();
        
        if (junction != null && isMoving && junction.isOccupiedBy == this)
        {
            // I have to restore the navigation
            junction.isOccupiedBy = null;
        }
    }

    public Vector3 GetHomeBasePosition()
    {
        return homeBase.position;
    }

    private IEnumerator ChangeText(string txt, float duration, bool blinking, float blinkingTime)
    {
        text.text = txt;

        float time = 0;
        Color originalColor = text.color;

        while (duration > time)
        {
            time += Time.deltaTime;

            if (blinking)
            {
                originalColor.a = Mathf.Sin(time * blinkingTime);

                text.color = originalColor;
            }

            yield return null;
        }

        // Make text disappear by resetting it
        originalColor.a = 1;

        text.color = originalColor;

        text.text = "";
    }
}
