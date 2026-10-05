using Godot;

public partial class CameraController : Node3D
{
    [Export] private Camera3D camera = null!;
    [Export] private Node3D target = null!;

    [Export] private float cameraHeight = 5.0f;
    [Export] private float cameraDistance = 8.0f;
    [Export] private float lookAtHeight = 1.0f;
    [Export] private float rotationSpeed = 60.0f;
    [Export] private float minPitch = -30.0f;
    [Export] private float maxPitch = 45.0f;
    [Export] private float followSpeed = 5.0f;
    [Export] private float autoAlignSpeed = 2.0f;

    // Wall avoidance settings
    [Export] private float whiskerWidth = 1.5f;
    [Export] private float sidePushDistance = 1.5f;
    [Export] private float sidePushSpeed = 6.0f;
    [Export] private float wallPadding = 0.4f;
    [Export] private float avoidanceMoveSpeed = 12.0f;
    [Export] private float returnMoveSpeed = 6.0f;
    [Export] private float minimumCameraDistance = 1.5f;

    // Ground avoidance settings
    [Export] private float groundWhiskerDepth = 2.0f;
    [Export] private float groundLift = 1.5f;
    [Export] private float groundInwardPush = 1.5f;
    [Export] private float groundAvoidanceSpeed = 8.0f;

    // Hint camera settings
    [Export] private Marker3D hintCameraPoint = null!;
    [Export] private float hintFov = 55.0f;
    [Export] private float hintTransitionSpeed = 2.5f;

    private float yaw = 0.0f;
    private float pitch = 0.0f;

    private float sideOffset = 0.0f;
    private float groundAmount = 0.0f;
    private float hintAmount = 0.0f;

    private bool hintActive = false;
    private float normalFov;


    public override void _Ready()
    {
        // Save the normal FOV so we can return to it later
        normalFov = camera.Fov;
    }


    public void SetHintActive(bool active)
    {
        // HintArea uses this to turn the Hint camera on or off
        hintActive = active;
    }


    public override void _Process(double delta)
    {
        float dt = (float)delta;


        // Smoothly follow the player
        float followWeight =
            1.0f - Mathf.Exp(-followSpeed * dt);

        GlobalPosition =
            GlobalPosition.Lerp(
                target.GlobalPosition,
                followWeight
            );


        // Read camera input from the right joystick
        float horizontalInput =
            Input.GetAxis(
                "Camera_left",
                "Camera_right"
            );

        float verticalInput =
            Input.GetAxis(
                "Camera_up",
                "Camera_down"
            );


        yaw += horizontalInput * rotationSpeed * dt;
        pitch += verticalInput * rotationSpeed * dt;


        // Limit how far the camera can rotate up and down
        pitch =
            Mathf.Clamp(
                pitch,
                minPitch,
                maxPitch
            );


        // Check if the player is manually moving the camera
        bool usingCameraStick =
            Mathf.Abs(horizontalInput) > 0.01f ||
            Mathf.Abs(verticalInput) > 0.01f;


        // If the player is not moving the camera,
        // slowly align it with the movement direction
        if (!usingCameraStick &&
            target is CharacterBody3D character)
        {
            Vector3 moveDirection =
                character.Velocity;

            moveDirection.Y = 0;


            if (moveDirection.Length() > 0.1f)
            {
                moveDirection =
                    moveDirection.Normalized();


                float wantedYaw =
                    Mathf.RadToDeg(
                        Mathf.Atan2(
                            -moveDirection.X,
                            -moveDirection.Z
                        )
                    );


                float weight =
                    1.0f -
                    Mathf.Exp(
                        -autoAlignSpeed * dt
                    );


                yaw =
                    Mathf.RadToDeg(
                        Mathf.LerpAngle(
                            Mathf.DegToRad(yaw),
                            Mathf.DegToRad(wantedYaw),
                            weight
                        )
                    );
            }
        }


        // Find the normal camera position around the player
        Vector3 wantedPosition =
            GetNormalCameraPosition();


        // All camera rays start near the player
        Vector3 rayStart =
            target.GlobalPosition +
            Vector3.Up * lookAtHeight;


        // Check walls and move the camera if needed
        bool wallNearby;

        wantedPosition =
            HandleWalls(
                rayStart,
                wantedPosition,
                horizontalInput,
                dt,
                out wallNearby
            );


        // Check the ground and push camera up/inward if needed
        wantedPosition =
            HandleGround(
                rayStart,
                wantedPosition,
                dt
            );


        // Use a faster movement speed while avoiding obstacles
        bool avoidingSomething =
            wallNearby ||
            groundAmount > 0.01f;


        float moveSpeed =
            avoidingSomething
            ? avoidanceMoveSpeed
            : returnMoveSpeed;


        float moveWeight =
            1.0f -
            Mathf.Exp(
                -moveSpeed * dt
            );


        // Smoothly move toward the wanted camera position
        Vector3 smoothPosition =
            camera.GlobalPosition.Lerp(
                wantedPosition,
                moveWeight
            );


        // Make sure the smooth movement did not move through a wall
        smoothPosition =
            ResolveWallCollision(
                rayStart,
                smoothPosition
            );


        // Keep the camera from getting too close to the player
        Vector3 fromPlayer =
            smoothPosition -
            rayStart;


        if (fromPlayer.Length() <
            minimumCameraDistance)
        {
            if (fromPlayer.Length() > 0.01f)
            {
                smoothPosition =
                    rayStart +
                    fromPlayer.Normalized() *
                    minimumCameraDistance;
            }
        }


        // Put the camera at the normal gameplay position
        camera.GlobalPosition =
            smoothPosition;


        // Make the camera look toward the player
        Vector3 lookPosition =
            GlobalPosition +
            Vector3.Up * lookAtHeight;


        camera.LookAt(
            lookPosition,
            Vector3.Up
        );


        // Save the normal camera rotation before applying Hint camera
        Quaternion normalRotation =
            camera.GlobalBasis
                .GetRotationQuaternion();


        // Smoothly turn the Hint camera on or off
        float wantedHintAmount =
            hintActive
            ? 1.0f
            : 0.0f;


        float hintWeight =
            1.0f -
            Mathf.Exp(
                -hintTransitionSpeed * dt
            );


        hintAmount =
            Mathf.Lerp(
                hintAmount,
                wantedHintAmount,
                hintWeight
            );


        Vector3 finalPosition =
            smoothPosition;

        Quaternion finalRotation =
            normalRotation;


        if (hintCameraPoint != null)
        {
            // Blend toward the Hint camera position
            finalPosition =
                smoothPosition.Lerp(
                    hintCameraPoint.GlobalPosition,
                    hintAmount
                );


            // Get the rotation of the Hint camera point
            Quaternion hintRotation =
                hintCameraPoint.GlobalBasis
                    .GetRotationQuaternion();


            // Blend toward the Hint camera rotation
            finalRotation =
                normalRotation.Slerp(
                    hintRotation,
                    hintAmount
                );
        }


        // Apply the final position and rotation
        Transform3D cameraTransform =
            camera.GlobalTransform;


        cameraTransform.Origin =
            finalPosition;

        cameraTransform.Basis =
            new Basis(finalRotation);


        camera.GlobalTransform =
            cameraTransform;


        // Smoothly change FOV when Hint camera is active
        camera.Fov =
            Mathf.Lerp(
                normalFov,
                hintFov,
                hintAmount
            );
    }


    private Vector3 GetNormalCameraPosition()
    {
        // Convert yaw and pitch into radians for trigonometry
        float yawRad =
            Mathf.DegToRad(yaw);

        float pitchRad =
            Mathf.DegToRad(pitch);


        float horizontalDistance =
            Mathf.Cos(pitchRad) *
            cameraDistance;


        // Calculate the camera offset around the player
        Vector3 offset =
            new Vector3(
                Mathf.Sin(yawRad) *
                horizontalDistance,

                cameraHeight +
                Mathf.Sin(pitchRad) *
                cameraDistance,

                Mathf.Cos(yawRad) *
                horizontalDistance
            );


        return GlobalPosition + offset;
    }


    private Vector3 HandleWalls(
        Vector3 rayStart,
        Vector3 wantedPosition,
        float horizontalInput,
        float dt,
        out bool wallNearby
    )
    {
        float yawRad =
            Mathf.DegToRad(yaw);


        // Find the camera right direction
        Vector3 cameraRight =
            new Vector3(
                Mathf.Cos(yawRad),
                0,
                -Mathf.Sin(yawRad)
            ).Normalized();


        // Create left and right whisker positions
        Vector3 leftEnd =
            wantedPosition -
            cameraRight * whiskerWidth;


        Vector3 rightEnd =
            wantedPosition +
            cameraRight * whiskerWidth;


        // Check the centre ray
        bool centreBlocked =
            CastRay(
                rayStart,
                wantedPosition,
                out Vector3 hitPosition,
                out Vector3 hitNormal
            );


        // Check the left whisker
        bool leftBlocked =
            CastRay(
                rayStart,
                leftEnd,
                out _,
                out _
            );


        // Check the right whisker
        bool rightBlocked =
            CastRay(
                rayStart,
                rightEnd,
                out _,
                out _
            );


        wallNearby =
            centreBlocked ||
            leftBlocked ||
            rightBlocked;


        float wantedSideOffset =
            0.0f;


        // If the left side is blocked, push camera right
        if (leftBlocked &&
            !rightBlocked)
        {
            wantedSideOffset =
                sidePushDistance;
        }


        // If the right side is blocked, push camera left
        else if (rightBlocked &&
                 !leftBlocked)
        {
            wantedSideOffset =
                -sidePushDistance;
        }


        // If both sides are blocked,
        // use the joystick direction
        else if (leftBlocked &&
                 rightBlocked)
        {
            if (horizontalInput > 0.05f)
            {
                wantedSideOffset =
                    sidePushDistance;
            }

            else if (horizontalInput < -0.05f)
            {
                wantedSideOffset =
                    -sidePushDistance;
            }
        }


        // Smooth the sideways movement
        float sideWeight =
            1.0f -
            Mathf.Exp(
                -sidePushSpeed * dt
            );


        sideOffset =
            Mathf.Lerp(
                sideOffset,
                wantedSideOffset,
                sideWeight
            );


        if (centreBlocked)
        {
            // Move the camera to the safe side of the wall
            wantedPosition =
                ProjectToWall(
                    wantedPosition,
                    hitPosition,
                    hitNormal
                );


            // Find a direction that runs along the wall
            Vector3 wallDirection =
                cameraRight -
                hitNormal *
                cameraRight.Dot(hitNormal);


            wallDirection.Y = 0;


            if (wallDirection.Length() > 0.01f)
            {
                wallDirection =
                    wallDirection.Normalized();


                if (wallDirection.Dot(cameraRight) < 0)
                {
                    wallDirection =
                        -wallDirection;
                }


                wantedPosition +=
                    wallDirection *
                    sideOffset;
            }
        }
        else
        {
            wantedPosition +=
                cameraRight *
                sideOffset;
        }


        // Check again so the camera does not clip through a wall
        return ResolveWallCollision(
            rayStart,
            wantedPosition
        );
    }


    private Vector3 HandleGround(
        Vector3 rayStart,
        Vector3 wantedPosition,
        float dt
    )
    {
        // Aim a ray toward a point below the camera
        Vector3 groundWhiskerEnd =
            wantedPosition -
            Vector3.Up *
            groundWhiskerDepth;


        bool hitGround =
            CastRay(
                rayStart,
                groundWhiskerEnd,
                out _,
                out Vector3 groundNormal
            );


        // Check that the ray hit a floor-like surface
        bool groundDetected =
            hitGround &&
            groundNormal.Y > 0.5f;


        float wantedAmount =
            groundDetected
            ? 1.0f
            : 0.0f;


        // Smooth this value to stop the camera from shaking
        float weight =
            1.0f -
            Mathf.Exp(
                -groundAvoidanceSpeed * dt
            );


        groundAmount =
            Mathf.Lerp(
                groundAmount,
                wantedAmount,
                weight
            );


        if (groundAmount > 0.001f)
        {
            Vector3 fromPlayer =
                wantedPosition -
                rayStart;


            float distance =
                fromPlayer.Length();


            if (distance > 0.01f)
            {
                Vector3 direction =
                    fromPlayer.Normalized();


                // Move the camera slightly closer to the player
                float newDistance =
                    Mathf.Max(
                        distance -
                        groundInwardPush,
                        minimumCameraDistance
                    );


                // Move the camera inward and upward
                Vector3 groundSafePosition =
                    rayStart +
                    direction *
                    newDistance +
                    Vector3.Up *
                    groundLift;


                groundSafePosition =
                    ResolveWallCollision(
                        rayStart,
                        groundSafePosition
                    );


                wantedPosition =
                    wantedPosition.Lerp(
                        groundSafePosition,
                        groundAmount
                    );
            }
        }


        return wantedPosition;
    }


    private Vector3 ProjectToWall(
        Vector3 point,
        Vector3 hitPosition,
        Vector3 hitNormal
    )
    {
        // Find how far the wanted position goes through the wall
        float distanceThroughWall =
            (point - hitPosition)
            .Dot(hitNormal);


        // Move the camera back onto the safe side
        Vector3 safePosition =
            point -
            hitNormal *
            distanceThroughWall;


        // Leave a small gap between camera and wall
        safePosition +=
            hitNormal *
            wallPadding;


        return safePosition;
    }


    private Vector3 ResolveWallCollision(
        Vector3 rayStart,
        Vector3 wantedPosition
    )
    {
        Vector3 safePosition =
            wantedPosition;


        // Check twice because corners can need another correction
        for (int i = 0; i < 2; i++)
        {
            bool blocked =
                CastRay(
                    rayStart,
                    safePosition,
                    out Vector3 hitPosition,
                    out Vector3 hitNormal
                );


            if (!blocked)
            {
                break;
            }


            safePosition =
                ProjectToWall(
                    safePosition,
                    hitPosition,
                    hitNormal
                );
        }


        return safePosition;
    }


    private bool CastRay(
        Vector3 start,
        Vector3 end,
        out Vector3 hitPosition,
        out Vector3 hitNormal
    )
    {
        var space =
            GetWorld3D()
            .DirectSpaceState;


        var query =
            PhysicsRayQueryParameters3D.Create(
                start,
                end
            );


        // Camera checks only Layer 1.
        // Small obstacles are Layer 2, so they are ignored.
        query.CollisionMask = 1;


        // Ignore the player's own body
        if (target is CollisionObject3D playerBody)
        {
            query.Exclude =
                new Godot.Collections.Array<Rid>
                {
                    playerBody.GetRid()
                };
        }


        var result =
            space.IntersectRay(query);


        if (result.Count > 0)
        {
            hitPosition =
                result["position"]
                .AsVector3();

            hitNormal =
                result["normal"]
                .AsVector3();

            return true;
        }


        hitPosition = end;
        hitNormal = Vector3.Zero;

        return false;
    }
}