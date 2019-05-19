using System;

namespace Messages
{
	[Serializable]
	public class BoxPost
	{
        public string boxInfo;
        
		public int boxIndex;

        public int areaIndex;

		public override string ToString(){
			return UnityEngine.JsonUtility.ToJson (this, true);
		}
	}
}