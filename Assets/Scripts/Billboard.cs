using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Camera m_Camera;
    public CameraSwitcher cameraSwitcher;

    private void Start() {
        if (m_Camera == null)
            m_Camera = Camera.main;

        cameraSwitcher = GameObject.Find("CameraController").GetComponent<CameraSwitcher>();
    }

    //Orient the camera after all movement is completed this frame to avoid jittering
    void LateUpdate()
    {
        m_Camera = cameraSwitcher.currentCam;
        transform.LookAt(transform.position + m_Camera.transform.rotation * Vector3.forward,
            m_Camera.transform.rotation * Vector3.up);
    }
}
