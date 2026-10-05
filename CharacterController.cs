using Godot;

public partial class CharacterController : CharacterBody3D
{
    [Export]
    private float moveSpeed = 5.0f;

    [Export]
    private float jumpVelocity = 5.0f;

    [Export]
    private float respawnHeight = -5.0f;

    private Vector3 spawnPosition;

    // Get the gravity value from Godot project settings
    private float gravity =
        ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();


    public override void _Ready()
    {
        // Save the starting position of the player
        // so we can use it later for respawning
        spawnPosition = GlobalPosition;
    }


    public override void _PhysicsProcess(double delta)
    {
        // Start with the player's current velocity
        Vector3 velocity = Velocity;


        // Apply gravity when the player is in the air
        if (!IsOnFloor())
        {
            velocity.Y -= gravity * (float)delta;
        }


        // Allow jumping only when the player is on the floor
        if (Input.IsActionJustPressed("Jump") && IsOnFloor())
        {
            velocity.Y = jumpVelocity;
        }


        // Get movement input from the left joystick
        Vector2 input = Input.GetVector(
            "Move_left",
            "Move_right",
            "Move_forward",
            "Move_backward"
        );


        // Convert the 2D joystick input into 3D movement
        // X is left/right and Z is forward/backward
        Vector3 direction = new Vector3(
            input.X,
            0,
            input.Y
        );


        // If the player is giving movement input
        if (direction.Length() > 0.0f)
        {
            // Normalize so diagonal movement
            // is not faster than normal movement
            direction = direction.Normalized();


            // Apply movement speed
            velocity.X = direction.X * moveSpeed;
            velocity.Z = direction.Z * moveSpeed;


            // Rotate the character so it faces
            // the direction it is moving
            LookAt(
                GlobalPosition + direction,
                Vector3.Up
            );
        }
        else
        {
            // Stop horizontal movement when
            // there is no joystick input
            velocity.X = 0;
            velocity.Z = 0;
        }


        // Give the new velocity to the CharacterBody3D
        Velocity = velocity;


        // Move the player and handle collisions
        MoveAndSlide();


        // If the player falls below the level,
        // move them back to the starting position
        if (GlobalPosition.Y < respawnHeight)
        {
            Respawn();
        }
    }


    private void Respawn()
    {
        // Return the player to the saved starting position
        GlobalPosition = spawnPosition;

        // Reset velocity so the player does not
        // continue falling after respawning
        Velocity = Vector3.Zero;
    }
}