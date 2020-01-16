﻿using OVRTouchSample;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


//Extension de la classe Hand. 
public class Grabber : Hand
{

    [Header("Hand Vars")]
    //public Animator handAnimator;
    //public HandPose handPose;//What is this?
    private Rigidbody rb;
    private InteractObject curentPointed;
    private GameObject currentPointedObject;
    private GrabbableObject currentGrabbable;
    private GameObject currentGrabbableObject;
    private Collider[] handCollider;
    private Collider playerCollider;
    public const float THRESH_COLLISION_FLEX = 0.9f;
    //Sketch
    List<Renderer> m_showAfterInputFocusAcquired;

    public float grabberRadius;
    private bool m_collisionEnabled = true;

    static private float m_collisionScaleCurrent = 0.0f;

    public const float COLLIDER_SCALE_MIN = 0.01f;
    public const float COLLIDER_SCALE_MAX = 1.0f;
    public const float COLLIDER_SCALE_PER_SECOND = 1.0f;
    public const float DISTANCE_GRAB_RANGE_MIN = 0.2f;

    private Ray ray;

    #region OVRGrabber
    //Values of input Hand Trigger that is consider a grab
    public float grabBegin = 0.55f;
    public float grabEnd = 0.35f;


    bool alreadyUpdated = false;

    // Child/attached transforms of the grabber, indicating where to snap held objects to (if you snap them).
    // Also used for ranking grab targets in case of multiple candidates.
    [SerializeField]
    protected Transform m_gripTransform = null;
    // Child/attached Colliders to detect candidate grabbable objects.
    [SerializeField]
    protected Collider[] m_grabVolumes = null;

    [SerializeField]
    protected Transform m_parentTransform;

    [SerializeField]
    protected GameObject m_player;
    [SerializeField]
    protected GameObject headAnchor;
    Renderer[] renderers;
    protected bool m_grabVolumeEnabled = true;
    protected Vector3 m_lastPos;
    protected Quaternion m_lastRot;
    protected Quaternion m_anchorOffsetRotation;
    protected Vector3 m_anchorOffsetPosition;
    protected float m_prevFlex;
    public GrabbableObject m_grabbedObj = null;
    protected Vector3 m_grabbedObjectPosOff;
    protected Quaternion m_grabbedObjectRotOff;
    protected List<GrabbableObject> grabCandidates;
    /// <summary>
    /// The currently grabbed object.
    /// </summary>
    public GrabbableObject grabbedObject
    {
        get { return m_grabbedObj; }
    }

    public void ForceRelease(GrabbableObject grabbable)
    {
        bool canRelease = (
            (m_grabbedObj != null) &&
            (m_grabbedObj == grabbable)
        );
        if (canRelease)
        {
            GrabEnd();
        }
    }

    protected virtual void Awake()
    {
        grabCandidates = new List<GrabbableObject>();
        m_showAfterInputFocusAcquired = new List<Renderer>();
    }

    override protected void Start()
    {
        base.Start();

        #region Awake
        controller = (OVRInput.Controller)handside;
        inputs = InputManager.Instance.inputs.Touch;
        handCollider = this.GetComponentsInChildren<Collider>().Where(childCollider => !childCollider.isTrigger).ToArray();

        m_anchorOffsetPosition = transform.localPosition;
        m_anchorOffsetRotation = transform.localRotation;
        // If we are being used with an OVRCameraRig, let it drive input updates, which may come from Update or FixedUpdate.
        OVRCameraRig rig = transform.GetComponentInParent<OVRCameraRig>();
        if (rig != null)
        {
            rig.UpdatedAnchors += (r) => { OnUpdatedAnchors(); };
        }

        renderers = GetComponentsInChildren<Renderer>();
        rb = GetComponent<Rigidbody>();
        //m_parentTransform = transform.parent;
        if (m_player) playerCollider = m_player.GetComponent<Collider>();
        #endregion


        m_lastPos = transform.position;
        m_lastRot = transform.rotation;

        #region Start


        //Vars
        currentPointedObject = null;
        curentPointed = null;
        currentGrabbableObject = null;

        //Get/Set
        //CollisionEnable(false);

        //Events
        OVRManager.InputFocusAcquired += OnInputFocusAcquired;
        OVRManager.InputFocusLost += OnInputFocusLost;
        #endregion

    }

    public void Update()
    {
        base.Update();
        alreadyUpdated = false;
        //TODO variable Grabber grabbedObject
        bool collisionEnabled = GetGrabbedObject() == null && flex >= THRESH_COLLISION_FLEX;
        CollisionEnable(collisionEnabled);
    }


    public void DistanceGrabBegin()
    {
        ray.origin = transform.position;
        ray.direction = transform.forward;
        RaycastHit rayHit;

        if (Physics.SphereCast(transform.position, 1f, transform.forward, out rayHit, 10, LayerMask.GetMask("Interact"))) //TODO Change layer to fit name
        {
            Debug.Log("ASDASDASDaSDASDaSD");
            //Check if the gameObject is a GrabbableObject
            if (!Main.Instance.grabbableObjects.ContainsKey(rayHit.transform.gameObject)) return;

            //Check if the gameObject wants to be distance grabbed
            Debug.Log(Main.Instance.grabbableObjects[rayHit.transform.gameObject].distanceGrab);
            if (Main.Instance.grabbableObjects[rayHit.transform.gameObject].distanceGrab != true) return;

            //Check if the gameObject is not in the distance grab range
            Debug.Log(Vector3.Distance(headAnchor.transform.position, rayHit.transform.position));
            Debug.Log(Vector3.Distance(headAnchor.transform.position, rayHit.transform.position ) < DISTANCE_GRAB_RANGE_MIN);
            if (Vector3.Distance(headAnchor.transform.position, rayHit.transform.position) < DISTANCE_GRAB_RANGE_MIN) return;
            //Check if the gameObject is too far
            if (Vector3.Distance(headAnchor.transform.position, rayHit.transform.position) > Main.Instance.grabbableObjects[rayHit.transform.gameObject].distanceRange) return;

            m_grabbedObj = Main.Instance.grabbableObjects[rayHit.transform.gameObject];
            m_grabbedObj.GrabBegin(this, handCollider[0]);

        }


    }


    // Hands follow the touch anchors by calling MovePosition each frame to reach the anchor.
    // This is done instead of parenting to achieve workable physics. If you don't require physics on
    // your hands or held objects, you may wish to switch to parenting.
    void OnUpdatedAnchors()
    {
        // Don't want to MovePosition multiple times in a frame, as it causes high judder in conjunction
        // with the hand position prediction in the runtime.
        if (alreadyUpdated) return;
        alreadyUpdated = true;

        Vector3 destPos = m_parentTransform.TransformPoint(m_anchorOffsetPosition);
        Quaternion destRot = m_parentTransform.rotation * m_anchorOffsetRotation;

        rb.MovePosition(destPos);
        rb.MoveRotation(destRot);

        MoveGrabbedObject(destPos, destRot);


        m_lastPos = transform.position;
        m_lastRot = transform.rotation;

        float prevFlex = m_prevFlex;
        // Update values from inputs
        m_prevFlex = inputs[controller].HandTrigger;

        CheckForGrabOrRelease(prevFlex);
    }

    void OnDestroy()
    {
        if (m_grabbedObj != null)
        {
            GrabEnd();
        }
    }

    void OnTriggerEnter(Collider otherCollider)
    {
        // Get the grab trigger
        if (!Main.Instance.grabbableObjects.ContainsKey(otherCollider.gameObject)) return;
        if (grabCandidates.Contains(Main.Instance.grabbableObjects[otherCollider.gameObject])) return;
        grabCandidates.Add(Main.Instance.grabbableObjects[otherCollider.gameObject]);

    }

    void OnTriggerExit(Collider otherCollider)
    {
        if (!Main.Instance.grabbableObjects.ContainsKey(otherCollider.gameObject)) return;
        if (!grabCandidates.Contains(Main.Instance.grabbableObjects[otherCollider.gameObject])) return;

        grabCandidates.Remove(Main.Instance.grabbableObjects[otherCollider.gameObject]);
    }

    protected void CheckForGrabOrRelease(float prevFlex)
    {
        Debug.DrawLine(transform.position, transform.position + (transform.forward * 10), Color.red);

        if ((m_prevFlex >= grabBegin) && (prevFlex < grabBegin))
        {
            if (grabCandidates.Count != 0)
                GrabBegin();
            else
                DistanceGrabBegin();
        }
        else if ((m_prevFlex <= grabEnd) && (prevFlex > grabEnd))
        {
            GrabEnd();
        }
    }

    protected virtual void GrabBegin()
    {
        float closestDistance = float.MaxValue;
        GrabbableObject closestGrabbable = null;
        Collider closestGrabbableCollider = null;

        // Iterate grab candidates and find the closest grabbable candidate
        foreach (GrabbableObject grabbable in grabCandidates)
        {
            bool canGrab = !(grabbable.isGrabbed && !grabbable.allowOffhandGrab);
            if (!canGrab)
            {
                continue;
            }

            for (int j = 0; j < grabbable.grabPoints.Length; ++j)
            {
                Collider grabbableCollider = grabbable.grabPoints[j];
                // Store the closest grabbable
                Vector3 closestPointOnBounds = grabbableCollider.ClosestPointOnBounds(m_gripTransform.position);
                float distance = Vector3.Distance(m_gripTransform.position, closestPointOnBounds);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestGrabbable = grabbable;
                    closestGrabbableCollider = grabbableCollider;
                }
            }
        }

        // Disable grab volumes to prevent overlaps
        GrabVolumeEnable(false);

        if (closestGrabbable != null)
        {

            if (closestGrabbable.isGrabbed)
            {
                closestGrabbable.grabbedBy.OffhandGrabbed(closestGrabbable);
            }

            m_grabbedObj = closestGrabbable;
            m_grabbedObj.GrabBegin(this, closestGrabbableCollider);
            HandPose customPose = m_grabbedObj.GetComponent<HandPose>() ?? defaultHandPose;

            m_lastPos = transform.position;
            m_lastRot = transform.rotation;

            // Set up offsets for grabbed object desired position relative to hand.
            if (m_grabbedObj.snapPosition)
            {
                m_grabbedObjectPosOff = m_gripTransform.localPosition;
                if (m_grabbedObj.snapOffset)
                {
                    Vector3 snapOffset = m_grabbedObj.snapOffset.position;
                    if (controller == OVRInput.Controller.LTouch) snapOffset.x = -snapOffset.x;
                    m_grabbedObjectPosOff += snapOffset;
                }
            }
            else
            {
                Vector3 relPos = m_grabbedObj.transform.position - transform.position;
                relPos = Quaternion.Inverse(transform.rotation) * relPos;
                m_grabbedObjectPosOff = relPos;
            }

            if (m_grabbedObj.snapOrientation)
            {
                m_grabbedObjectRotOff = m_gripTransform.localRotation;
                if (m_grabbedObj.snapOffset)
                {
                    m_grabbedObjectRotOff = m_grabbedObj.snapOffset.rotation * m_grabbedObjectRotOff;
                }
            }
            else
            {
                Quaternion relOri = Quaternion.Inverse(transform.rotation) * m_grabbedObj.transform.rotation;
                m_grabbedObjectRotOff = relOri;
            }

            // Note: force teleport on grab, to avoid high-speed travel to dest which hits a lot of other objects at high
            // speed and sends them flying. The grabbed object may still teleport inside of other objects, but fixing that
            // is beyond the scope of this demo.
            MoveGrabbedObject(m_lastPos, m_lastRot, true);
            SetPlayerIgnoreCollision(m_grabbedObj.gameObject, true);

        }


    }

    protected virtual void MoveGrabbedObject(Vector3 pos, Quaternion rot, bool forceTeleport = false)
    {
        if (m_grabbedObj == null)
        {
            return;
        }

        Rigidbody grabbedRigidbody = m_grabbedObj.rb;
        Vector3 grabbablePosition = pos + rot * m_grabbedObjectPosOff;
        Quaternion grabbableRotation = rot * m_grabbedObjectRotOff;

        if (forceTeleport)
        {
            grabbedRigidbody.transform.position = grabbablePosition;
            grabbedRigidbody.transform.rotation = grabbableRotation;
        }
        else
        {
            grabbedRigidbody.MovePosition(grabbablePosition);
            grabbedRigidbody.MoveRotation(grabbableRotation);
        }
    }

    protected void GrabEnd()
    {
        if (m_grabbedObj != null)
        {
            OVRPose localPose = new OVRPose { position = OVRInput.GetLocalControllerPosition(controller), orientation = OVRInput.GetLocalControllerRotation(controller) };
            OVRPose offsetPose = new OVRPose { position = m_anchorOffsetPosition, orientation = m_anchorOffsetRotation };
            localPose = localPose * offsetPose;

            OVRPose trackingSpace = transform.ToOVRPose() * localPose.Inverse();
            Vector3 linearVelocity = trackingSpace.orientation * OVRInput.GetLocalControllerVelocity(controller);
            Vector3 angularVelocity = trackingSpace.orientation * OVRInput.GetLocalControllerAngularVelocity(controller);

            GrabbableRelease(linearVelocity, angularVelocity);
            currentPose = defaultHandPose;
        }

        // Re-enable grab volumes to allow overlap events
        GrabVolumeEnable(true);
    }

    protected void GrabbableRelease(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        m_grabbedObj.GrabEnd(linearVelocity, angularVelocity);
        //if (m_parentHeldObject) m_grabbedObj.transform.parent = null;
        SetPlayerIgnoreCollision(m_grabbedObj.gameObject, false);
        m_grabbedObj = null;
    }

    protected virtual void GrabVolumeEnable(bool enabled)
    {
        if (m_grabVolumeEnabled == enabled)
        {
            return;
        }

        m_grabVolumeEnabled = enabled;
        for (int i = 0; i < m_grabVolumes.Length; ++i)
        {
            Collider grabVolume = m_grabVolumes[i];
            grabVolume.enabled = m_grabVolumeEnabled;
        }

        if (!m_grabVolumeEnabled)
        {
            grabCandidates.Clear();
        }
    }

    protected virtual void OffhandGrabbed(GrabbableObject grabbable)
    {
        if (m_grabbedObj == grabbable)
        {
            GrabbableRelease(Vector3.zero, Vector3.zero);
        }
    }

    protected void SetPlayerIgnoreCollision(GameObject grabbable, bool ignore)
    {
        if (m_player != null)
        {
            if (playerCollider != null)
            {
                Collider[] colliders = grabbable.GetComponents<Collider>();
                foreach (Collider c in colliders)
                {
                    Physics.IgnoreCollision(c, playerCollider, ignore);
                }
            }
        }
    }
    #endregion





    public GameObject GetGrabbedObject()
    {
        return currentGrabbableObject;
    }

    private void UpdateCapTouchStates()
    {
        //TODO Near touch inputPkg
        //Pointing is always false. Because no implementation in Grabber.
        //Same thing for GivingThumbsUp
        //m_isPointing = !OVRInput.Get(OVRInput.NearTouch.PrimaryIndexTrigger, m_controller);
        //m_isGivingThumbsUp = !OVRInput.Get(OVRInput.NearTouch.PrimaryThumbButtons, m_controller);

        //inputPkg.Touch[controller].IndexTrigger;
    }

    private void OnInputFocusAcquired()
    {
        if (m_restoreOnInputAcquired)
        {
            for (int i = 0; i < m_showAfterInputFocusAcquired.Count; ++i)
            {
                if (m_showAfterInputFocusAcquired[i])
                {
                    m_showAfterInputFocusAcquired[i].enabled = true;
                }
            }
            m_showAfterInputFocusAcquired.Clear();

            // Update function will update this flag appropriately. Do not set it to a potentially incorrect value here.
            //CollisionEnable(true);

            m_restoreOnInputAcquired = false;
        }
    }
    protected void LateUpdate()
    {
        // Hand's collision grows over a short amount of time on enable, rather than snapping to on, to help somewhat with interpenetration issues.
        if (m_collisionEnabled && m_collisionScaleCurrent + Mathf.Epsilon < COLLIDER_SCALE_MAX)
        {
            m_collisionScaleCurrent = Mathf.Min(COLLIDER_SCALE_MAX, m_collisionScaleCurrent + Time.deltaTime * COLLIDER_SCALE_PER_SECOND);
            for (int i = 0; i < handCollider.Length; ++i)
            {
                Collider collider = handCollider[i];
                collider.transform.localScale = new Vector3(m_collisionScaleCurrent, m_collisionScaleCurrent, m_collisionScaleCurrent);
            }
        }
    }

    public void castRay()
    {
        ray.origin = transform.position;
        ray.direction = transform.forward;
        RaycastHit rayHit;
        if (Physics.SphereCast(transform.position, 1f, transform.forward, out rayHit, grabberRadius, 1 << 8)) //TODO Change layer to fit name
        {

            if (currentPointedObject != rayHit.transform.gameObject)
            {
                if (curentPointed != null) curentPointed.Pointed = false;
                currentPointedObject = rayHit.transform.gameObject;
                curentPointed = rayHit.transform.GetComponent<InteractObject>();
                if (curentPointed)
                    curentPointed.Pointed = true;
            }
            if (InputManager.Instance.inputs.Touch[controller].HandTrigger > 0.5f)
            {
                currentGrabbable = rayHit.transform.GetComponent<GrabbableObject>();
                currentGrabbableObject = rayHit.transform.gameObject;
                currentGrabbable.Selected = true;
            }


        }
        else if (curentPointed)
        {
            curentPointed.Pointed = false;
            currentPointedObject = null;
            curentPointed = null;
        }


    }
    public void CollisionEnable(bool enabled)
    {
        if (m_collisionEnabled == enabled)
        {
            return;
        }
        m_collisionEnabled = enabled;


        for (int i = 0; i < handCollider.Length; ++i)
        {
            handCollider[i].enabled = enabled;
        }
        //if (enabled)
        //{
        //    m_collisionScaleCurrent = COLLIDER_SCALE_MIN;
        //    for (int i = 0; i < handCollider.Length; ++i)
        //    {
        //        Collider collider = handCollider[i];
        //        collider.transform.localScale = new Vector3(COLLIDER_SCALE_MIN, COLLIDER_SCALE_MIN, COLLIDER_SCALE_MIN);
        //        collider.enabled = true;
        //    }
        //}
        //else
        //{
        //    m_collisionScaleCurrent = COLLIDER_SCALE_MAX;
        //    for (int i = 0; i < handCollider.Length; ++i)
        //    {
        //        Collider collider = handCollider[i];
        //        collider.enabled = false;
        //        collider.transform.localScale = new Vector3(COLLIDER_SCALE_MIN, COLLIDER_SCALE_MIN, COLLIDER_SCALE_MIN);
        //    }
        //}
    }

    private void OnInputFocusLost()
    {
        if (gameObject.activeInHierarchy)
        {
            m_showAfterInputFocusAcquired.Clear();

            for (int i = 0; i < renderers.Length; ++i)
            {
                if (renderers[i].enabled)
                {
                    renderers[i].enabled = false;
                    m_showAfterInputFocusAcquired.Add(renderers[i]);
                }
            }

            CollisionEnable(false);

            m_restoreOnInputAcquired = true;
        }
    }

}
