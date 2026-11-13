using UnityEngine;

// TEMPORARY: Level1Test station-refill experiment (Assets/_TempStationTest).
//
// Turns a world-space label to the camera's angle every frame, so the machines' and boxes' labels
// read the same however each one is turned in the scene.
public class FaceCamera : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = cam.transform.rotation;
    }
}
