using System;
using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.Experiment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GlobeEffect.VRCheckerboard.Editor
{
    /// <summary>
    /// Knöpfe "Vorschau-Werte übernehmen" und "Originalwerte wiederherstellen"
    /// für beide Experiment Manager (Random Dots und Checkerboard).
    ///
    /// Unity verwirft Änderungen aus dem Play Mode beim Stoppen. Deshalb merkt
    /// sich diese Klasse das Ergebnis und schreibt es nach dem Stoppen in die
    /// Szene (mit Undo). Gespeichert wird die Szene wie gewohnt mit Strg+S.
    /// </summary>
    [InitializeOnLoad]
    internal static class PreviewTakeoverEditor
    {
        private const string PendingKey = "GlobeEffect.PreviewTakeover.Pending";

        static PreviewTakeoverEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void TakeOver(MonoBehaviour manager)
        {
            Change(manager, "Use Preview Values",
                () => ((IPreviewTakeoverManager)manager).TakeOverPreviewValues());
        }

        public static void Restore(MonoBehaviour manager)
        {
            Change(manager, "Restore Original Values",
                () => ((IPreviewTakeoverManager)manager).RestoreValuesBeforePreviewTakeover());
        }

        private static void Change(MonoBehaviour manager, string actionName, Func<bool> action)
        {
            if (Application.isPlaying)
            {
                if (!action()) return;
                RememberForEditMode(manager);
                Debug.Log(actionName + ": gilt sofort für die nächste Sitzung und wird nach dem Stoppen " +
                    "des Play Mode in die Szene übernommen.", manager);
                return;
            }

            RecordUndo(manager, actionName);
            if (!action()) return;
            MarkChanged(manager);
            Debug.Log(actionName + ": erledigt. Szene speichern (Strg+S), damit es bleibt.", manager);
        }

        private static void RememberForEditMode(MonoBehaviour manager)
        {
            var takeover = (IPreviewTakeoverManager)manager;
            var pending = new Pending
            {
                typeName = manager.GetType().FullName,
                scenePath = manager.gameObject.scene.path,
                objectPath = HierarchyPath(manager.transform),
                values = takeover.CaptureExperimentValues(),
                valuesBeforeTakeover = takeover.ValuesBeforePreviewTakeover
            };

            // Ein Eintrag je Manager; ein neuerer ersetzt den älteren.
            PendingList list = LoadPending();
            list.items.RemoveAll(item => item.typeName == pending.typeName
                && item.scenePath == pending.scenePath && item.objectPath == pending.objectPath);
            list.items.Add(pending);
            SessionState.SetString(PendingKey, JsonUtility.ToJson(list));
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            PendingList list = LoadPending();
            SessionState.EraseString(PendingKey);

            foreach (Pending pending in list.items)
            {
                MonoBehaviour manager = Find(pending);
                if (manager == null)
                {
                    Debug.LogWarning("Vorschau-Werte konnten nicht in die Szene übernommen werden: " +
                        pending.typeName + " nicht gefunden (" + pending.objectPath + ").");
                    continue;
                }

                var takeover = (IPreviewTakeoverManager)manager;
                RecordUndo(manager, "Preview Values From Play Mode");
                takeover.ApplyExperimentValues(pending.values);
                takeover.ValuesBeforePreviewTakeover = pending.valuesBeforeTakeover;
                MarkChanged(manager);
                Debug.Log("Vorschau-Werte aus dem Play Mode in die Szene übernommen. " +
                    "Szene speichern (Strg+S), damit sie bleiben.", manager);
            }
        }

        private static PendingList LoadPending()
        {
            string json = SessionState.GetString(PendingKey, string.Empty);
            PendingList list = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PendingList>(json);
            list ??= new PendingList();
            list.items ??= new List<Pending>();
            return list;
        }

        private static MonoBehaviour Find(Pending pending)
        {
            foreach (MonoBehaviour candidate in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (candidate is IPreviewTakeoverManager
                    && candidate.GetType().FullName == pending.typeName
                    && candidate.gameObject.scene.path == pending.scenePath
                    && HierarchyPath(candidate.transform) == pending.objectPath)
                    return candidate;
            }

            return null;
        }

        private static void RecordUndo(MonoBehaviour manager, string actionName)
        {
            Object stimulus = ((IPreviewTakeoverManager)manager).PreviewStimulus;
            if (stimulus != null)
                Undo.RecordObjects(new[] { manager, stimulus }, actionName);
            else
                Undo.RecordObject(manager, actionName);
        }

        private static void MarkChanged(MonoBehaviour manager)
        {
            Object stimulus = ((IPreviewTakeoverManager)manager).PreviewStimulus;
            foreach (Object changed in new[] { manager, stimulus })
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
            public string typeName;
            public string scenePath;
            public string objectPath;
            public string values;
            public string valuesBeforeTakeover;
        }

        [Serializable]
        private sealed class PendingList
        {
            public List<Pending> items = new();
        }
    }
}
