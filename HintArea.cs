using Godot;

public partial class HintArea : Area3D
{
    [Export]
    private CharacterBody3D target = null!;

    [Export]
    private CameraController cameraController = null!;


    public override void _Ready()
    {
        // Listen for when a body enters or leaves this area
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }


    private void OnBodyEntered(Node3D body)
    {
        // Check if the body that entered is the player
        if (body == target)
        {
            GD.Print("PLAYER ENTERED HINT AREA");

            // Tell the camera controller to use the Hint camera
            cameraController.SetHintActive(true);
        }
    }


    private void OnBodyExited(Node3D body)
    {
        // Check if the player left the Hint area
        if (body == target)
        {
            GD.Print("PLAYER LEFT HINT AREA");

            // Return to the normal third-person camera
            cameraController.SetHintActive(false);
        }
    }
}