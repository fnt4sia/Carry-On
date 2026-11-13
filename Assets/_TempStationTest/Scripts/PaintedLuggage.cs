using UnityEngine;

// TEMPORARY: Level1Test station-refill experiment (Assets/_TempStationTest).
//
// Added to a bag the paint machine repaints. The spawner pools bags by source prefab and doesn't
// reset a bag's colour when it rents it out again, so a green bag painted blue would come back out
// of the green pool still blue. When the pool takes the bag back it deactivates it; this then puts
// the bag's own colour back and removes itself.
public class PaintedLuggage : MonoBehaviour
{
    private Luggage luggage;
    private Renderer body;
    private LuggageColor originalColor;
    private Material originalMaterial;

    public static void Remember(Luggage bag, Renderer bodyRenderer)
    {
        // Painted twice: the first record holds the colour the bag was born with.
        if (bag.GetComponent<PaintedLuggage>() != null)
            return;

        PaintedLuggage record = bag.gameObject.AddComponent<PaintedLuggage>();
        record.luggage = bag;
        record.body = bodyRenderer;
        record.originalColor = bag.color;
        record.originalMaterial = bodyRenderer.sharedMaterials[0];
    }

    private void OnDisable()
    {
        if (luggage != null)
            luggage.color = originalColor;

        if (body != null)
        {
            Material[] materials = body.sharedMaterials;
            materials[0] = originalMaterial;
            body.sharedMaterials = materials;
        }

        Destroy(this);
    }
}
