using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The dashed route from one stage-select node to the next stage's node, drawn on the sea.
///
/// Lives inside its node (the Map Node prefab's Route child) and finds the other end itself: the
/// node whose LevelConfig is this node's <see cref="LevelConfig.nextLevel"/>. So the route always
/// follows the real unlock order — give an island a different LevelConfig, or change a config's
/// next level, and the routes redraw. A stage with no next level (or whose next level isn't on
/// the map) draws nothing.
///
/// The LineRenderer and its Route Dash material live in the prefab; this only lays the line's
/// points along a curve from just outside one node to just outside the next, and tells the dash
/// shader the line's width and length. Two bends give every route its own shape: the same sign on
/// both curves it one way, opposite signs make an S.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class MapRoute : MonoBehaviour
{
    [Tooltip("Sideways push of the curve near the start, in metres. Positive bends to the right " +
             "of travel.")]
    [SerializeField] private float bendStart = 10f;
    [Tooltip("Sideways push near the end. Same sign as Bend Start curves the route one way; " +
             "the opposite sign makes an S.")]
    [SerializeField] private float bendEnd = 5f;
    [Tooltip("Gap left around each node, so the route starts and ends just outside its ring.")]
    [SerializeField, Min(0f)] private float nodeGap = 12f;
    [Tooltip("World height of the line, just above the sea.")]
    [SerializeField] private float height = 2.45f;

    private const int CurveSteps = 160;
    private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
    private static readonly int RouteLengthId = Shader.PropertyToID("_RouteLength");

    private readonly List<Vector3> points = new();
    private LineRenderer line;
    private MaterialPropertyBlock block;
    private LevelNode from;
    private LevelNode to;
    private Vector3 builtFrom;
    private Vector3 builtTo;
    private bool dirty = true;

    private void OnEnable() => dirty = true;

    // Setting the line from OnValidate can trip Unity's "SendMessage during OnValidate"
    // warning, so it only flags the rebuild.
    private void OnValidate() => dirty = true;

    private void LateUpdate()
    {
        line ??= GetComponent<LineRenderer>();
        if (from == null)
            from = GetComponentInParent<LevelNode>();

        LevelConfig next = from != null && from.Level != null ? from.Level.nextLevel : null;
        if (to == null || to.Level != next)
        {
            to = FindNode(next);
            dirty = true;
        }

        if (to == null)
        {
            if (line.positionCount != 0)
                line.positionCount = 0;
            return;
        }

        // Nodes don't move in play; in the editor this keeps the route on an island being dragged.
        if (dirty || from.transform.position != builtFrom || to.transform.position != builtTo)
            Rebuild();
    }

    private static LevelNode FindNode(LevelConfig level)
    {
        if (level == null)
            return null;

        foreach (LevelNode node in FindObjectsByType<LevelNode>(FindObjectsSortMode.None))
            if (node.Level == level)
                return node;
        return null;
    }

    private void Rebuild()
    {
        dirty = false;
        block ??= new MaterialPropertyBlock();
        builtFrom = from.transform.position;
        builtTo = to.transform.position;

        Vector3 start = new(builtFrom.x, height, builtFrom.z);
        Vector3 end = new(builtTo.x, height, builtTo.z);
        Vector3 span = end - start;
        Vector3 right = Vector3.Cross(Vector3.up, span.normalized);
        Vector3 control1 = start + span / 3f + right * bendStart;
        Vector3 control2 = start + span * (2f / 3f) + right * bendEnd;

        // Walk the whole curve and keep only the stretch between the two rings.
        points.Clear();
        float length = 0f;
        for (int i = 0; i <= CurveSteps; i++)
        {
            Vector3 point = Bezier(start, control1, control2, end, i / (float)CurveSteps);
            if (Flat(point - start) < nodeGap || Flat(point - end) < nodeGap)
                continue;

            if (points.Count > 0)
                length += Vector3.Distance(points[^1], point);
            points.Add(point);
        }

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());

        line.GetPropertyBlock(block);
        block.SetFloat(LineWidthId, line.widthMultiplier * line.widthCurve.Evaluate(0.5f));
        block.SetFloat(RouteLengthId, length);
        line.SetPropertyBlock(block);
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }
}
