using UnityEngine;
using System.Collections;
using System.Net;

public class HelloInternet : MonoBehaviour {
	void Start () {

		// Method 1

		if (CheckConnection ("http://gravityhunter.mi.hdm-stuttgart.de") == true)
			Debug.Log ("Method 1: Website ist erreichbar");
		else Debug.Log ("Method 1: Website ist NICHT erreichbar");



		// Method 2

		if (HasConnection () == true) Debug.Log ("Method 2: Website ist erreichbar");
		else Debug.Log ("Method 2: Website ist NICHT erreichbar");


	}



	// Method 1

	bool CheckConnection(string URL){
		try{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
			request.Timeout = 5000;
			request.Credentials = CredentialCache.DefaultNetworkCredentials;
			HttpWebResponse response = (HttpWebResponse)request.GetResponse();

			if (response.StatusCode == HttpStatusCode.OK) return true;
			else return false;
		}
		catch{
			return false;
		}
	}



	// Method 2

	public static bool HasConnection(){
		try{
			using (var client = new WebClient())
			using (var stream = new WebClient().OpenRead("http://gravityhunter.mi.hdm-stuttgart.de")){
				return true;
			}
		}catch{
			return false;
		}
	}

}