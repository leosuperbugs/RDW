using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Redirection;

struct MinDistInfo
{
    public float minDist;
    // the target that our user has approached
    public int id;
}

public class RDW : MonoBehaviour
{
    public int redirectType = (int)RedirectType.Translation;
    public bool isRDWOn = false;
    public Transform head;
    public Transform worldTrans;
    public Canvas canvas;
    public Text rotationGainText;
    public Text curvatureGainText;
    public Text lackText;
    public Text targetText;
    public Text distText;
    public Text radiText;
    public Text transGainText;
    public GameObject Center;
    // Some context information for RDW and counter deviation
    public GameObject start;
    // The position of the corners
    public List<Transform> corners = new List<Transform>();
    // the index of the starting point where user start walking
    public int startingPoint;
    // record if user is inside of corner, is true if user is inside
    public bool isBeforeInside;
    // only within CORNER_XXXXX m can we take user as being inside the corner
    // using two different value to avoid shaking 
    public const float CORNER_ENTER = 0.45f;
    public const float CORNER_LEAVE = 0.5f;
    public const float VCORNER_BOUND = 0.4f;

    // The math:
    // Rotation gain: 60 * (1 - 1 / 0.8) = 15 deg
    // As a result, curvature gain should make up for the rest, 
    // which is 15 deg
    //
    // If rotation gain didn't perform enough 
    // then curvature gain can do some more, vice versa
    enum RedirectType { Translation, Rotation, Curvature };
    private const int NUM_OF_CORNERS = 6;
    private const float PART_ROTATION = 13.0f;
    private const float PART_CURVATURE = 17.0f;
    private const float rotationGainRate = 0.78f; // actual multiplier
    private const float ROTATION_THRESHOLD = 0.5f; // degrees per second
    private const float TRANSLATION_UP_THRESHOLD = 0.20f; // up scaling
    private const float TRANSLATION_DOWN_THRESHOLD = 0.10f; // down scaling
    private const float PATH_LENGTH = 3.0f; // the length of each virtual path
    // min radius
    private const float CURV_GAIN_THRES = 7.9f;
    // meters per second
    private const float MOVEMENT_THRESHOLD = 0.05f;

    private Vector3 deltaPos;
    private Vector3 currPos;
    private Vector3 prevPos;
    private Vector3 prevDir;
    private Vector3 currDir;

    private float deltaDir;
    // radius applied by curvature gain
    private float curvatureGainRate = 9.5f;
    //Proposed curvature gain based on user speed
    private float rotationFromCurvatureGain;


    private float timer;
    // if is in transit, we should use timer
    private bool isInTransit = false;
    // Time that we transit between the two gains
    private const float TRANSIT_TIME = 0.4f;
    // The lack of gain. E.g : when rotation gain only accumulated to 10 deg, the lack is 5deg ( it should be 15 degs )
    private float lackDegs = 0.0f;
    // It is possible that we exit the circle at the beginning, we don't need to calculate lack at that point
    private bool isFirstInsideOut = true;
    // The same to entering the circle
    private bool isFirstOutsideIn = true;
    // The coefficient that we use to determine if we should take the first turning into account when calculating 'lack'
    private const float lackCountThreshold = 0.40f;
    // accumulated gain in degrees
    private float rotationGainInDeg = 0.0f;
    private float curvatureGainInDeg = 0.0f;
    private float transit = 0.0f;

    // four apexes of the 3m * 3m triangles
    private Vector3[] turningPoints;
    // the index of the target point in physical space
    private int currentTarget = -1;
    // the relative position from the current target to turning center
    private Vector3 turningCenterRelativePos = new Vector3(0, 0, 0);
    // the relative position from the previous target to turning center
    private Vector3 turningCenterPrevRelaPos = new Vector3(0, 0, 0);
    // the circle center of current curvature gain
    private Vector3 turningCenter = new Vector3(0, 0, 0);
    private const float BIAS_THRESHOLD = 0.5f;
    //private const float INF_RADIUS = float.MaxValue;
    // the index of the target point in virtual space
    private int currentVirtTarget = 0;


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // Always the first thing to do before RDW
        UpdateCurrData();
        // Some lazy init stuff
        UpdateInit();
        if (IsRDWOn()) transit = Transit();
        RotationGain();
    }

    void LateUpdate()
    {
        LocomotionInit();
        CurvatureGain();
        TranslationGain();
        ChangeCanvasPos();
        InfoDump();
        // Always the last thing to do
        UpdatePrevData();
    }

    private void UpdateInit() {
        LocomotionInit();
        if (turningPoints == null) {
            turningPoints = start.GetComponent<BoundaryCheck>().turningPoints;
        }
        if (turningPoints != null && turningCenterRelativePos != null) {
            turningCenter = turningPoints[currentTarget] + turningCenterRelativePos;
            Center.transform.localPosition = turningCenter;
        }
    }
     
    
    private void LocomotionInit() { 
        if (startingPoint >= 0 && currentTarget < 0) {
            // for physical space
            currentTarget = startingPoint;
			CalcCurvatureRadius();
            turningCenterRelativePos = CalcTurningCenterRelativePos(startingPoint);
            currentTarget = GetNextTarget(currentTarget);
			targetText.text = currentTarget.ToString();
            prevPos = HeadPosition();
            prevDir = worldTrans.forward;
            // for virtual world
            currentVirtTarget = GetNextVirtualTarget(currentVirtTarget);
        }
    }

    private void ChangeCanvasPos() {
        // change canvas position
        Vector3 front = head.transform.forward;
        canvas.transform.position = head.position + front.normalized * 1.5f;
        canvas.transform.eulerAngles = new Vector3(head.transform.eulerAngles.x, head.transform.eulerAngles.y, head.transform.eulerAngles.z);
    }

    private void InfoDump() {
        // Info dump
        rotationGainText.text = "RotationGain:" + rotationGainInDeg.ToString();
        curvatureGainText.text = "CurvatureGain:" + curvatureGainInDeg.ToString();

        // Info dump
        if (lackText == null) lackText = GameObject.Find("TextLack").GetComponent<Text>();
        lackText.text = "Lack:" + lackDegs.ToString();
    }

    private MinDistInfo FindClosestPoint(Vector3[] points, Vector3 position) {
        float minDist = 999.0f;
        int id = 0;
        // the target that our user has approached
        int i = 0;

        foreach (Vector3 corner in points)
        {
            // is now inside vs. is before inside
            // I think we should use physical pos here
            float distance = Vector3.Distance(FlattenedDir3D(corner), FlattenedDir3D(position));
            if (distance < minDist) { minDist = distance; id = i; }
            ++i;
        }

        MinDistInfo info = new MinDistInfo();
        info.minDist = minDist;
        //info.min = turningPoints[id];
        info.id = id;
        return info;
    }
    // detect if user is close to the corner
    // returns 0 if our user is out of corner
    // returns 1 if our user is fully inside the corner
    private float Transit()
    {
        // first check if user is in corner
        if (!isInTransit)
        {
            MinDistInfo info = FindClosestPoint(turningPoints, head.localPosition);
            float minDist = info.minDist;
            int id = info.id;
            //min = info.min;

            if (minDist <= CORNER_ENTER && isBeforeInside == false)
            {
                // outside in
                GoFromOutsideIn();

            }
            else if (minDist >= CORNER_LEAVE && isBeforeInside == true)
            {
                // inside out
                GoFromInsideOut();

                // re-calculate radius
                CalcCurvatureRadius();
                turningCenterRelativePos = CalcTurningCenterRelativePos(id);
                // change to next target
                currentTarget = GetNextTarget(id);
                targetText.text = currentTarget.ToString();
            }

            // gather all points as a vector
            Vector3[] virtTargets = TransformToPos(corners);
            // Approaching next virtual target
            MinDistInfo vinfo = FindClosestPoint(virtTargets, head.position);
            if (vinfo.minDist <= VCORNER_BOUND) {
                currentVirtTarget = GetNextVirtualTarget(vinfo.id);
		    }
        }

        // rate based on timer
        return TransitRate();
    }

    private void CalcCurvatureRadius() { 
		// calculate new radius
		curvatureGainRate = (PATH_LENGTH * 0.5f) / Mathf.Sin(ConvertToRadians(StartingAngle()));
        radiText.text = "rad: " + curvatureGainRate.ToString();
    }

    private Vector3[] TransformToPos(List<Transform> transforms) {
        int length = transforms.Count;
        Vector3[] vectors = new Vector3[length];

        for (int i = 0; i < length; ++i) {
            vectors[i] = transforms[i].position;
		}
        return vectors;
    }

    // 
    private Vector3 CalcTurningCenterRelativePos(int approachedTarget)
    {
        if (turningPoints == null) { 
			turningPoints = start.GetComponent<BoundaryCheck>().turningPoints;
		}
        // calculate turning center based on current radius and standing point
        Vector3 next = turningPoints[GetNextTarget(approachedTarget)] - turningPoints[approachedTarget];
        // the direction of the turning circle center
        Vector3 dirOfCenter = Quaternion.Euler(0,  -(90.0f - StartingAngle()), 0) * next;

        turningCenterPrevRelaPos = dirOfCenter.normalized * curvatureGainRate;
        Vector3 relative = turningCenterPrevRelaPos - next;
        return relative;
    }

    private int GetNextTarget(int target)
    { 
        // change target
        return (target + 1) % BoundaryCheck.NUM_OF_BOUNDARY_POINTS;
        // should be able to determine next target based on users' walking direction is clock wise or anti-clock wise
    }

    private int GetNextVirtualTarget(int target)
    {
        return (target + 1) % NUM_OF_CORNERS;
    }

    // User goes from outside in
    private void GoFromOutsideIn()
    {
        float newLackDegs;
        isInTransit = true;
        isBeforeInside = true;
        timer = TRANSIT_TIME;

        if (isFirstOutsideIn)
        {
			isFirstOutsideIn = false;
            if (curvatureGainInDeg < PART_CURVATURE * lackCountThreshold) { 
			    isFirstOutsideIn = false;
			}
        }
        else
        {
            // Calculate the lack degrees and accumulate it
            newLackDegs = PART_CURVATURE - (curvatureGainInDeg + rotationGainInDeg);
            lackDegs += newLackDegs;
            Debug.Log("curvature lack " + newLackDegs.ToString());
        }

        curvatureGainInDeg = 0.0f;
    }

    // User goes from inside out
    private void GoFromInsideOut()
    {
        float newLackDegs;
        isInTransit = true;
        isBeforeInside = false;
        timer = TRANSIT_TIME;

        // Calculate the lack degrees in rotation gain
        // We shouldn't do this at first time cuz the user starts inside the 'turning area'
        if (isFirstInsideOut)
        {
			isFirstInsideOut = false;
            if (rotationGainInDeg < PART_ROTATION * lackCountThreshold) { 
			    isFirstInsideOut = false;
			}
        }
        else
        {
            newLackDegs = PART_ROTATION - Mathf.Abs( curvatureGainInDeg + rotationGainInDeg);
            Debug.Log("rotation lack " + newLackDegs.ToString());
            lackDegs += newLackDegs;
        }

        rotationGainInDeg = 0.0f;
    }

    private float StartingAngle()
    {
        return (lackDegs + PART_CURVATURE) / 2.0f;
    }

    // Returns the rate for two kinds of gains (0~1)
    private float TransitRate()
    {
        if (isInTransit)
        {
            timer = timer - Time.deltaTime;
            if (timer <= 0.0f)
            {
                // time over
                isInTransit = false;
                timer = 0.0f;
            }
            float fragment = timer / TRANSIT_TIME;
            // if user is inside, we return from 0 to 1
            if (isBeforeInside == true) { return 1.0f - fragment; }
            // else we return from 1 to 0
            return fragment;
        }
        else
        {
            if (isBeforeInside == true) { return 1.0f; }
            return 0.0f;
        }
    }


    private bool IsUserRotating() {
        return (Mathf.Abs(deltaDir) / Time.deltaTime >= ROTATION_THRESHOLD) && IsUserMoving();
    }
   
    private bool IsUserMoving() {
        return (deltaPos.magnitude / Time.deltaTime > MOVEMENT_THRESHOLD); 
    }

    private void RotationGain()
    {
        // update rotation gain here
        if (IsRDWOn())
        {
            if (IsUserRotating())  //if User is rotating
            {
                // Do rotation gain if the user is rotating
                float rotationInDegrees = (1 - rotationGainRate) * deltaDir *
                    /* this transit is aimed for corner transit of rotation gain and curvature gain */
                    transit;
                rotationGainInDeg += rotationInDegrees;
                // Direct user to the left side
                worldTrans.RotateAround(head.position, Vector3.down, -rotationInDegrees);
                //worldTrans.Rotate(Vector3.up, rotationInDegrees, Space.Self);
            }
        }
        else
        {
            prevPos = HeadPosition();
            prevDir = FlattenedDir3D(currDir);
        }
    }

    private void CurvatureGain()
    {
        // update curvature gain here
        if (IsRDWOn())
        {

            // Do curvature gain
            if (!float.IsNaN(prevPos.x))
            {
                deltaPos = currPos - prevPos;
                deltaPos = Utilities.FlattenedPos3D(deltaPos);
                if (IsUserMoving()) //User is moving
                {
                    // To be done: add a parameter here to determine the direction is clock-wise or counter clock wise
                    // !!!
                    rotationFromCurvatureGain = Mathf.Rad2Deg * (deltaPos.magnitude / (curvatureGainRate)) *
                    /* this transit is aimed for corner transit of rotation gain and curvature gain*/
                    (1 - transit);
                    // adjust the curvature gain based on counter deviation result
                    // to be done
                    rotationFromCurvatureGain += CounterDeviationAdjust(rotationFromCurvatureGain);
                    // think about negative values

                    curvatureGainInDeg += rotationFromCurvatureGain;
                    // if (Mathf.Approximately(rotationFromCurvatureGain, 0)) return;
                    if (rotationFromCurvatureGain < 0) { 
					    worldTrans.RotateAround(head.position, Vector3.down, rotationFromCurvatureGain);
					} else { 
					    worldTrans.RotateAround(head.position, Vector3.up, -rotationFromCurvatureGain);
					}
                    //worldTrans.Rotate(Vector3.up, rotationFromCurvatureGain, Space.Self);
                }
            }
        }
    }

    // not in use yet
    private void TranslationGain() { 
		if (IsRDWOn() && IsUserMoving() && (1 - transit) > 0) {
            // First calc the distance from user to next check point
            float distToVirTarget = Vector3.Distance(FlattenedDir3D(head.position), FlattenedDir3D(corners[currentVirtTarget].position));
            // Calc the rate of the circle completed in physical space 
            Vector3 vecToHead = head.localPosition - turningCenter;
            vecToHead = FlattenedDir3D(vecToHead);
            //// Calc the rate 
            float physRemainDist = Vector3.Distance(FlattenedDir3D(head.localPosition), FlattenedDir3D(turningPoints[currentTarget]));
            // Calc translation gain rate
            float rawTransGainRate = physRemainDist / distToVirTarget;
            // info dump
            transGainText.text = "trans: " + rawTransGainRate.ToString();
            //float rawTransGainRate = physDistRemainRate / virtDistRemainRate;
            // threshold
            if (rawTransGainRate > 1 + TRANSLATION_UP_THRESHOLD) rawTransGainRate = 1 + TRANSLATION_UP_THRESHOLD;
            if (rawTransGainRate < 1 - TRANSLATION_DOWN_THRESHOLD) rawTransGainRate = 1 - TRANSLATION_DOWN_THRESHOLD;
            // Calc the vector of forward moving
            Vector3 flatHead = FlattenedDir3D(head.forward);
            Vector3 translationGain = (rawTransGainRate - 1) * flatHead.normalized * Vector3.Dot(flatHead.normalized, deltaPos);
            translationGain = translationGain * (1 - transit);
            // Apply translation gain
            worldTrans.localPosition += translationGain;
		}
    }

    private void UpdatePrevData() { 
        prevPos = HeadPosition();
        prevDir = FlattenedDir3D(currDir);
    }

    private void UpdateCurrData() { 
		currPos = HeadPosition();
        currDir = FlattenedDir3D(head.forward);
		deltaDir = Utilities.GetSignedAngle(prevDir, currDir);
    }

    // returns the adjustment in curvature gain
    private float CounterDeviationAdjust(float currGain) {
        float diff = DistanceToCenter() - curvatureGainRate;
        // data  
        distText.text = "dist: " + diff.ToString();
        float ratio = (diff / BIAS_THRESHOLD); 
        if (diff > 0.0f) {
            // we are out, need to apply more gain
            float maxAdjust = 0.4f * currGain;
            if (diff < BIAS_THRESHOLD) {
                return maxAdjust * ratio;
			}
            return maxAdjust;
		}
        else {
            // we are in the circle, need to direct user less
            float maxAdjust = -2.0f * currGain;
            if (diff > -BIAS_THRESHOLD) return -ratio * maxAdjust;
            return maxAdjust;
		}
    }

    private float DistanceToCenter()
    {
        float dist = Vector3.Distance(FlattenedDir3D(head.localPosition), FlattenedDir3D(turningCenter));
        Debug.Log("Distance to center: " + dist.ToString());
        return dist;
    }

    private bool IsRDWOn()
    {
        // outside signal an user control
        if ( isRDWOn && !OVRInput.Get(OVRInput.RawButton.X)) { return true; }
        return false;
    }

    private Vector3 HeadPosition()
    {
        // I think this is the actual physical position of the head?
        return head.localPosition;
    }

    public static Vector3 FlattenedDir3D(Vector3 vec)
    {
        return (new Vector3(vec.x, 0, vec.z));
    }

    public float ConvertToRadians(float angle)
    {
        return (Mathf.PI / 180) * angle;
    }
}
