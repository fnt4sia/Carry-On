using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The new Level1 gate: a glass booth a flight's bags are loaded into. Players carry in the bags the
// order lists, then press the dispatch button beside the gate. The door rolls down, the booth floor
// opens and everything inside drops away. The flight pays only if the booth held exactly what it
// asked for: a wrong or spare bag, a missing one, or the order's timer running out sends the lot
// down the hole for nothing and breaks the combo.
//
// Gate, the instant trigger delivery that bounces wrong bags back out, is the older flow. Level2 and
// the main menu still use it; the two never meet in one scene.
public class DispatchGate : MonoBehaviour
{
    // One colour of the current order: how many it wants and how many sit in the booth right now.
    // The booth count goes down as well as up, since a player can carry a bag back out.
    public class OrderLine
    {
        public OrderLine(LuggageColor color, int required)
        {
            Color = color;
            Required = required;
        }

        public LuggageColor Color { get; }
        public int Required { get; }
        public int InBooth { get; set; }
        public bool IsFilled => InBooth == Required;
        public bool IsOver => InBooth > Required;
    }

    public enum Phase { Loading, Dispatching }

    [SerializeField, Min(1)] private int gateNumber = 1;

    [Header("Order")]
    [Tooltip("Colours an order may ask for. Every colour here must also be in the level's luggage " +
             "pool, or an order that asks for it can never be filled.")]
    [SerializeField]
    private List<LuggageColor> palette = new() { LuggageColor.Red, LuggageColor.Green, LuggageColor.Yellow };
    [SerializeField, Min(1)] private int minColorsPerOrder = 2;
    [SerializeField, Min(1)] private int maxColorsPerOrder = 3;
    [SerializeField, Min(1)] private int minBagsPerColor = 1;
    [SerializeField, Min(1)] private int maxBagsPerColor = 2;
    [Tooltip("Cap on the bags one order asks for in total, so a big draw still fits the timer.")]
    [SerializeField, Min(1)] private int maxBagsPerOrder = 4;

    [Header("Booth")]
    [Tooltip("Centre of the booth floor. A bag lying within holdRadius of it, and below holdHeight, " +
             "is in the booth.")]
    [SerializeField] private Transform holdCentre;
    [SerializeField, Min(0.1f)] private float holdRadius = 6.8f;
    [SerializeField, Min(0.1f)] private float holdHeight = 7f;

    [Header("Dispatch")]
    [Tooltip("Rolling shutter over the entrance: scaled on Y from rolled up (0) to shut (1), pivot " +
             "at its top edge. Its collider stops bags, never players, so nobody gets shut inside it.")]
    [SerializeField] private Transform door;
    [Tooltip("Black disc on the booth floor that opens like an iris under the bags.")]
    [SerializeField] private Transform trapdoor;
    [SerializeField, Min(0.05f)] private float doorTime = 0.4f;
    [SerializeField, Min(0.05f)] private float trapdoorTime = 0.25f;
    [Tooltip("How long the bags take to sink through the open floor.")]
    [SerializeField, Min(0.1f)] private float dropTime = 0.9f;
    [Tooltip("Pause with the floor shut again before the door rolls up on the next order.")]
    [SerializeField, Min(0f)] private float nextOrderDelay = 0.6f;

    private const float FallbackOrderTime = 20f;

    private readonly List<OrderLine> order = new();
    private readonly List<LuggageColor> paletteDraw = new();
    private readonly List<Luggage> inBooth = new();
    private readonly HashSet<Luggage> seen = new();
    private readonly Collider[] overlap = new Collider[64];
    private Collider doorCollider;
    private Vector3 trapdoorOpenScale;
    private int contentsSignature = -1;

    public int GateNumber => gateNumber;
    public IReadOnlyList<OrderLine> Order => order;
    public Phase CurrentPhase { get; private set; }
    public float TimeLeft { get; private set; }
    public float OrderTime { get; private set; } = FallbackOrderTime;

    /// <summary>Bags in the booth that no line of the order wants.</summary>
    public int WrongBags { get; private set; }

    /// <summary>Flights dispatched in a row with the right bags. A failed one resets it.</summary>
    public int Streak { get; private set; }

    /// <summary>True when pressing the button now would pay out.</summary>
    public bool IsReady => CurrentPhase == Phase.Loading && ContentsMatch();

    // Raised when a new order arrives or the booth's contents change, so the screen redraws
    // without polling every frame. The timer bar is the only thing it reads per frame.
    public event Action OrderChanged;

    // Raised once per flight, when the door is shut and the booth judged: success, coins paid.
    public event Action<bool, int> Dispatched;

    private void Awake()
    {
        if (door != null)
        {
            doorCollider = door.GetComponentInChildren<Collider>();
            if (doorCollider != null) doorCollider.enabled = false;
            SetDoor(0f);
        }

        if (trapdoor != null)
        {
            trapdoorOpenScale = trapdoor.localScale;
            SetTrapdoor(0f);
        }
    }

    private void Start()
    {
        LevelConfig config = LevelContext.CurrentConfig;
        if (config != null)
            OrderTime = config.orderTime;
        else
            Debug.LogError($"{nameof(DispatchGate)} '{name}' needs an active {nameof(LevelContext)}.", this);

        GenerateOrder();
    }

    private void Update()
    {
        if (CurrentPhase != Phase.Loading) return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft > 0f) return;

        TimeLeft = 0f;
        StartCoroutine(Dispatch(-1, timedOut: true));
    }

    private void FixedUpdate()
    {
        if (CurrentPhase == Phase.Loading)
            ScanBooth();
    }

    // Called by DispatchButton. Works whatever the booth holds; the verdict comes after the door shuts.
    public void Press(int playerIndex)
    {
        if (CurrentPhase != Phase.Loading) return;
        StartCoroutine(Dispatch(playerIndex, timedOut: false));
    }

    private IEnumerator Dispatch(int presser, bool timedOut)
    {
        CurrentPhase = Phase.Dispatching;

        if (doorCollider != null) doorCollider.enabled = true;
        yield return Animate(doorTime, SetDoor, from: 0f, to: 1f);

        // Judged behind the shut door, on exactly the bags that are about to drop.
        ScanBooth();
        bool success = !timedOut && ContentsMatch();
        int coins = 0;
        if (success)
        {
            Streak++;
            coins = PayOut(presser);
        }
        else
        {
            Streak = 0;
        }

        AudioManager.Instance?.PlaySFX(success ? Sfx.Score : Sfx.Wrong);
        Dispatched?.Invoke(success, coins);

        yield return Animate(trapdoorTime, SetTrapdoor, from: 0f, to: 1f);
        yield return Drop(new List<Luggage>(inBooth));
        yield return Animate(trapdoorTime, SetTrapdoor, from: 1f, to: 0f);
        yield return new WaitForSeconds(nextOrderDelay);

        yield return Animate(doorTime, SetDoor, from: 1f, to: 0f);
        if (doorCollider != null) doorCollider.enabled = false;

        CurrentPhase = Phase.Loading;
        GenerateOrder();
    }

    // The bags sink straight down through the open floor. Kinematic, so nothing on the way down can
    // catch them; returning them to the pool puts physics back.
    private IEnumerator Drop(List<Luggage> bags)
    {
        var starts = new List<Vector3>(bags.Count);
        foreach (Luggage bag in bags)
        {
            bag.IsDelivered = true;
            bag.DropAllGrabbers();
            if (bag.Body != null)
            {
                bag.Body.linearVelocity = Vector3.zero;
                bag.Body.angularVelocity = Vector3.zero;
                bag.Body.isKinematic = true;
            }

            starts.Add(bag.transform.position);
        }

        for (float t = 0f; t < dropTime; t += Time.deltaTime)
        {
            float fall = 0.5f * -Physics.gravity.y * t * t;
            for (int i = 0; i < bags.Count; i++)
                if (bags[i] != null)
                    bags[i].transform.position = starts[i] + Vector3.down * fall;

            yield return null;
        }

        foreach (Luggage bag in bags)
            if (bag != null)
                bag.DestroyLuggage();
    }

    // coins = (per-bag coins x bags + coins per whole second left) x combo multiplier. Each bag's
    // share is credited to whoever last carried it, the rest to the player who pressed the button.
    private int PayOut(int presser)
    {
        LevelConfig config = LevelContext.CurrentConfig;
        if (config == null) return 0;

        float multiplier = config.ComboMultiplier(Streak);
        int bags = inBooth.Count;
        int total = Mathf.RoundToInt(
            (config.scoreCorrectDelivery * bags + config.coinsPerSecondLeft * Mathf.FloorToInt(TimeLeft))
            * multiplier);

        int perBag = Mathf.RoundToInt(config.scoreCorrectDelivery * multiplier);
        int paid = 0;
        foreach (Luggage bag in inBooth)
        {
            PlayerGrab carrier = bag.GetLastGrabber();
            int playerIndex = carrier != null ? carrier.GetPlayerIndex() : presser;
            if (RoundScoreContext.TryRecordDelivery(playerIndex, perBag))
                paid += perBag;
        }

        if (total != paid)
            RoundScoreContext.TryApplyScore(presser, total - paid);

        return total;
    }

    private void ScanBooth()
    {
        inBooth.Clear();
        seen.Clear();
        WrongBags = 0;
        foreach (OrderLine line in order)
            line.InBooth = 0;

        if (holdCentre == null) return;

        Vector3 centre = holdCentre.position;
        Vector3 halfExtents = new(holdRadius, holdHeight * 0.5f, holdRadius);
        int hits = Physics.OverlapBoxNonAlloc(centre + Vector3.up * (holdHeight * 0.5f), halfExtents,
                                              overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits; i++)
        {
            Collider hit = overlap[i];
            overlap[i] = null;
            if (!Luggage.TryGetFromCollider(hit, out Luggage bag) || !seen.Add(bag)) continue;

            // One a player is still holding isn't loaded yet.
            if (bag.IsDelivered || bag.GetIsGrabbed()) continue;

            Vector3 offset = bag.transform.position - centre;
            offset.y = 0f;
            if (offset.sqrMagnitude > holdRadius * holdRadius) continue;

            inBooth.Add(bag);
            OrderLine line = LineFor(bag);
            if (line != null)
                line.InBooth++;
            else
                WrongBags++;
        }

        int signature = WrongBags;
        foreach (OrderLine line in order)
            signature = signature * 31 + line.InBooth;

        if (signature == contentsSignature) return;
        contentsSignature = signature;
        OrderChanged?.Invoke();
    }

    // Level1 has no wrapper, so a wrapped bag fills nothing.
    private OrderLine LineFor(Luggage bag)
    {
        if (bag.IsWrapped) return null;

        foreach (OrderLine line in order)
            if (line.Color == bag.color)
                return line;

        return null;
    }

    private bool ContentsMatch()
    {
        if (order.Count == 0 || WrongBags > 0) return false;

        foreach (OrderLine line in order)
            if (!line.IsFilled)
                return false;

        return true;
    }

    private void GenerateOrder()
    {
        order.Clear();
        paletteDraw.Clear();
        paletteDraw.AddRange(palette);

        if (paletteDraw.Count == 0)
        {
            Debug.LogError($"{nameof(DispatchGate)} '{name}' has an empty colour palette and cannot build an order.", this);
        }
        else
        {
            int colours = Mathf.Clamp(UnityEngine.Random.Range(minColorsPerOrder, maxColorsPerOrder + 1),
                                      1, Mathf.Min(paletteDraw.Count, maxBagsPerOrder));
            int budget = maxBagsPerOrder;

            for (int i = 0; i < colours; i++)
            {
                // Drawn without replacement so one order never lists the same colour twice.
                int pick = UnityEngine.Random.Range(0, paletteDraw.Count);
                LuggageColor color = paletteDraw[pick];
                paletteDraw.RemoveAt(pick);

                // Leave at least one bag of budget for every colour still to come.
                int most = Mathf.Max(1, Mathf.Min(maxBagsPerColor, budget - (colours - i - 1)));
                int bags = Mathf.Clamp(UnityEngine.Random.Range(minBagsPerColor, maxBagsPerColor + 1), 1, most);
                budget -= bags;
                order.Add(new OrderLine(color, bags));
            }
        }

        TimeLeft = OrderTime;
        contentsSignature = -1;
        ScanBooth();
    }

    private void SetDoor(float shut)
    {
        if (door == null) return;

        Vector3 scale = door.localScale;
        scale.y = Mathf.Max(0.001f, shut);
        door.localScale = scale;
    }

    private void SetTrapdoor(float open)
    {
        if (trapdoor == null) return;

        trapdoor.gameObject.SetActive(open > 0f);
        trapdoor.localScale = new Vector3(trapdoorOpenScale.x * open, trapdoorOpenScale.y, trapdoorOpenScale.z * open);
    }

    private static IEnumerator Animate(float duration, Action<float> apply, float from, float to)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
            yield return null;
        }

        apply(to);
    }

    private void OnValidate()
    {
        maxColorsPerOrder = Mathf.Max(minColorsPerOrder, maxColorsPerOrder);
        maxBagsPerColor = Mathf.Max(minBagsPerColor, maxBagsPerColor);
        maxBagsPerOrder = Mathf.Max(minColorsPerOrder, maxBagsPerOrder);
    }

    private void OnDrawGizmosSelected()
    {
        if (holdCentre == null) return;

        Gizmos.color = Color.yellow;
        Vector3 c = holdCentre.position;
        for (int i = 0; i < 32; i++)
        {
            Vector3 a = c + Quaternion.Euler(0f, i * 360f / 32f, 0f) * Vector3.forward * holdRadius;
            Vector3 b = c + Quaternion.Euler(0f, (i + 1) * 360f / 32f, 0f) * Vector3.forward * holdRadius;
            Gizmos.DrawLine(a, b);
        }
    }
}
