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
    /// stations that do not match the luggage the level actually spawns, unwired tuning
    /// assets, and conveyor seam rules (same uniform scale, same deck height).
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
                return;

            LevelConfig config = ReadObject<LevelConfig>(context, "levelConfig");

            ValidateContext(scene, context, config, report);
            ValidateRequiredSystems(scene, report);
            ValidateStationsMatchContent(scene, config, report);
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

        /// <summary>
        /// The check that matters most: a level that spawns Sticky bags but has no washer
        /// is unwinnable — every delivery of those bags is a guaranteed penalty.
        /// </summary>
        private static void ValidateStationsMatchContent(Scene scene, LevelConfig config, Report report)
        {
            if (config == null || config.luggagePrefabs == null)
                return;

            HashSet<LuggageBehaviorType> spawned = new();
            foreach (GameObject prefab in config.luggagePrefabs)
            {
                if (prefab == null)
                {
                    report.Error($"{scene.name}: '{config.name}' has an empty slot in luggagePrefabs.", config);
                    continue;
                }

                Luggage luggage = prefab.GetComponent<Luggage>();
                if (luggage == null)
                {
                    report.Error($"{scene.name}: luggage prefab '{prefab.name}' has no Luggage component.", config);
                    continue;
                }

                spawned.Add(luggage.behaviorType);
            }

            if (spawned.Contains(LuggageBehaviorType.Sticky) && FindInScene<WashingMachine>(scene).Count == 0)
            {
                report.Error(
                    $"{scene.name}: spawns Sticky luggage but has no WashingMachine. " +
                    "Those bags can never be processed — every delivery is a penalty.", config);
            }

            if (spawned.Contains(LuggageBehaviorType.Fragile) && FindInScene<Wrapper>(scene).Count == 0)
            {
                report.Error(
                    $"{scene.name}: spawns Fragile luggage but has no Wrapper. " +
                    "Those bags can never be processed — every delivery is a penalty.", config);
            }

        }

        private static void ValidateStations(Scene scene, Report report)
        {
            foreach (MachineStation station in FindInScene<MachineStation>(scene))
            {
                SerializedObject so = new(station);

                if (so.FindProperty("stationTuning")?.objectReferenceValue == null)
                    report.Error($"{station.name}: no StationTuning assigned.", station);

                if (so.FindProperty("snapTransform")?.objectReferenceValue == null)
                    report.Error($"{station.name}: no snapTransform — luggage has nowhere to dock.", station);

                if (so.FindProperty("sliderTransform")?.objectReferenceValue == null)
                    report.Error($"{station.name}: no sliderTransform.", station);

                bool animationDriven = so.FindProperty("animationDriven")?.boolValue ?? true;
                if (animationDriven && so.FindProperty("machineAnimator")?.objectReferenceValue == null)
                {
                    report.Error(
                        $"{station.name}: animationDriven is on but no Animator is assigned — " +
                        "it will never finish processing and the slot will stay occupied.", station);
                }

                switch (station)
                {
                    case WashingMachine washer
                        when new SerializedObject(washer).FindProperty("washedLuggagePrefab")?.objectReferenceValue == null:
                        report.Warn($"{washer.name}: no washedLuggagePrefab — falls back to an in-place swap.", washer);
                        break;

                    case Wrapper wrapper
                        when new SerializedObject(wrapper).FindProperty("wrappedLuggagePrefab")?.objectReferenceValue == null:
                        report.Warn($"{wrapper.name}: no wrappedLuggagePrefab — falls back to an in-place swap.", wrapper);
                        break;
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

                if (so.FindProperty("tuning")?.objectReferenceValue == null)
                    report.Error($"{conveyor.name}: no ConveyorTuning assigned — the belt will not steer.", conveyor);

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
