using System;
using GlobeEffect.VRCheckerboard.Experiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GlobeEffect.VRCheckerboard.Editor
{
    /// <summary>
    /// Knöpfe "Vorschau-Werte übernehmen" und "Originalwerte wiederherstellen".
    ///
    /// Unity verwirft Änderungen aus dem Play Mode beim Stoppen. Deshalb merkt
    /// sich diese Klasse das Ergebnis und schreibt es nach dem Stoppen in die
    /// Szene (mit Undo). Gespeichert wird die Szene wie gewohnt mit Strg+S.
    /// </summary>
    [InitializeOnLoad]
    internal static class RandomDotPreviewTakeoverEditor
    {
        private const string PendingKey = "GlobeEffect.RandomDotPreviewTakeover.Pending";

        static RandomDotPreviewTakeoverEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void TakeOver(RandomDotExperimentManager manager)
        {
            Change(manager, "Vorschau-Werte übernehmen", () => manager.TakeOverPreviewValues());
        }

        public static void Restore(RandomDotExperimentManager manager)
        {
            Change(manager, "Originalwerte wiederherstellen", () => manager.RestoreValuesBeforePreviewTakeover());
        }

        private static void Change(RandomDotExperimentManager manager, string actionName, Func<bool> action)
        {
            if (Application.isPlaying)
            {
                if (!action()) return;
                RememberForEditMode(manager);
                Debug.Log(actionName + ": gilt sofort für F5/F7 und wird nach dem Stoppen " +
                    "des Play Mode in die Szene übernommen.", manager);
                return;
            }

            RecordUndo(manager, actionName);
            if (!action()) return;
            MarkChanged(manager);
            Debug.Log(actionName + ": erledigt. Szene speichern (Strg+S), damit es bleibt.", manager);
        }

        private static void RememberForEditMode(RandomDotExperimentManager manager)
        {
            var pending = new Pending
            {
                scenePath = manager.gameObject.scene.path,
                objectPath = HierarchyPath(manager.transform),
                values = manager.CaptureExperimentValues(),
                valuesBeforeTakeover = manager.ValuesBeforePreviewTakeover
            };
            SessionState.SetString(PendingKey, JsonUtility.ToJson(pending));
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            string json = SessionState.GetString(PendingKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            SessionState.EraseString(PendingKey);

            Pending pending = JsonUtility.FromJson<Pending>(json);
            RandomDotExperimentManager manager = Find(pending);
            if (manager == null)
            {
                Debug.LogWarning("Vorschau-Werte konnten nicht in die Szene übernommen werden: " +
                    "Random Dot Experiment Manager nicht gefunden (" + pending.objectPath + ").");
                return;
            }

            RecordUndo(manager, "Vorschau-Werte aus dem Play Mode");
            manager.ApplyExperimentValues(pending.values);
            manager.ValuesBeforePreviewTakeover = pending.valuesBeforeTakeover;
            MarkChanged(manager);
            Debug.Log("Vorschau-Werte aus dem Play Mode in die Szene übernommen. " +
                "Szene speichern (Strg+S), damit sie bleiben.", manager);
        }

        private static RandomDotExperimentManager Find(Pending pending)
        {
            foreach (RandomDotExperimentManager candidate in Object.FindObjectsByType<RandomDotExperimentManager>(
                         FindObjectsInactive.Include))
            {
                if (candidate.gameObject.scene.path == pending.scenePath
                    && HierarchyPath(candidate.transform) == pending.objectPath)
                    return candidate;
            }

            return null;
        }

        private static void RecordUndo(RandomDotExperimentManager manager, string actionName)
        {
            if (manager.Stimulus != null)
                Undo.RecordObjects(new Object[] { manager, manager.Stimulus }, actionName);
            else
                Undo.RecordObject(manager, actionName);
        }

        private static void MarkChanged(RandomDotExperimentManager manager)
        {
            foreach (Object changed in new Object[] { manager, manager.Stimulus })
            {
                if (changed == null) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
                EditorUtility.SetDirty(changed);
            }

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        [Serializable]
        private sealed class Pending
        {
            public string scenePath;
            public string objectPath;
            public string values;
            public string valuesBeforeTakeover;
        }
    }
}
