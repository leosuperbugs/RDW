using UnityEngine;

public class ControllerLocomotion : MonoBehaviour
{
	  // determines which controller should be used for locomotion
    public enum Controller { Left, Right };
    public Controller controller = Controller.Right;

	  // the maximum movement speed in meters per second
    public float maxSpeed = 3.0f;
	
    // the deadzone is the area close to the center of the thumbstick
    public float moveDeadzone = 0.2f;
    public float rotateDeadzone = 0.75f;

    private bool locked = false;

    // determine which way user direct their steering
    public enum SteerDir { View, Head};
    public int steerDir = (int)SteerDir.Head;

    // direction
    public Quaternion rotation;

    OVRCameraRig cameraRig = null;

    // turing degrees of snap turn
    private float ST_DEGREES = 45.0f;
     public float lineWidth = 0.01f;
    public float height = 1.2f;

    private LineRenderer lineRenderer;

    // private bool didHitTarget = false;
    // private bool inTranslation = false;

    public float flyTime = 0.3f;
    // private float actualFlyTime = 0.0f;
    private Vector3 deltaPos;

    // Start is called before the first frame update
    void Start()
    {
		// this script is meant to be used on the OVRCameraRig game object
        cameraRig = GetComponent<OVRCameraRig>();
    }

    // Update is called once per frame
    void Update()
    {
        //if (OVRInput.GetDown(OVRInput.Button.One))
        //{
        //    ++steerDir;
        //    steerDir %= 2;
        //}

        if (steerDir == (int)SteerDir.View)
        {
            rotation = cameraRig.centerEyeAnchor.rotation;
        } 
        else
        {
            rotation = cameraRig.rightControllerAnchor.rotation;
        }

        // stores the x and y values of the thumbstick
        Vector2 thumbstickVector = new Vector2();

		// read the thumbstick values from either the right or left controller
        if (controller == Controller.Right)
            thumbstickVector = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        else
            thumbstickVector = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
		
		// if the thumbstick has been pushed outside the dead zone
        if (thumbstickVector.y > moveDeadzone || thumbstickVector.y < -moveDeadzone)
        {
            // COMPLETE THIS SECTION OF CODE

            // step 1 - create a Vector3 that contains the values for movement
            // this calculation will require maxSpeeed, thumstickVector.y, and Time.deltaTime
            Vector3 movement = new Vector3(0.0f, 0.0f, 1.0f) * maxSpeed * thumbstickVector.y * Time.deltaTime;


            // step 2 - multiply by movement vector by the head orientation
            // this can be retrieved using cameraRig.centerEyeAnchor.rotation
            movement = rotation * movement;

            // step 3 - add this movement vector to the current position of the game object
            // this can be found using transform.localPosition
            transform.localPosition += movement;

        }


        if (thumbstickVector.x > rotateDeadzone || thumbstickVector.x < -rotateDeadzone)
        {
            if (!locked)
            {
                // perform rapid turn
                locked = true;
                int rotateDirection;
                if (thumbstickVector.x > 0)
                {
                    rotateDirection = 1;
                }
                else
                {
                    rotateDirection = -1;
                }
                transform.RotateAround(transform.position, Vector3.up, rotateDirection * ST_DEGREES);
            }
        }
        else
        {
            locked = false;
        }


    }
}
