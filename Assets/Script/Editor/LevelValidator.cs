using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarryOn.EditorTools
{
    /// <summary>
    /// Checks the things that are easy to forget when authoring a level: missing config,
    /// stations that do not match the luggage the level actually spawns, zeroed belt
    /// speeds, and conveyor seam rules (same uniform scale, same deck height).
    ///
    /// Menu: Carry On > Validate Open Scene / Validate All Build Scenes.
    /// Findings are logged with the offending object as context, so clicking the log
    /// selects it in the hierarchy.
    /// </summary>
    public static class LevelValidator
    {
        // Belt deck sits this far above the piece pivot at scale 1. Mating pieces must
        // agree or the seam steps and luggage catches on the lip.
        private const float DeckGauge = 4f;
        private const float HeightTolerance = 0.01f;
        private const string BeltSurfaceMaterialName = "BeltSurface";
        private const string BeltShaderName = "Shader Graphs/ConveyorBelt";
        private const string BeltAxisProperty = "_BeltAxis";

        private class Report
        {
            public readonly List<string> Errors = new();
            public readonly List<string> Warnings = new();

            public void Error(string message, Object context)
            {
                Errors.Add(message);
                Debug.LogError($"[LevelValidator] {message}", context);
            }

            public void Warn(string message, Object context)
            {
                Warnings.Add(message);
                Debug.LogWarning($"[LevelValidator] {message}", context);
            }
        }

        [MenuItem("Carry On/Validate Open Scene %#v")]
        public static void ValidateOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            Report report = new();
            Validate(scene, report);
            Summarize(scene.name, report);
        }

        [MenuItem("Carry On/Validate All Build Scenes")]
        public static void ValidateAllBuildScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string activeScenePath = SceneManager.GetActiveScene().path;
            Report report = new();

            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled)
                    continue;

                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                Validate(scene, report);
            }

            if (!string.IsNullOrEmpty(activeScenePath))
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

            Summarize("all build scenes", report);
        }

        private static void Summarize(string label, Report report)
        {
            if (report.Errors.Count == 0 && report.Warnings.Count == 0)
                Debug.Log($"[LevelValidator] {label}: OK — no problems found.");
            else
                Debug.Log($"[LevelValidator] {label}: {report.Errors.Count} error(s), {report.Warnings.Count} warning(s). See entries above.");
        }

        private static void Validate(Scene scene, Report report)
        {
            // Only gameplay scenes have a level to validate.
            LevelContext context = FindInScene<LevelContext>(scene).FirstOrDefault();
            if (context == null)
            {
                // A menu scene has no level rules to check, but it can still carry belts — and a
                // belt whose stripes scroll the wrong way under the luggage looks broken anywhere.
                foreach (Conveyor conveyor in FindInScene<Conveyor>(scene))
                    ValidateBeltStripes(conveyor, report);

                return;
            }

            LevelConfig config = ReadObject<LevelConfig>(context, "levelConfig");

            ValidateContext(scene, context, config, report);
            ValidateRequiredSystems(scene, report);
            ValidateLuggagePool(scene, config, report);
            ValidateStations(scene, report);
            ValidateConveyors(scene, report);
            ValidateGates(scene, report);
        }

        private static void ValidateContext(Scene scene, LevelContext context, LevelConfig config, Report report)
        {
            if (FindInScene<LevelContext>(scene).Count > 1)
                report.Error($"{scene.name}: more than one LevelContext in the scene.", context);

            if (config == null)
            {
                report.Error($"{scene.name}: LevelContext has no LevelConfig assigned.", context);
                return;
            }

            if (!string.IsNullOrWhiteSpace(config.sceneName) && config.sceneName != scene.name)
            {
                report.Error(
                    $"{scene.name}: LevelContext uses '{config.name}', whose sceneName is '{config.sceneName}'. " +
                    "The config is pointing at a different scene.", context);
            }

            if (config.luggagePrefabs == null || config.luggagePrefabs.Count == 0)
                report.Error($"{scene.name}: '{config.name}' has no luggagePrefabs — nothing will ever spawn.", config);
        }

        private static void ValidateRequiredSystems(Scene scene, Report report)
        {
            RequireOne<GameManager>(scene, report);
            RequireOne<LuggageSpawner>(scene, report);
            RequireOne<LuggageSink>(scene, report);

            PlayerSpawner spawner = FindInScene<PlayerSpawner>(scene).FirstOrDefault();
            if (spawner == null)
            {
                report.Error($"{scene.name}: no PlayerSpawner — players would stay wherever they were left.", null);
                return;
            }

            SerializedProperty points = new SerializedObject(spawner).FindProperty("spawnPoints");
            if (points == null || points.arraySize == 0)
                report.Error($"{scene.name}: PlayerSpawner has no spawn points assigned.", spawner);
        }

        private static void RequireOne<T>(Scene scene, Report report) where T : Component
        {
            if (FindInScene<T>(scene).Count == 0)
                report.Error($"{scene.name}: no {typeof(T).Name} in the scene.", null);
        }

        private static void ValidateLuggagePool(Scene scene, LevelConfig config, Report report)
        {
            if (config == null || config.luggagePrefabs == null)
                return;

            foreach (GameObject prefab in config.luggagePrefabs)
            {
                if (prefab == null)
                {
                    report.Error($"{scene.name}: '{config.name}' has an empty slot in luggagePrefabs.", config);
                    continue;
                }

                if (prefab.GetComponent<Luggage>() == null)
                    report.Error($"{scene.name}: luggage prefab '{prefab.name}' has no Luggage component.", config);
            }
        }

        private static void ValidateStations(Scene scene, Report report)
        {
            foreach (MachineStation station in FindInScene<MachineStation>(scene))
            {
                SerializedObject so = new(station);

                if (so.FindProperty("snapTransform")?.objectReferenceValue == null)
                    report.Error($"{station.name}: no snapTransform — luggage has nowhere to dock.", station);

                // A station carries its luggage one of two ways: riding an animated slider, or
                // walking the intake/output anchors in code. Having neither leaves the bag parked
                // on the snap point for the whole cycle; having both makes them fight in LateUpdate.
                bool hasSlider = so.FindProperty("sliderTransform")?.objectReferenceValue != null;
                bool hasAnchorWalk = so.FindProperty("intakeTransform")?.objectReferenceValue != null
                    || so.FindProperty("outputTransform")?.objectReferenceValue != null;

                if (!hasSlider && !hasAnchorWalk)
                {
                    report.Error(
                        $"{station.name}: no sliderTransform and no intake/output anchors — " +
                        "the bag never moves through the machine.", station);
                }
                else if (hasSlider && hasAnchorWalk)
                {
                    report.Error(
                        $"{station.name}: has both a sliderTransform and intake/output anchors — " +
                        "the slider follow in LateUpdate will fight the anchor walk. Pick one.", station);
                }

                bool animationDriven = so.FindProperty("animationDriven")?.boolValue ?? true;
                if (animationDriven && so.FindProperty("machineAnimator")?.objectReferenceValue == null)
                {
                    report.Error(
                        $"{station.name}: animationDriven is on but no Animator is assigned — " +
                        "it will never finish processing and the slot will stay occupied.", station);
                }
            }
        }

        /// <summary>
        /// Seam rules. Belts only line up if mating pieces share a uniform scale and sit at
        /// the same deck height; otherwise luggage steps, snags, or drops at the join.
        /// </summary>
        private static void ValidateConveyors(Scene scene, Report report)
        {
            List<Conveyor> conveyors = FindInScene<Conveyor>(scene);
            if (conveyors.Count == 0)
                return;

            Dictionary<float, int> deckHeights = new();

            foreach (Conveyor conveyor in conveyors)
            {
                SerializedObject so = new(conveyor);

                // Belt speed lives on the prefab now, so the failure to catch is a zeroed
                // value rather than an unassigned asset.
                if (so.FindProperty("moveSpeed")?.floatValue <= 0f)
                    report.Error($"{conveyor.name}: moveSpeed is 0 — the belt will not move luggage.", conveyor);

                // Turn pieces steer around a pivot; without it they fall back to straight.
                bool isTurn = so.FindProperty("shape")?.enumValueIndex == 1;
                if (isTurn && so.FindProperty("turnPivot")?.objectReferenceValue == null)
                    report.Error($"{conveyor.name}: Turn shape but no turnPivot — it will steer straight through the corner.", conveyor);

                Vector3 scale = conveyor.transform.lossyScale;
                if (!Approximately(scale.x, scale.y) || !Approximately(scale.y, scale.z))
                {
                    report.Error(
                        $"{conveyor.name}: non-uniform scale {Format(scale)}. Conveyor pieces must be uniformly scaled " +
                        "or the deck height and seam will not match neighbouring pieces.", conveyor);
                }

                if (!HasBeltSurfaceMaterial(conveyor))
                {
                    report.Warn(
                        $"{conveyor.name}: deck collider has no '{BeltSurfaceMaterialName}' physic material. " +
                        "Friction will fight the belt's velocity steering.", conveyor);
                }

                ValidateBeltStripes(conveyor, report);

                float deckHeight = conveyor.transform.position.y + DeckGauge * scale.y;
                float rounded = Mathf.Round(deckHeight * 100f) / 100f;
                deckHeights.TryGetValue(rounded, out int count);
                deckHeights[rounded] = count + 1;
            }

            // Every belt in a level normally runs at one height. More than one is usually a
            // mis-placed piece rather than a deliberate multi-level belt, so warn (not error).
            if (deckHeights.Count > 1)
            {
                string detail = string.Join(", ", deckHeights.OrderByDescending(p => p.Value)
                    .Select(p => $"y={p.Key} ({p.Value} piece(s))"));
                report.Warn(
                    $"{scene.name}: conveyor pieces sit at {deckHeights.Count} different deck heights — {detail}. " +
                    "Pieces that are meant to connect must share one height or the seam will step.", null);
            }
        }

        /// <summary>
        /// The belt's stripes scroll along a world-space axis stored on the material, while the
        /// luggage travels along the piece's own forward. Nothing links the two, so rotating a
        /// piece without swapping its material leaves the stripes crawling sideways or backwards
        /// under the bags. This is the check that catches it.
        /// </summary>
        private static void ValidateBeltStripes(Conveyor conveyor, Report report)
        {
            Material belt = null;
            foreach (Renderer renderer in conveyor.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader != null && material.shader.name == BeltShaderName)
                        belt = material;
                }
            }

            if (belt == null)
            {
                report.Warn(
                    $"{conveyor.name}: no '{BeltShaderName}' material anywhere on the piece, so its deck renders " +
                    "static while the belt still carries luggage. The belt material goes on the 'Model' child, " +
                    "not on the conveyor root.", conveyor);
                return;
            }

            if (!belt.HasProperty(BeltAxisProperty))
                return;

            Vector3 axis = belt.GetVector(BeltAxisProperty);
            Vector3 forward = conveyor.transform.forward;
            if (Vector3.Dot(axis.normalized, forward) < 0.9f)
            {
                report.Error(
                    $"{conveyor.name}: belt material '{belt.name}' scrolls along {Format(axis)} but the piece " +
                    $"faces {Format(forward)} — the stripes run sideways or backwards under the luggage. " +
                    "Assign the belt material whose axis matches the facing.", conveyor);
            }
        }

        private static bool HasBeltSurfaceMaterial(Conveyor conveyor)
        {
            foreach (Collider collider in conveyor.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger)
                    continue;

                PhysicsMaterial material = collider.sharedMaterial;
                if (material != null && material.name.Contains(BeltSurfaceMaterialName))
                    return true;
            }

            return false;
        }

        private static void ValidateGates(Scene scene, Report report)
        {
            List<Gate> gates = FindInScene<Gate>(scene);
            Dictionary<int, Gate> byNumber = new();

            foreach (Gate gate in gates)
            {
                int number = gate.GateNumber;
                if (byNumber.TryGetValue(number, out Gate existing))
                {
                    report.Error(
                        $"{gate.name}: gate number {number} is already used by '{existing.name}'. " +
                        "Destination routing needs unique numbers.", gate);
                    continue;
                }

                byNumber.Add(number, gate);
            }

            if (gates.Count == 0)
                report.Error($"{scene.name}: no delivery Gate — luggage can never be scored.", null);
        }

        private static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            List<T> found = new();
            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<T>(true));

            return found;
        }

        private static TObject ReadObject<TObject>(Component component, string propertyName) where TObject : Object
        {
            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            return property?.objectReferenceValue as TObject;
        }

        private static bool Approximately(float a, float b) => Mathf.Abs(a - b) < HeightTolerance;

        private static string Format(Vector3 value) => $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
    }
}
