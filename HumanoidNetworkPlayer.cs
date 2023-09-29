using System.Collections;
using System.Collections.Generic;
using Unity.Multiplayer.Samples.Utilities.ClientAuthority;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class HumanoidNetworkPlayer : NetworkBehaviour
{
    //First define some global variables in order to speed up the Update() function
    GameObject myXRRig;
    XRInputModalityManager HaCM;                    //what mode are we in: controller, hands, none?
    Transform myXRLH, myXRRH, myXRLC, myXRRC, myXRCam;  //positions and rotations
    Transform avHead, avLeft, avRight, avBody;          //avatars moving parts 

    float avtScale, avtHeight, avtToeYPos, avtEyeYPos;  //floats for Autotune
    bool autoTune = false;

    Transform myCameraOffset;                           //third/first person toggle
    Toggle thirdPToggle;

    //For animation control
    InputAction rightTurnAction, leftMoveAction;
    Animator avtAnimator;

    //Fingers
    Transform[] XRigLeftHandFingers, XRigRightHandFingers;
    Transform[] AvatLeftHandFingers, AvatRightHandFingers;

    Transform leftPalmBone, rightPalmBone;
    GameObject handVisualizer;
    //bool leftHasController, rightHasController;

    //WFS BUG
    bool avatarHasController;
    AvatarHasController avatarHCState;      

    //some fine tuning parameters if needed
    [SerializeField]
    private Vector3 avatarLeftPositionOffset, avatarRightPositionOffset;
    [SerializeField]
    private Quaternion avatarLeftRotationOffset, avatarRightRotationOffset;
    [SerializeField]
    private Vector3 avatarHeadPositionOffset;
    [SerializeField]
    private Quaternion avatarHeadRotationOffset;
    [SerializeField] Vector3 thirdPOffset, firstPOffset;

    // Start is called before the first frame update
    public override void OnNetworkSpawn()
    {
        var myID = transform.GetComponent<NetworkObject>().NetworkObjectId;
        if (IsOwnedByServer)
            transform.name = "Host:" + myID;    //this must be the host
        else
            transform.name = "Client:" + myID; //this must be the client 

        //WFS BUG
        avatarHCState = transform.Find("XR IK Rig").Find("AvatarHasController").GetComponent<AvatarHasController>();

        if (!IsOwner) return;

        //Fingers - UPDATED!
        AvatLeftHandFingers = new Transform[5];
        AvatRightHandFingers = new Transform[5];
        XRigLeftHandFingers = new Transform[5];
        XRigRightHandFingers = new Transform[5];

        myXRRig = GameObject.Find("XR Origin (XR Rig)");
        if (myXRRig)
        {
            Debug.Log("Found XR Origin");

        }
        else Debug.Log("Could not find XR Origin!");

        //Animation
        rightTurnAction = GameObject.Find("Turn").GetComponent<ActionBasedContinuousTurnProvider>().rightHandTurnAction.action;
        leftMoveAction = GameObject.Find("Move").GetComponent<ActionBasedContinuousMoveProvider>().leftHandMoveAction.action;
        avtAnimator = transform.GetComponent<Animator>();

        //pointers to the XR RIg
        HaCM = myXRRig.GetComponent<XRInputModalityManager>();
        //myXRLH = HaCM.leftHand.transform.Find("Direct Interactor"); //Fingers
        myXRLC = HaCM.leftController.transform;
        //myXRRH = HaCM.rightHand.transform.Find("Direct Interactor"); //Fingers
        myXRRC = HaCM.rightController.transform;
        myXRCam = GameObject.Find("Main Camera").transform;

        //Fingers
        handVisualizer = GameObject.Find("LeftHandDebugDrawJoints");
        if (!handVisualizer) Debug.Log("ERROR: your XR system has no controllers nor hands active!");
        else
        {
            myXRLH = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("Palm").transform;
            myXRRH = GameObject.Find("RightHandDebugDrawJoints").transform.Find("Palm").transform;
        }

        myCameraOffset = GameObject.Find("Camera Offset").transform;

        //pointers to the avatar
        avLeft = transform.Find("XR IK Rig").Find("Left Arm IK").Find("Left Arm IK_target");
        avRight = transform.Find("XR IK Rig").Find("Right Arm IK").Find("Right Arm IK_target");
        avHead = transform.Find("XR IK Rig").Find("Head IK").Find("Head IK_target");
        avBody = transform;

        //ADD THIS CODE!!!
        var vTog = GameObject.Find("Toggle").GetComponent<Toggle>();
        if (vTog.isOn)
        {
            GameObject.Find("Network Manager").GetComponent<VivoxPlayer>().SignIntoVivox();
        }

        thirdPToggle = GameObject.Find("CameraPosition").GetComponent<Toggle>();    //can read IsOn state here
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!IsOwner) return;
        if (!avtAnimator) return;

        if (!autoTune)
        {
            avtEyeYPos = avHead.position.y*1.14f;
            avtToeYPos = avtAnimator.GetIKPosition(AvatarIKGoal.LeftFoot).y;        //get the position of the foot from the IK system
            avtHeight = avtEyeYPos - avtToeYPos;
            avtScale = (myXRCam.position.y != 0 ? avtHeight / myXRCam.position.y : 1);
            myXRRig.transform.localScale = new Vector3(avtScale, avtScale, avtScale);
            Debug.Log("XR Origin scaled to " + avtScale);

            if (handVisualizer)
            {
                //Initial set of the global position of the avatar hands
                avLeft.position = (HaCM.m_LeftInputMode == XRInputModalityManager.InputMode.MotionController ? myXRLC.position : myXRLH.position);
                avRight.position = (HaCM.m_RightInputMode == XRInputModalityManager.InputMode.MotionController ? myXRRC.position : myXRRH.position);

                MapHands();
            }

            autoTune = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner)
        {
            if (avatarHCState.HasChanged())
            {
                int x = avatarHCState.get();
                Debug.Log("Client with id " + transform.GetComponent<NetworkObject>().NetworkObjectId + " switched to " + (x == 0 ? "controllers" : "hands"));
                AvatarTurnOnOffHandsIK((float)x);
                avatarHCState.Reset();
            }
            return;
        }
        
        if (!myXRRig) return;
        if (!autoTune) return;
        if (!handVisualizer) return;

        Vector3 cameraPOffset = myCameraOffset.rotation * (thirdPToggle.isOn ? thirdPOffset : firstPOffset); //global coordinates

        switch (HaCM.m_LeftInputMode)
        {
            case XRInputModalityManager.InputMode.MotionController:
                if (avLeft)
                {
                    //avLeft.position = myXRLC.position + avatarLeftPositionOffset;
                    avLeft.localPosition = Quaternion.Inverse(myCameraOffset.rotation) * (myXRLC.position - myCameraOffset.position);
                    avLeft.rotation = myXRLC.rotation * avatarLeftRotationOffset;

                    float leftControllerMoveValueY = leftMoveAction.ReadValue<Vector2>()[1];
                    if (leftControllerMoveValueY == 0)
                        avtAnimator.SetBool("IsMoving", false);
                    else
                    {
                        avtAnimator.SetBool("IsMoving", true);
                        avtAnimator.SetFloat("DirectionY", leftControllerMoveValueY);
                    }

                    float leftControllerMoveValueX = leftMoveAction.ReadValue<Vector2>()[0];
                    if (leftControllerMoveValueX == 0)
                        avtAnimator.SetBool("IsMoving", false);
                    else
                    {
                        avtAnimator.SetBool("IsMoving", true);
                        avtAnimator.SetFloat("DirectionX", leftControllerMoveValueX);
                    }

                    //if (!leftHasController) AvatarTurnOnOffLeftHandIK(0);
                    //leftHasController = true;
                    if (!avatarHasController)
                    {
                        AvatarTurnOnOffHandsIK(0); //WFS BUG: turn OFF finger IK
                        avatarHCState.set(0);
                    }
                    avatarHasController = true;
                }
                break;

            case XRInputModalityManager.InputMode.TrackedHand:
                if (avLeft)
                {
                    //avLeft.position = myXRLH.position + avatarLeftPositionOffset;
                    avLeft.localPosition = Quaternion.Inverse(myCameraOffset.rotation) * (myXRLH.position - myCameraOffset.position);
                    avLeft.rotation = myXRLH.rotation * avatarLeftRotationOffset;

                    //Fingers
                    LinkAvtFingersToXROrigin(AvatLeftHandFingers, XRigLeftHandFingers, avLeft);

                    //if (leftHasController) AvatarTurnOnOffLeftHandIK(1);
                    //leftHasController = false;
                    if (avatarHasController)
                    {
                        AvatarTurnOnOffHandsIK(1);  //WFS BUG: turn ON finger IK
                        avatarHCState.set(1);
                    }
                    avatarHasController = false;
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
                    //avRight.position = myXRRC.position + avatarRightPositionOffset;
                    avRight.localPosition = Quaternion.Inverse(myCameraOffset.rotation) * (myXRRC.position - myCameraOffset.position);
                    avRight.rotation = myXRRC.rotation * avatarRightRotationOffset;

                    float rightControllerRotateValueX = rightTurnAction.ReadValue<Vector2>()[0];
                    if (rightControllerRotateValueX == 0)
                        avtAnimator.SetBool("IsRotating", false);
                    else
                    {
                        avtAnimator.SetBool("IsRotating", true);
                        avtAnimator.SetFloat("RotationX", rightControllerRotateValueX);
                    }

                    //if (!rightHasController) AvatarTurnOnOffRightHandIK(0);
                    //rightHasController = true;
                }
                break;

            case XRInputModalityManager.InputMode.TrackedHand:
                if (avRight)
                {
                    //avRight.position = myXRRH.position + avatarRightPositionOffset;
                    avRight.localPosition = Quaternion.Inverse(myCameraOffset.rotation) * (myXRRH.position - myCameraOffset.position);
                    avRight.rotation = myXRRH.rotation * avatarRightRotationOffset;

                    //Fingers
                    LinkAvtFingersToXROrigin(AvatRightHandFingers, XRigRightHandFingers, avRight);

                    //if (rightHasController) AvatarTurnOnOffRightHandIK(1);
                    //rightHasController = false;
                }
                break;

            case XRInputModalityManager.InputMode.None:
                break;
        }

        if (avHead)
        {
            //avHead.position = myXRCam.position + avatarHeadPositionOffset;
            avHead.rotation = myXRCam.rotation * avatarHeadRotationOffset;
        }

        if (avBody)
        {
            //avBody.position = avHead.position + new Vector3(0, -0.5f, 0);
            avBody.position = myXRRig.transform.position + cameraPOffset;
            avBody.rotation = myXRRig.transform.rotation;
        }
    }

    void MapHands()
    {
        //XR Origin - LEFT
        XRigLeftHandFingers[0] = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("IndexTip").transform;
        XRigLeftHandFingers[1] = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("MiddleTip").transform;
        XRigLeftHandFingers[2] = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("ThumbTip").transform;
        XRigLeftHandFingers[3] = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("RingTip").transform;
        XRigLeftHandFingers[4] = GameObject.Find("LeftHandDebugDrawJoints").transform.Find("LittleTip").transform;

        //XR Origin - RIGHT
        XRigRightHandFingers[0] = GameObject.Find("RightHandDebugDrawJoints").transform.Find("IndexTip").transform;
        XRigRightHandFingers[1] = GameObject.Find("RightHandDebugDrawJoints").transform.Find("MiddleTip").transform;
        XRigRightHandFingers[2] = GameObject.Find("RightHandDebugDrawJoints").transform.Find("ThumbTip").transform;
        XRigRightHandFingers[3] = GameObject.Find("RightHandDebugDrawJoints").transform.Find("RingTip").transform;
        XRigRightHandFingers[4] = GameObject.Find("RightHandDebugDrawJoints").transform.Find("LittleTip").transform;

        //Avatar - LEFT
        AvatLeftHandFingers[0] = transform.Find("XR IK Rig").Find("Left Index Finger IK").Find("Left Index Finger IK_target").transform;
        AvatLeftHandFingers[1] = transform.Find("XR IK Rig").Find("Left Middle Finger IK").Find("Left Middle Finger IK_target").transform;
        AvatLeftHandFingers[2] = transform.Find("XR IK Rig").Find("Left Thumb IK").Find("Left Thumb IK_target").transform;
        AvatLeftHandFingers[3] = transform.Find("XR IK Rig").Find("Left Ring Finger IK").Find("Left Ring Finger IK_target").transform;
        AvatLeftHandFingers[4] = transform.Find("XR IK Rig").Find("Left Pinky Finger IK").Find("Left Pinky Finger IK_target").transform;

        //Avatar - LEFT
        AvatRightHandFingers[0] = transform.Find("XR IK Rig").Find("Right Index Finger IK").Find("Right Index Finger IK_target").transform;
        AvatRightHandFingers[1] = transform.Find("XR IK Rig").Find("Right Middle Finger IK").Find("Right Middle Finger IK_target").transform;
        AvatRightHandFingers[2] = transform.Find("XR IK Rig").Find("Right Thumb IK").Find("Right Thumb IK_target").transform;
        AvatRightHandFingers[3] = transform.Find("XR IK Rig").Find("Right Ring Finger IK").Find("Right Ring Finger IK_target").transform;
        AvatRightHandFingers[4] = transform.Find("XR IK Rig").Find("Right Pinky Finger IK").Find("Right Pinky Finger IK_target").transform;

        leftPalmBone = transform.Find("XR IK Rig").Find("Left Index Finger IK").GetComponent<ChainIKConstraint>().data.root.parent;
        rightPalmBone = transform.Find("XR IK Rig").Find("Right Index Finger IK").GetComponent<ChainIKConstraint>().data.root.parent;
        
    }

    void LinkAvtFingersToXROrigin(Transform[] avatarBone, Transform[] XRBone, Transform avatarHand)
    {
        for (int i = 0; i < 5; i++) //for each finger
        {
            if (avatarHand == avLeft)  avatarBone[i].position = leftPalmBone.position  + (XRBone[i].position - myXRLH.position);
            if (avatarHand == avRight) avatarBone[i].position = rightPalmBone.position + (XRBone[i].position - myXRRH.position);

            avatarBone[i].rotation = avatarHand.rotation;
        }
    }

    /* void AvatarTurnOnOffLeftHandIK (float val)
    {
        for (int i = 0; i < 5; i++)
            AvatLeftHandFingers[i].parent.GetComponent<ChainIKConstraint>().weight = val;
    }

    void AvatarTurnOnOffRightHandIK(float val)
    {
        for (int i = 0; i < 5; i++)
            AvatRightHandFingers[i].parent.GetComponent<ChainIKConstraint>().weight = val;
    } */

    void AvatarTurnOnOffHandsIK(float val)
    {
        foreach (var x in transform.Find("XR IK Rig").GetComponentsInChildren<ChainIKConstraint>())
        {
            x.weight = val;
        }
    }
}
