using System.Collections;
using UnityEngine;

// The big button beside a dispatch gate. PlayerGrab presses it when an empty-handed player hits
// Grab (E) within reach, the same way it works a machine, so it only needs a collider to be found.
// The cap is lit green while the booth holds exactly what the order wants and amber otherwise; it
// can be pressed either way, and it goes dark while a flight is being dispatched.
public class DispatchButton : MonoBehaviour
{
    [SerializeField] private DispatchGate gate;
    [Tooltip("The part that lights up and sinks when pressed.")]
    [SerializeField] private Renderer cap;
    [SerializeField] private Color readyColor = new(0.24f, 0.86f, 0.42f);
    [SerializeField] private Color waitingColor = new(1f, 0.6f, 0.12f);
    [SerializeField] private Color busyColor = new(0.35f, 0.35f, 0.4f);
    [Tooltip("How far the cap sinks on a press, in its parent's units.")]
    [SerializeField, Min(0f)] private float pressDepth = 0.08f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock block;
    private Vector3 capRest;
    private Color shown = Color.clear;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (cap != null) capRest = cap.transform.localPosition;
    }

    private void Update()
    {
        if (cap == null || gate == null) return;

        Color target = gate.CurrentPhase == DispatchGate.Phase.Dispatching ? busyColor
                     : gate.IsReady ? readyColor
                     : waitingColor;
        if (target == shown) return;

        shown = target;
        cap.GetPropertyBlock(block);
        block.SetColor(BaseColorId, target);
        block.SetColor(EmissionColorId, target * 0.6f);
        cap.SetPropertyBlock(block);
    }

    public void Press(int playerIndex)
    {
        if (gate == null || gate.CurrentPhase != DispatchGate.Phase.Loading) return;

        gate.Press(playerIndex);
        if (cap != null) StartCoroutine(Sink());
    }

    private IEnumerator Sink()
    {
        Transform t = cap.transform;
        t.localPosition = capRest + Vector3.down * pressDepth;
        yield return new WaitForSeconds(0.15f);
        t.localPosition = capRest;
    }
}
