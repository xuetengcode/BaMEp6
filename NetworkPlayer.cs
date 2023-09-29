 using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;

public class NetworkPlayer : NetworkBehaviour
{
    //First define some global variables in order to speed up the Update() function
    GameObject myXRRig;
    XRInputModalityManager HaCM;                    //what mode are we in: controller, hands, none?
    Transform myXRLH, myXRRH, myXRLC, myXRRC, myXRCam;  //positions and rotations
    Transform avHead, avLeft, avRight, avBody;          //avatars moving parts 

    //some fine tuning parameters if needed
    [SerializeField]
    private Vector3 avatarLeftPositionOffset, avatarRightPositionOffset;
    [SerializeField]
    private Quaternion avatarLeftRotationOffset, avatarRightRotationOffset;
    [SerializeField]
    private Vector3 avatarHeadPositionOffset;
    [SerializeField]
    private Quaternion avatarHeadRotationOffset;

    // Start is called before the first frame update
    public override void OnNetworkSpawn()
    {
        var myID = transform.GetComponent<NetworkObject>().NetworkObjectId;
        if (IsOwnedByServer)
            transform.name = "Host:" + myID;    //this must be the host
        else
            transform.name = "Client:" + myID; //this must be the client 

        if (!IsOwner) return;

        myXRRig = GameObject.Find("XR Origin (XR Rig)");
        if (myXRRig)
        {
            Debug.Log("Found XR Origin");
           
        }
        else Debug.Log("Could not find XR Origin!");

        //pointers to the XR RIg
        HaCM = myXRRig.GetComponent<XRInputModalityManager>();
        myXRLH = HaCM.leftHand.transform.Find("Direct Interactor");
        myXRLC = HaCM.leftController.transform;
        myXRRH = HaCM.rightHand.transform.Find("Direct Interactor");
        myXRRC = HaCM.rightController.transform;
        myXRCam = GameObject.Find("Main Camera").transform;

        //pointers to the avatar
        avLeft = transform.Find("Left Hand");
        avRight = transform.Find("Right Hand");
        avHead = transform.Find("Head");
        avBody = transform.Find("Body");

        //ADD THIS CODE!!!
        var vTog = GameObject.Find("Toggle").GetComponent<Toggle>();
        if (vTog.isOn)
        {
            GameObject.Find("Network Manager").GetComponent<VivoxPlayer>().SignIntoVivox();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;
        if (!myXRRig) return;

        switch (HaCM.m_LeftInputMode)
        {
            case XRInputModalityManager.InputMode.MotionController:
                if (avLeft)
                {
                    avLeft.position = myXRLC.position + avatarLeftPositionOffset;
                    avLeft.rotation = myXRLC.rotation * avatarLeftRotationOffset;
                }
                break;
            case XRInputModalityManager.InputMode.TrackedHand:
                if (avLeft)
                {
                    avLeft.position = myXRLH.position + avatarLeftPositionOffset;
                    avLeft.rotation = myXRLH.rotation * avatarLeftRotationOffset;
                }
                break;

            case XRInputModalityManager.InputMode.None:
                break;
        }

        switch (HaCM.m_RightInputMode)
        {
            case XRInputModalityManager.InputMode.MotionController:
                if (avRight)
                {
                    avRight.position = myXRRC.position + avatarRightPositionOffset;
                    avRight.rotation = myXRRC.rotation * avatarRightRotationOffset;
                }
                break;
            case XRInputModalityManager.InputMode.TrackedHand:
                if (avRight)
                {
                    avRight.position = myXRRH.position + avatarRightPositionOffset;
                    avRight.rotation = myXRRH.rotation * avatarRightRotationOffset;
                }
                break;

            case XRInputModalityManager.InputMode.None:
                break;
        }

        if (avHead)
        {
            avHead.position = myXRCam.position + avatarHeadPositionOffset;
            avHead.rotation = myXRCam.rotation * avatarHeadRotationOffset;
        }

        if (avBody)
        {
            avBody.position = avHead.position + new Vector3(0, -0.5f, 0);
        }
    }
}


