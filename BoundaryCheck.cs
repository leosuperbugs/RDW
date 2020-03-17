using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoundaryCheck : MonoBehaviour
{
    private OVRBoundary rawBoundary;
    private Vector3[] boundaryPoints;
    // the four turning points of the virtual track, position based on boundary
    public Vector3[] turningPoints;
    private Vector3 boundarySize;
    // the position of head
    public Transform head;

    private const float DIMENX = 4.0f; 
    private const float DIMENY = 4.0f;
    private const float DIMEN = 3.0f;
    private const float CURV_DEG = 9.5f;
    public const int NUM_OF_BOUNDARY_POINTS = 4;
    private const float CLOSE_DIST = 0.2f;
    private Vector3 ARROW_HEIGHT = Vector3.up;

    // the direction from point 0 to point 1
    private Vector3 forward;
    // the direction from point 1 to point 2
    private Vector3 next;

    private GameObject arrow;
    private InitWayPointer wayPointer;
    public GameObject world;
    public GameObject cameraObject;
    private RDW rdw;

    // do sth against the auto alignment
    private Vector3 cameraPostion;
    private Vector3 cameraDirection;

    private bool isInitOn = true;
    private List<GameObject> initWayPointers;

    // Start is called before the first frame update
    void Start()
    {
        // Detect all the boundaries
        rawBoundary = OVRManager.boundary;
        boundaryPoints = rawBoundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
        boundarySize = rawBoundary.GetDimensions(OVRBoundary.BoundaryType.PlayArea);
        // shrink the boundary to sth like 3m * 3m space

        // get camera postion and directions
        OVRCameraRig rig = cameraObject.GetComponent<OVRCameraRig>();
        cameraPostion = rig.trackingSpace.position;
        cameraDirection = rig.trackingSpace.rotation * new Vector3(1.0f, 1.0f, 1.0f);

        // Make the world disappear until the user walks to the way pointer
        SetObjInvisible(world, false);

        // Get RDWTest component
        rdw = cameraObject.GetComponent<RDW>();

        initWayPointers = new List<GameObject>();
        FetchInitWayPointers();
    }

    private void Awake()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        // flip the system to mode isInitOn
        //if(OVRInput.GetDown(OVRInput.Button.One) && !isInitOn)
        //{
        //    isInitOn = true;
        //    ShowWayPointers();
        //    SetObjInvisible(world, false);
        //}

        updateTrack();
    }

    private void LateUpdate()
    {
        // detect the distance of our user and four way pointers
        if (isInitOn)
        {
            for (int i = 0; i < NUM_OF_BOUNDARY_POINTS; ++i)
            {
                GameObject obj = FindInitWayPointer(i);
                // If we get close to the way pointer,
                // which means we are close
                if (obj)
                {
                    if (Vector3.Distance(FlattenedDir3D(obj.transform.localPosition),FlattenedDir3D(this.head.localPosition)) < CLOSE_DIST)
                    {
                        // Delete all way pointers
                        // DeleteWayPointers();
                        // Align the world to the direction of our user
                        Transform wayPointerTrans = obj.transform;
                        world.transform.localPosition = ProcessPosForInit(wayPointerTrans.localPosition - new Vector3(0, -0.5f, 0));
                        world.transform.localRotation = Quaternion.LookRotation(ProcessDirForInit(
                            wayPointerTrans.localRotation * Vector3.forward
                            )); // is this correct?

                        // We want to get the index of the starting point
                        rdw.startingPoint = i;

                        // We also need to make sure if our user is in the circle of turning
                        float distanceToTarget = Vector3.Distance(FlattenedDir3D(head.transform.localPosition), FlattenedDir3D(turningPoints[i]));
                        if (distanceToTarget < RDW.CORNER_ENTER) {
                            rdw.isBeforeInside = true;	
						} else {
                            rdw.isBeforeInside = false;	
						}

                        // Show the world
                        SetObjInvisible(world, true);
                        isInitOn = false;
                        rdw.isRDWOn = true;
                        break;
                    }
                }
            }
        }
    }

    // Making way pointers invisible
    private void DeleteWayPointers()
    {
        for (int i = 0; i < NUM_OF_BOUNDARY_POINTS; ++i)
        {
            GameObject obj = initWayPointers[i];
            SetObjInvisible(obj, false);
        }
    }

    private void ShowWayPointers()
    {
        for (int i = 0; i < NUM_OF_BOUNDARY_POINTS; ++i)
        {
            GameObject obj = initWayPointers[i];
            SetObjInvisible(obj, false);
        }
    }

    /// <summary>
    /// Checks the size of the boundary.
    /// </summary>
    /// <returns><c>true</c>, if boundary size was checked, <c>false</c> otherwise.</returns>
    //private bool CheckBoundarySize()
    //{
    //    if (boundarySize.x >= DIMENX && boundarySize.y >= DIMENY)
    //    {
    //        return true;
    //    }
    //    else
    //    {
    //        return false;
    //    }
    //}

    /// <summary>
    /// Calculates the initial direction.
    /// </summary>
    /// <returns>The initial direction.</returns>
    private Vector3 CalculateInitialDirection()
    {
        float theta = CURV_DEG;
        float radians = theta * (Mathf.PI / 180);
        float tan = Mathf.Tan(radians);

        // the direction where people should point to at the beginning
        Vector3 initDir = forward.normalized - tan * next.normalized;

        return initDir;
    }

    private void SetPosOfInitArrow(int firstPoint)
    {
        if (firstPoint >= 4 && firstPoint < 0) return;

        // we need the next 2 points so that we can get initial positions of the arrow
        int secondPoint = (firstPoint + 1) % 4;
        int thirdPoint = (secondPoint + 1) % 4;

        forward = boundaryPoints[secondPoint] - boundaryPoints[firstPoint];
        next = boundaryPoints[thirdPoint] - boundaryPoints[secondPoint];
        // We should check if we have enough space
        if (forward.magnitude < DIMEN || next.magnitude < DIMEN)
        {
            Debug.Log("No enough space");
            return;
        }

        //Vector3 originForward = forward;
        //Vector3 originNext = next;
        //forward = forward.normalized;
        //next = next.normalized;

        // shrink the space so that our users won't collide with the boundary
        Vector3 wayPointerPosition = boundaryPoints[firstPoint] +
            (forward.magnitude - DIMEN) / 2 * forward.normalized +
            (next.magnitude - DIMEN) / 2 * next.normalized;
        turningPoints[firstPoint] = wayPointerPosition;

        arrow = FindInitWayPointer(firstPoint);
        wayPointer = arrow.GetComponent<InitWayPointer>();
        if (wayPointer)
        {
            // set the initial position and direction of the starting way pointer
            wayPointer.setInitPosition(wayPointerPosition, CalculateInitialDirection());
        }
    }

    private GameObject FindInitWayPointer(int firstPoint)
    {
        return initWayPointers[firstPoint];
    }

    private void FetchInitWayPointers()
    {
        // get all way pointers
        for (int i = 0; i < NUM_OF_BOUNDARY_POINTS; ++i)
        {
            initWayPointers.Add(GameObject.Find("InitWayPointer" + i.ToString()));
        }
    }

    private void updateTrack()
    {
        for (int i = 0; i < NUM_OF_BOUNDARY_POINTS; ++i)
        {
            SetPosOfInitArrow(i);
        }
    }

    public static Vector3 ForwardDir3D(Vector3 vec)
    {
        return (new Vector3(vec.x, 0, 0));
    }

    public static Vector3 FlattenedDir3D(Vector3 vec)
    {
        return (new Vector3(vec.x, 0, vec.z));
    }

    // Doing sth tricky
    // The -vec.z is caused by the set up of the virtural environment. Should fix this later
    public static Vector3 ProcessDirForInit(Vector3 vec)
    {
        return (new Vector3(vec.x, 0, vec.z));
    }

    public static Vector3 ProcessPosForInit(Vector3 vec)
    {
        // some magic numbers here
        return new Vector3(vec.x, vec.y, vec.z);
    }

    private static void SetObjInvisible(GameObject obj, bool enabled)
    {
        obj.SetActive(enabled);
    }


}
