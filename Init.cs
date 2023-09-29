using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Init : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Transform XRO = transform;
        Transform CamO = GameObject.Find("Camera Offset").transform;
        Transform MainC = GameObject.Find("Main Camera").transform;
        Vector3 initPos = new Vector3(0, 0, 0);
        Vector3 initCPos = new Vector3(0, 1.3f, 0);
        Quaternion initRot = Quaternion.Euler(0, 0, 0);

        XRO.position = initPos;
        XRO.rotation = initRot;
        CamO.position = initCPos;
        CamO.rotation = initRot;
        MainC.position = initPos;
        MainC.rotation = initRot;

        Debug.Log("Position XR origin:" + XRO.position + ", Camera Offset: " + CamO.position + ", Main Camera: " + MainC.position);
        Debug.Log("Position XR rotation:" + XRO.rotation + ", Camera Offset: " + CamO.rotation + ", Main Camera: " + MainC.rotation);
    }

}
