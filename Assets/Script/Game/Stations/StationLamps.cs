using UnityEngine;

// Tints the two indicator domes on a machine station: the entry lamp says whether the machine
// will still take luggage, the exit lamp says whether finished luggage is on its way out.
// The lamps are authored on the prefab; this only recolours them.
[RequireComponent(typeof(MachineStation))]
public class StationLamps : MonoBehaviour
{
    [Header("Entry Side")]
    [SerializeField] private Renderer entryLamp;
    [SerializeField] private Light entryLight;

    [Header("Exit Side")]
    [SerializeField] private Renderer exitLamp;
    [SerializeField] private Light exitLight;

    [Header("Colours")]
    [SerializeField] private Color readyColor = new(0.45f, 1f, 0.5f);
    [SerializeField] private Color busyColor = new(1f, 0.35f, 0.3f);
    [SerializeField, Min(0f)] private float emissionStrength = 2.5f;
    [SerializeField, Min(0f)] private float lightIntensity = 2f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MachineStation station;
    private MaterialPropertyBlock block;
    private bool hasAppliedOnce;
    private StationPhase lastPhase;

    private void Awake()
    {
        station = GetComponent<MachineStation>();
        block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        hasAppliedOnce = false;
    }

    private void Update()
    {
        if (hasAppliedOnce && station.Phase == lastPhase)
            return;

        lastPhase = station.Phase;
        hasAppliedOnce = true;

        bool entryFree = lastPhase == StationPhase.Idle;
        bool outputReady = lastPhase == StationPhase.Ready;
        Apply(entryLamp, entryLight, entryFree ? readyColor : busyColor);
        Apply(exitLamp, exitLight, outputReady ? readyColor : busyColor);
    }

    private void Apply(Renderer lamp, Light lampLight, Color color)
    {
        if (lamp != null)
        {
            lamp.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionColorId, color * emissionStrength);
            lamp.SetPropertyBlock(block);
        }

        if (lampLight != null)
        {
            lampLight.color = color;
            lampLight.intensity = lightIntensity;
        }
    }
}
