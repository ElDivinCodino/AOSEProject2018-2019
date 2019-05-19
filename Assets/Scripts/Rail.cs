using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rail : MonoBehaviour
{
    public Robot railRobot;

    [Serializable]
    public struct Areas
    {
        public Area area;
        public Transform robotPlace;
    }

    public Areas[] areas;

    // Dictionary containing the area and where the robot has to be in order to be allowed to pick up/store from/to that area
    public Dictionary<Area, Transform> railInfo = new Dictionary<Area, Transform>();

    void Start()
    {

        for (int i = 0; i < areas.Length; i++)
        {
            railInfo.Add(areas[i].area, areas[i].robotPlace);
        }
    }

    public Transform FindShareAreaPlace(Robot r, out int index)
    {
        Dictionary<Area, Transform> r_railInfo = r.rail.railInfo;
        index = -1;

        foreach (KeyValuePair<Area, Transform> area in railInfo)
        {
            foreach (KeyValuePair<Area, Transform> r_area in r_railInfo)
            {
                if (area.Key.type == r_area.Key.type && area.Key.type == "ShareArea" && area.Key.index == r_area.Key.index)
                {
                    index = area.Key.index;
                    return area.Value;
                }
            }
        }

        return null;
    }

    // ASSUMPTION: Only 1 pickup area for each rail
    public Transform FindPickupAreaPlace()
    {
        foreach (KeyValuePair<Area, Transform> area in railInfo)
        {
            if (area.Key.type == "PickupArea")
            {
                return area.Value;
            }
        }

        throw new Exception("This rail does not have pickup area!");
    }
}
