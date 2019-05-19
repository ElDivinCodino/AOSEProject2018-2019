using UnityEngine;
using Proyecto26;
using System.Collections;
using DeltaInformatica.Elevate;
using System;

/* All the stuff between Unity and the EDT should be managed in this class */
public class EdtInterface : MonoBehaviour
{

    private RequestHelper currentRequest;

    public string Hostname { get; private set; }
    public int Port { get; private set; }
    public string User { get; private set; }
    public string Password { get; private set; }
    public string Protocol { get; private set; }

    private EdtClient edtClient;

    public EdtInterface()
    {
        Hostname = "elevate.deltainformatica.eu";
        Port = 443;
        User = "test@aose.it";
        Password = "mypass";
        Protocol = "https";
    }

    public void LateUpdate()
    {
        //update edt client time
        edtClient.CurrentTime = Time.time;

        //verify warning messages
        string warningMessage = edtClient.GetWarningMessage();
        if (null != warningMessage)
        {
            Debug.LogWarning(warningMessage);
        }

        //verify commands
        string commandsString = edtClient.GetDescription();
        if (null != commandsString)
        {
            //Debug.Log("Commands to be executed: " + commandsString);
            StartCoroutine(ParseDescription(commandsString));
        }

        //update tags (this can be performed at any time)
        if (Time.realtimeSinceStartup > 5f && Time.realtimeSinceStartup < 10f)
        {
            edtClient.SetTag("foul", true);
        }
    }

    void Start()
    {
        //starting EDT client
        Debug.Log("Starting the EDT Client...");
        edtClient = new EdtClient(Hostname, Port, User, Password, Protocol);
        // test (should be called by EDT)
        //StartCoroutine(SpawnBoxesGradually(2));
    }

    public void OnDisable()
    {
        edtClient.Stop();
    }

    // Tell the WarehouseManager the indexes of the pickup and storage areas for the next box, and add to its desire set the "generate_box" goal 
    void CreateBox(int fromIndex, int toIndex)
    {
        WarehouseManager.instance.nextPickUpAreaIndex = fromIndex;
        WarehouseManager.instance.nextStorageAreaIndex = toIndex;

        if (toIndex != -1)
        {
            AddDesire ("deliver_box");
        }
        else
        {
            AddDesire("create_box");
        }
    }

    // Tell the WarehouseManager the index of the box that should be retrieved and the pickup area index where it has to be delivered
    void RetrieveBox(int boxIndex, int pickupIndex)
    {
        WarehouseManager.instance.boxToBeRetrievedIndex = boxIndex;
        WarehouseManager.instance.nextStorageAreaIndex = pickupIndex;

        AddDesire("retrieve_box");
    }

    // Tell the WarehouseManager the index of the box that should be moved and the storage area index where it has to be delivered
    void MoveBox(int boxIndex, int storageIndex)
    {
        WarehouseManager.instance.boxToBeRetrievedIndex = boxIndex;
        WarehouseManager.instance.nextStorageAreaIndex = storageIndex;

        AddDesire("move_box");
    }

    // Tell the WarehouseManager that all the robots should go back to their own base
    void AllRobotsAtHome()
    {
        AddDesire("back_home");
    }

    #region EDT Commands

    IEnumerator ParseDescription(string str)
    {
        Debug.Log(str);
        
        // Reset the in-game logger
        WarehouseManager.instance.ingameLogger = "";
        StartCoroutine(WarehouseManager.instance.ChangeText(WarehouseManager.instance.ingameLogger, 0, false, 0));

        WaitForSeconds wait = new WaitForSeconds(0.5f);

        // get all the lines of the description, one by one
        foreach (var line in str.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
        {
            if (line.Contains("Create_Box"))
            {
                string[] token = line.Split(new Char[] { ' ', ',', '\t' });

                if (token.Length > 2)
                    throw new Exception("Wrong Create_Box syntax!");

                int index = int.Parse(token[1]);

                CreateBox(index, -1);
            }
            else if (line.Contains("Deliver_Box"))
            {
                string[] token = line.Split(new Char[] { ' ', ',', '\t' });

                if (token.Length > 3)
                    throw new Exception("Wrong Deliver_Box syntax!");

                int fromIndex = int.Parse(token[1]);
                int toIndex = int.Parse(token[2]);

                CreateBox(fromIndex, toIndex);
            }
            else if (line.Contains("Retrieve_Box"))
            {
                string[] token = line.Split(new Char[] { ' ', ',', '\t' });

                if (token.Length > 3)
                    throw new Exception("Wrong Retrieve_Box syntax!");

                int boxIndex = int.Parse(token[1]);
                int toIndex = int.Parse(token[2]);

                RetrieveBox(boxIndex, toIndex);
            }
            else if (line.Contains("Move_Box"))
            {
                string[] token = line.Split(new Char[] { ' ', ',', '\t' });

                if (token.Length > 3)
                    throw new Exception("Wrong Move_Box syntax!");

                int boxIndex = int.Parse(token[1]);
                int toIndex = int.Parse(token[2]);

                MoveBox(boxIndex, toIndex);
            }
            else if (line.Contains("Timeout"))
            {
                // Managed internally by the EDT
            }
            else if (line.Contains("Job_Completed"))
            {
                AllRobotsAtHome();
            }

            // Needed in order to let the prolog part process the request
            yield return wait;
        }
        
        // Display the in-game logger
        StartCoroutine(WarehouseManager.instance.ChangeText(WarehouseManager.instance.ingameLogger, 1, false, 0));
    }

    public void PostBoxInfo(string info, int boxId, int startingAreaId, int arrivingAreaId)
    {
        switch (info)
        {
            case "BoxCreated":
                edtClient.SetTag("Box" + boxId + "Created", true);
                break;
            case "BoxStored":
                edtClient.SetTag("Box" + boxId + "StoredAt" + arrivingAreaId, true);
                break;
            case "BoxRetrieved":
                edtClient.SetTag("Box" + boxId + "RetrievedAt" + arrivingAreaId, true);
                break;
            case "BoxMoved":
                edtClient.SetTag("Box" + boxId + "MovedTo" + arrivingAreaId, true);
                break;
            default:
                break;
        }
    }

    public void PostRobotInfo(string info)
    {
        switch (info)
        {
            case "AllArrived":
                edtClient.SetTag("AllBackHome", true);
                break;
            default:
                break;
        }
    }

    #endregion

    #region BDI Commands
    void AddBelief(object beliefName)
    {
        WarehouseManager.instance.AddBelief(beliefName);
    }

    void DelBelief(object beliefName)
    {
        WarehouseManager.instance.DelBelief(beliefName);
    }

    void CheckBelief(object beliefName)
    {
        WarehouseManager.instance.CheckBelief(ref beliefName);
    }

    void AddDesire(object desireName)
    {
        WarehouseManager.instance.AddDesire(desireName);
    }

    void DelDesire(object desireName)
    {
        WarehouseManager.instance.DelDesire(desireName);
    }

    void CheckDesire(object desireName)
    {
        WarehouseManager.instance.CheckDesire(ref desireName);
    }
    #endregion

}
