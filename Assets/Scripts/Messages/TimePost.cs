using System;

namespace Messages
{
	[Serializable]
	public class TimePost
	{
		public float timePassed;

		public override string ToString(){
			return UnityEngine.JsonUtility.ToJson (this, true);
		}
	}
}