using UnityEngine;
using System.Collections;

public class qrCode : MonoBehaviour {

    public static bool visible;

	// Use this for initialization
	void Start () {
        visible = true;
	}
	
	// Update is called once per frame
	void Update () {
	    if(visible == false)
        {
            transform.position = new Vector3(-500f, -500f, -500f);
        }
	}
}
