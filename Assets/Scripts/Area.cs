using UnityLogic;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using TMPro;
using System.Collections.Generic;

public class Area : Agent
{
    public string type = "N.D.";

    public string kbPath = "KBs/prologfile";
    public string kbName = "kbName";

    public int index;

    public Robot railBot;

    private TextMeshPro text;
    public List<GameObject> boxes = new List<GameObject>();

    private bool hasBox = false;

    void Start()
    {
        Init(kbPath, kbName);
        text = GetComponentInChildren<TextMeshPro>();
    }

    public void PrintLog(string str)
    {
        Debug.Log(str);
    }

    public Robot GetRobot()
    {
        Robot chosenRobot = null;

        foreach (GameObject box in boxes)
        {
            if (box.GetComponent<Box>().destinationArea.transform.parent.gameObject.name != this.gameObject.name)
            {
                chosenRobot = WarehouseManager.instance.GetAppropriateRobot(this, box.GetComponent<Box>());
            }
        }

        return chosenRobot;
    }

    public void EnqueueJob(Robot r)
    {
        WarehouseManager.instance.EnqueueJob(this, r);
    }

    public void CheckForBoxes()
    {
        if (hasBox)
            AddBelief("has_box");
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("Box"))
        {
            boxes.Add(other.gameObject);

            if (boxes.Count > 0)
                hasBox = true;
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.name.Contains("Box"))
        {
            boxes.Remove(other.gameObject);

            if (boxes.Count < 1)
                hasBox = false;
        }
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

    public bool HasBox()
    {
        return hasBox;
    }
}
