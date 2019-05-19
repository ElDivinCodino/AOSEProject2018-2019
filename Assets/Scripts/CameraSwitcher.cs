using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraSwitcher : MonoBehaviour {

	public Camera persp, orth;

	[HideInInspector]
	public Camera currentCam;

	bool isOrth;

	void Start() {
		isOrth = orth.isActiveAndEnabled;
		
		if(isOrth)
		{
			currentCam = orth;
		}
		else
		{
			currentCam = persp;
		}
	}

	public void SwitchCamera()
	{
		isOrth = !isOrth;
		orth.enabled = isOrth;
		persp.enabled = !orth.isActiveAndEnabled;

		if(isOrth)
		{
			currentCam = orth;
		}
		else
		{
			currentCam = persp;
		}
	}
}
