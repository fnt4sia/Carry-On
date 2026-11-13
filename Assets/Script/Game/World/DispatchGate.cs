using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The new Level1 gate: a glass booth a flight's bags are loaded into. Players carry in the bags the
// order lists, then press the dispatch button beside the gate. The doors slide shut, the booth floor
// opens and everything inside drops away, a player standing in there included (they come back at
// their spawn point a few seconds later). The flight pays when the booth held at least every bag
// the order lists; spare or wrong bags don't matter, they just drop with the rest. A missing bag,
// or the order's timer running out, sends the lot down the hole for nothing and breaks the combo.
//
// Gate, the instant trigger delivery that bounces wrong bags back out, is the older flow. Level2 and
// the main menu still use it; the two never meet in one scene.
public class DispatchGate : MonoBehaviour
{
    // One colour of the current order: how many it wants, whether wrapped, and how many sit in the
    // booth right now. The booth count goes down as well as up, since a player can carry a bag back out.
    public class OrderLine
    {
        public OrderLine(LuggageColor color, bool wrapped, int required)
        {
            Color = color;
            Wrapped = wrapped;
            Required = required;
        }

        public LuggageColor Color { get; }
        public bool Wrapped { get; }
        public int Required { get; }
        public int InBooth { get; set; }
        public bool IsFilled => InBooth >= Required;
    }

    // Waiting is the opening pause before the first order, with the doors shut.
    public enum Phase { Waiting, Loading, Dispatching }

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
    [Tooltip("How many of an order's colours must arrive wrapped. Needs a wrapping machine in the " +
             "scene. 0 = never, which is Level1.")]
    [SerializeField, Min(0)] private int wrappedLinesPerOrder;

    [Header("Booth")]
    [Tooltip("Centre of the booth floor. A bag whose centre is within holdRadius of it, and below " +
             "holdHeight, is in the booth.")]
    [SerializeField] private Transform holdCentre;
    [Tooltip("World metres. Set it to the glass's inner radius: a bag pushed up against the glass " +
             "still has its centre inside it, and one on the threshold counts once its centre is in.")]
    [SerializeField, Min(0.1f)] private float holdRadius = 7.3f;
    [SerializeField, Min(0.1f)] private float holdHeight = 7f;

    [Header("Dispatch")]
    [Tooltip("The two curved glass doors: pivots on the booth's axis, authored open. Each slides " +
             "round the drum by its shut angle to close the entrance. Their colliders stop bags, " +
             "never players, so nobody gets shut inside.")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private float leftDoorShutAngle = -28.5f;
    [SerializeField] private Transform rightDoor;
    [SerializeField] private float rightDoorShutAngle = 33.7f;
    [Tooltip("Black disc over the whole booth floor that opens like an iris under the bags.")]
    [SerializeField] private Transform trapdoor;
    [SerializeField, Min(0.05f)] private float doorTime = 0.6f;
    [SerializeField, Min(0.05f)] private float trapdoorTime = 0.25f;
    [Tooltip("How long everything on the floor takes to fall through it once it opens.")]
    [SerializeField, Min(0.1f)] private float dropTime = 0.9f;
    [Tooltip("Pause with the floor shut again before the doors open on the next order.")]
    [SerializeField, Min(0f)] private float nextOrderDelay = 0.6f;
    [Tooltip("Seconds a player who fell through the floor is gone before reappearing at their spawn point.")]
    [SerializeField, Min(0f)] private float playerRespawnDelay = 3f;
    [Tooltip("Seconds after the round starts before the doors open on the first order. The countdown " +
             "runs at time scale 0, so this only starts counting at GO, while the belt starts at once.")]
    [SerializeField, Min(0f)] private float firstOrderDelay = 3f;

    private const float FallbackOrderTime = 20f;

    private readonly List<OrderLine> order = new();
    private readonly List<LuggageColor> paletteDraw = new();
    private readonly List<Luggage> inBooth = new();
    private readonly HashSet<Luggage> seen = new();
    private readonly HashSet<PlayerInput> respawning = new();
    private readonly Collider[] overlap = new Collider[64];
    private Quaternion leftDoorOpen;
    private Quaternion rightDoorOpen;
    private Vector3 trapdoorOpenScale;
    private int contentsSignature = -1;

    public int GateNumber => gateNumber;
    public IReadOnlyList<OrderLine> Order => order;
    public Phase CurrentPhase { get; private set; }
    public float TimeLeft { get; private set; }
    public float OrderTime { get; private set; } = FallbackOrderTime;

    /// <summary>Flights dispatched in a row with the right bags. A failed one resets it.</summary>
    public int Streak { get; private set; }

    /// <summary>True when pressing the button now would pay out.</summary>
    public bool IsReady => CurrentPhase == Phase.Loading && IsOrderFilled();

    // Raised when a new order arrives or the booth's contents change, so the screen redraws
    // without polling every frame. The timer bar is the only thing it reads per frame.
    public event Action OrderChanged;

    // Raised once per flight, when the door is shut and the booth judged: success, coins paid.
    public event Action<bool, int> Dispatched;

    private void Awake()
    {
        if (leftDoor != null) leftDoorOpen = leftDoor.localRotation;
        if (rightDoor != null) rightDoorOpen = rightDoor.localRotation;

        if (trapdoor != null)
        {
            trapdoorOpenScale = trapdoor.localScale;
            SetTrapdoor(0f);
        }

        SetDoor(1f);
    }

    private IEnumerator Start()
    {
        LevelConfig config = LevelContext.CurrentConfig;
        if (config != null)
            OrderTime = config.orderTime;
        else
            Debug.LogError($"{nameof(DispatchGate)} '{name}' needs an active {nameof(LevelContext)}.", this);

        yield return new WaitForSeconds(firstOrderDelay);
        yield return Animate(doorTime, SetDoor, from: 1f, to: 0f);

        CurrentPhase = Phase.Loading;
        GenerateOrder();
    }

    private void Update()
    {
        if (CurrentPhase != Phase.Loading) return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft > 0f) return;

        TimeLeft = 0f;
        StartCoroutine(Dispatch(timedOut: true));
    }

    private void FixedUpdate()
    {
        if (CurrentPhase == Phase.Loading)
            ScanBooth();
    }

    // Called by DispatchButton. Works whatever the booth holds; the verdict comes after the door shuts.
    public void Press()
    {
        if (CurrentPhase != Phase.Loading) return;
        StartCoroutine(Dispatch(timedOut: false));
    }

    private IEnumerator Dispatch(bool timedOut)
    {
        CurrentPhase = Phase.Dispatching;

        yield return Animate(doorTime, SetDoor, from: 0f, to: 1f);

        // Judged behind the shut door, on exactly the bags that are about to drop.
        ScanBooth();
        bool success = !timedOut && IsOrderFilled();
        int coins = 0;
        if (success)
        {
            Streak++;
            coins = PayOut();
        }
        else
        {
            Streak = 0;
        }

        AudioManager.Instance?.PlaySFX(success ? Sfx.Score : Sfx.Wrong);
        Dispatched?.Invoke(success, coins);

        yield return Animate(trapdoorTime, SetTrapdoor, from: 0f, to: 1f);
        yield return Drop(new List<Luggage>(inBooth), PlayersInBooth());
        yield return Animate(trapdoorTime, SetTrapdoor, from: 1f, to: 0f);
        yield return new WaitForSeconds(nextOrderDelay);

        yield return Animate(doorTime, SetDoor, from: 1f, to: 0f);

        CurrentPhase = Phase.Loading;
        GenerateOrder();
    }

    // Everything on the open floor falls straight down. Kinematic, so nothing on the way down can
    // catch it. Bags go back to the pool, which puts physics back; players are hidden, then come
    // back at their spawn point after playerRespawnDelay.
    private IEnumerator Drop(List<Luggage> bags, List<PlayerInput> players)
    {
        var falling = new List<Transform>();
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

            falling.Add(bag.transform);
        }

        foreach (PlayerInput player in players)
        {
            // A bag held across the doorway stays outside; it mustn't be dragged down on its joint.
            player.GetComponent<PlayerGrab>()?.Drop();
            if (player.TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            falling.Add(player.transform);
        }

        List<Vector3> starts = falling.ConvertAll(t => t.position);
        for (float t = 0f; t < dropTime; t += Time.deltaTime)
        {
            float fall = 0.5f * -Physics.gravity.y * t * t;
            for (int i = 0; i < falling.Count; i++)
                if (falling[i] != null)
                    falling[i].position = starts[i] + Vector3.down * fall;

            yield return null;
        }

        foreach (Luggage bag in bags)
            if (bag != null)
                bag.DestroyLuggage();

        if (players.Count > 0)
            StartCoroutine(Respawn(players));
    }

    private List<PlayerInput> PlayersInBooth()
    {
        var players = new List<PlayerInput>();
        foreach (PlayerInput player in PlayerInput.all)
            if (IsInBooth(player.transform.position))
                players.Add(player);

        return players;
    }

    // Like PoolHazard: hidden for the delay (the follow camera stops framing an inactive player),
    // then back at their spawn point. An inactive PlayerInput reads playerIndex -1, so each is moved
    // only once active again, and they come back in player order: re-enabling hands a player the
    // lowest free index, which is then their own.
    private IEnumerator Respawn(List<PlayerInput> players)
    {
        foreach (PlayerInput player in players)
        {
            respawning.Add(player);
            player.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(playerRespawnDelay);

        foreach (PlayerInput player in players)
        {
            respawning.Remove(player);
            if (player == null) continue;

            if (player.TryGetComponent(out Rigidbody body)) body.isKinematic = false;
            player.gameObject.SetActive(true);
            if (PlayerSpawner.Instance != null) PlayerSpawner.Instance.MovePlayerToSpawn(player);
        }
    }

    // Players outlive the scene, so one still hidden when the level unloads must come back here.
    private void OnDisable()
    {
        foreach (PlayerInput player in respawning)
        {
            if (player == null) continue;
            if (player.TryGetComponent(out Rigidbody body)) body.isKinematic = false;
            player.gameObject.SetActive(true);
        }

        respawning.Clear();
    }

    // coins = (per-bag coins x the bags the order lists + coins per whole second left) x combo
    // multiplier. Spare or wrong bags in the booth earn nothing, so stuffing it doesn't pay. Each
    // ordered bag is a delivery; the time bonus and rounding go on top as plain score.
    private int PayOut()
    {
        LevelConfig config = LevelContext.CurrentConfig;
        if (config == null) return 0;

        float multiplier = config.ComboMultiplier(Streak);
        int bags = 0;
        foreach (OrderLine line in order)
            bags += line.Required;

        int total = Mathf.RoundToInt(
            (config.scoreCorrectDelivery * bags + config.coinsPerSecondLeft * Mathf.FloorToInt(TimeLeft))
            * multiplier);

        int perBag = Mathf.RoundToInt(config.scoreCorrectDelivery * multiplier);
        int paid = 0;
        for (int i = 0; i < bags; i++)
            if (RoundScoreContext.TryRecordDelivery(perBag))
                paid += perBag;

        if (total != paid)
            RoundScoreContext.TryApplyScore(total - paid);

        return total;
    }

    private void ScanBooth()
    {
        inBooth.Clear();
        seen.Clear();
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
            if (!Luggage.TryGetFromCollider(hit, out Luggage bag) || !seen.Add(bag) || bag.IsDelivered) continue;

            // A bag a player is carrying counts too: if the floor opens it goes down with them.
            // The collider's centre, not the transform: a bag's pivot sits well off its middle, so
            // one lying on its side against the glass had its pivot outside the radius.
            Collider body = bag.SurfaceCollider != null ? bag.SurfaceCollider : hit;
            if (!IsInBooth(body.bounds.center)) continue;

            // Everything in the booth drops; only the colours the order lists are counted.
            inBooth.Add(bag);
            OrderLine line = LineFor(bag);
            if (line != null)
                line.InBooth++;
        }

        int signature = 0;
        foreach (OrderLine line in order)
            signature = signature * 31 + line.InBooth;

        if (signature == contentsSignature) return;
        contentsSignature = signature;
        OrderChanged?.Invoke();
    }

    // Over the floor: within holdRadius of the centre and between just under the floor and holdHeight.
    private bool IsInBooth(Vector3 point)
    {
        if (holdCentre == null) return false;

        Vector3 offset = point - holdCentre.position;
        if (offset.y < -1f || offset.y > holdHeight) return false;

        offset.y = 0f;
        return offset.sqrMagnitude <= holdRadius * holdRadius;
    }

    // A wrapped bag fills only a wrapped line and a plain bag only a plain one. Level1 asks for no
    // wrapped lines, so there a wrapped bag fills nothing.
    private OrderLine LineFor(Luggage bag)
    {
        foreach (OrderLine line in order)
            if (line.Color == bag.color && line.Wrapped == bag.IsWrapped)
                return line;

        return null;
    }

    // Every line at or over its count. What else is in the booth doesn't matter.
    private bool IsOrderFilled()
    {
        if (order.Count == 0) return false;

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
            int wrappedLines = Mathf.Min(wrappedLinesPerOrder, colours);

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
                // The colours are already a random draw, so wrapping the first lines is as random
                // as picking which ones.
                order.Add(new OrderLine(color, wrapped: i < wrappedLines, bags));
            }
        }

        TimeLeft = OrderTime;
        contentsSignature = -1;
        ScanBooth();
    }

    private void SetDoor(float shut)
    {
        if (leftDoor != null)
            leftDoor.localRotation = leftDoorOpen * Quaternion.Euler(0f, leftDoorShutAngle * shut, 0f);
        if (rightDoor != null)
            rightDoor.localRotation = rightDoorOpen * Quaternion.Euler(0f, rightDoorShutAngle * shut, 0f);
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
