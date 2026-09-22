using System;
using System.Collections.Generic;
using UnityEngine;

// A gate is a departing flight. Its manifest lists how many bags of each colour the plane
// still needs; players carry matching luggage into the gate trigger until every line is
// filled, at which point the flight departs and a fresh one takes its place.
//
// Colour is the entire destination rule — any red bag fills a red slot, and no bag is ever
// pre-assigned to a gate. A bag the current flight does not need is thrown back out onto the
// floor instead of scored, so a wrong delivery costs the players time rather than points.
public class Gate : MonoBehaviour
{
    // One line of the current flight: what it wants and how much of it has landed.
    //
    // Every line names a colour. A line is then either plain ("3 red") or wrapped ("2 red, wrapped"),
    // so "wrapped" is a second axis on top of colour rather than a replacement for it — a wrapped
    // green bag will not fill a wrapped-red line.
    public class ManifestLine
    {
        public ManifestLine(LuggageColor color, bool needsWrapped, int required)
        {
            Color = color;
            NeedsWrapped = needsWrapped;
            Required = required;
        }

        public LuggageColor Color { get; }

        /// <summary>Whether the bag must have been through a wrapper.</summary>
        public bool NeedsWrapped { get; }

        public int Required { get; }
        public int Delivered { get; private set; }
        public bool IsFilled => Delivered >= Required;

        public bool Accepts(Luggage luggage)
        {
            if (IsFilled) return false;
            // Exact match on both axes: a wrapped bag does not fill a plain line and a plain bag
            // does not fill a wrapped one, so over-processing is as wrong as under-processing.
            if (luggage.IsWrapped != NeedsWrapped) return false;
            return luggage.color == Color;
        }

        public void CountDelivery()
        {
            Delivered++;
        }
    }

    [SerializeField, Min(1)] private int gateNumber = 1;

    [Header("Flight Generation")]
    [Tooltip("Colours this gate's flights may ask for. Every colour listed here must also be " +
             "spawnable by the level's luggage pool, or a flight that asks for it can never be filled.")]
    [SerializeField]
    private List<LuggageColor> palette = new()
    {
        LuggageColor.Red,
        LuggageColor.Blue,
        LuggageColor.Green,
        LuggageColor.Yellow
    };

    [Tooltip("How many different colours one flight asks for. Never more than the palette holds.")]
    [SerializeField, Min(1)] private int minColorsPerFlight = 1;
    [SerializeField, Min(1)] private int maxColorsPerFlight = 2;

    [Tooltip("How many bags a flight asks for of each colour it wants.")]
    [SerializeField, Min(1)] private int minBagsPerColor = 1;
    [SerializeField, Min(1)] private int maxBagsPerColor = 3;

    [Header("Wrapped Lines")]
    [Tooltip("How many lines of every flight ask for wrapped bags. Each names its own colour, so " +
             "a wrapped line reads 'red, wrapped'. Fixed, not random: a level that teaches " +
             "wrapping should always ask for it. 0 = never.")]
    [SerializeField, Min(0)] private int wrappedLinesPerFlight;
    [Tooltip("How many wrapped bags such a line asks for.")]
    [SerializeField, Min(1)] private int minBagsPerWrappedLine = 1;
    [SerializeField, Min(1)] private int maxBagsPerWrappedLine = 2;

    [Header("Rejection")]
    [Tooltip("Horizontal speed a bag the flight does not want is thrown back out at.")]
    [SerializeField, Min(0f)] private float rejectSpeed = 6f;
    [Tooltip("Upward speed added to the bounce so the bag arcs clear of the gate instead of scraping it.")]
    [SerializeField, Min(0f)] private float rejectLift = 3f;

    private readonly List<ManifestLine> manifest = new();

    // Scratch copy of the palette that GenerateFlight draws from, so one flight never asks
    // for the same colour on two lines. Kept as a field to avoid allocating per flight.
    private readonly List<LuggageColor> paletteDraw = new();

    public int GateNumber => gateNumber;
    public IReadOnlyList<ManifestLine> Manifest => manifest;
    public int FlightsCompleted { get; private set; }

    // Raised when the manifest is replaced or a line's delivered count moves, so the gate's
    // scene-authored board can redraw without polling every frame.
    public event Action ManifestChanged;

    private void Start()
    {
        GenerateFlight();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Luggage.TryGetFromCollider(other, out Luggage luggage)) return;
        if (luggage == null || luggage.IsDelivered) return;

        LevelConfig config = LevelContext.CurrentConfig;
        if (config == null)
        {
            Debug.LogError($"{nameof(Gate)} '{name}' needs an active {nameof(LevelContext)}.", this);
            return;
        }

        ManifestLine line = FindOpenLine(luggage);
        if (line == null)
        {
            RejectLuggage(luggage);
            return;
        }

        int playerIndex = luggage.GetLastGrabber() != null
            ? luggage.GetLastGrabber().GetPlayerIndex()
            : -1;

        if (!RoundScoreContext.TryRecordDelivery(playerIndex, config.scoreCorrectDelivery))
            return;

        line.CountDelivery();
        luggage.IsDelivered = true;
        luggage.DestroyLuggage();

        if (IsManifestFilled())
            CompleteFlight();
        else
            ManifestChanged?.Invoke();
    }

    private ManifestLine FindOpenLine(Luggage luggage)
    {
        for (int i = 0; i < manifest.Count; i++)
        {
            if (manifest[i].Accepts(luggage))
                return manifest[i];
        }

        return null;
    }

    private bool IsManifestFilled()
    {
        for (int i = 0; i < manifest.Count; i++)
        {
            if (!manifest[i].IsFilled)
                return false;
        }

        return manifest.Count > 0;
    }

    private void RejectLuggage(Luggage luggage)
    {
        Rigidbody body = luggage.Body;
        if (body == null) return;

        // Whoever walked it in loses hold of it, the same as any dropped bag.
        luggage.DropAllGrabbers();
        body.isKinematic = false;

        // Send it back the way it came so it lands in front of the gate instead of inside it.
        Vector3 back = -body.linearVelocity;
        back.y = 0f;
        if (back.sqrMagnitude < 0.01f)
            back = -transform.forward;

        body.linearVelocity = back.normalized * rejectSpeed + Vector3.up * rejectLift;
    }

    private void CompleteFlight()
    {
        FlightsCompleted++;
        GenerateFlight();
    }

    private void GenerateFlight()
    {
        manifest.Clear();

        paletteDraw.Clear();
        paletteDraw.AddRange(palette);
        if (paletteDraw.Count == 0)
        {
            Debug.LogError($"{nameof(Gate)} '{name}' has an empty colour palette and cannot build a flight.", this);
            ManifestChanged?.Invoke();
            return;
        }

        int lineCount = Mathf.Clamp(
            UnityEngine.Random.Range(minColorsPerFlight, maxColorsPerFlight + 1),
            1,
            paletteDraw.Count);

        for (int i = 0; i < lineCount; i++)
        {
            // Drawn without replacement so one flight never lists the same colour twice.
            int pick = UnityEngine.Random.Range(0, paletteDraw.Count);
            LuggageColor color = paletteDraw[pick];
            paletteDraw.RemoveAt(pick);

            int bags = UnityEngine.Random.Range(minBagsPerColor, maxBagsPerColor + 1);
            manifest.Add(new ManifestLine(color, needsWrapped: false, bags));
        }

        // Wrapped lines draw from the full palette, independently of the plain lines. A flight may
        // therefore want both "2 red" and "1 red wrapped" — two different jobs on the same colour,
        // which is exactly the kind of order that makes the wrapper worth fighting over.
        for (int i = 0; i < wrappedLinesPerFlight; i++)
        {
            LuggageColor color = palette[UnityEngine.Random.Range(0, palette.Count)];
            int bags = UnityEngine.Random.Range(minBagsPerWrappedLine, maxBagsPerWrappedLine + 1);
            manifest.Add(new ManifestLine(color, needsWrapped: true, bags));
        }

        ManifestChanged?.Invoke();
    }

    private void OnValidate()
    {
        maxColorsPerFlight = Mathf.Max(minColorsPerFlight, maxColorsPerFlight);
        maxBagsPerColor = Mathf.Max(minBagsPerColor, maxBagsPerColor);
        maxBagsPerWrappedLine = Mathf.Max(minBagsPerWrappedLine, maxBagsPerWrappedLine);
    }
}
