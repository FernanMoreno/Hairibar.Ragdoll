using UnityEditor;
using System.Text;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollLiveAuthoring))]
    public sealed class RagdollLiveAuthoringEditor : UnityEditor.Editor
    {
        sealed class UndoObjectFactory : RagdollRuntimeAuthoring.IObjectFactory
        {
            public T AddComponent<T>(GameObject owner) where T : Component
            {
                return Undo.AddComponent<T>(owner);
            }

            public void Destroy(Object value)
            {
                if (value) Undo.DestroyObjectImmediate(value);
            }
        }

        static readonly UndoObjectFactory ObjectFactory =
            new UndoObjectFactory();

        SerializedProperty references;
        SerializedProperty options;
        SerializedProperty rebuildOnChange;

        RagdollLiveAuthoring Author => (RagdollLiveAuthoring)target;

        void OnEnable()
        {
            references = serializedObject.FindProperty("references");
            options = serializedObject.FindProperty("options");
            rebuildOnChange = serializedObject.FindProperty("rebuildOnChange");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(references, true);
            EditorGUILayout.PropertyField(options, true);
            EditorGUILayout.PropertyField(rebuildOnChange);
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                Undo.RegisterFullObjectHierarchyUndo(
                    Author.transform.root.gameObject,
                    "Edit live ragdoll authoring");
            }
            serializedObject.ApplyModifiedProperties();

            string error;
            bool valid = Author.TryValidate(out error);
            if (!valid) EditorGUILayout.HelpBox(error, MessageType.Error);
            else if (Author.AuthoredRig)
                EditorGUILayout.HelpBox("The owned ragdoll is valid and live-editable.", MessageType.Info);

            if (changed && rebuildOnChange.boolValue && valid)
            {
                RebuildWithUndo();
            }

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("Rebuild Authored Ragdoll")) RebuildWithUndo();
            }
            using (new EditorGUI.DisabledScope(!Author.AuthoredRig))
            {
                if (GUILayout.Button("Finish Authoring"))
                {
                    Undo.DestroyObjectImmediate(Author);
                }
            }
        }

        internal void RebuildWithUndo()
        {
            string ownershipError;
            if (!Author.TryValidateReplacementOwnership(out ownershipError))
            {
                UnityEngine.Debug.LogError(ownershipError, Author);
                return;
            }
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild live ragdoll");
            RagdollAuthoredRig previous = Author.AuthoredRig;
            if (previous)
            {
                GameObject stage;
                string stageError;
                if (!Author.TryBuildInactiveStage(out stage, out stageError))
                {
                    UnityEngine.Debug.LogError(stageError, Author);
                    return;
                }
                Undo.RegisterFullObjectHierarchyUndo(
                    Author.transform.root.gameObject,
                    "Rebuild live ragdoll");
                string rebuildError;
                bool rebuildSucceeded;
                try
                {
                    rebuildSucceeded = RagdollRuntimeAuthoring.TryRebuild(
                        previous,
                        Author.References,
                        Author.Options,
                        ObjectFactory,
                        out rebuildError);
                }
                finally { if (stage) Object.DestroyImmediate(stage); }
                if (!rebuildSucceeded)
                {
                    Undo.RevertAllDownToGroup(group);
                    UnityEngine.Debug.LogError(rebuildError, Author);
                    return;
                }
                Undo.RecordObject(Author, "Assign authored ragdoll");
                Author.MarkConfigurationApplied(
                    RagdollLiveAuthoringHashUtility.Compute(Author));
                EditorUtility.SetDirty(Author);
                Undo.CollapseUndoOperations(group);
                return;
            }

            RagdollAuthoredRig rebuilt;
            string error;
            if (!RagdollRuntimeAuthoring.TryBuild(
                Author.References,
                Author.Options,
                ObjectFactory,
                out rebuilt,
                out error))
            {
                Undo.RevertAllDownToGroup(group);
                UnityEngine.Debug.LogError(error, Author);
                return;
            }

            Undo.RecordObject(Author, "Assign authored ragdoll");
            Author.SetAuthoredRig(rebuilt);
            Author.MarkConfigurationApplied(
                RagdollLiveAuthoringHashUtility.Compute(Author));
            EditorUtility.SetDirty(Author);
            Undo.CollapseUndoOperations(group);
        }

        static void DestroyOwned<T>(T[] values) where T : Object
        {
            if (values == null) return;
            for (int index = values.Length - 1; index >= 0; index--)
                if (values[index]) Undo.DestroyObjectImmediate(values[index]);
        }

    }

    internal static class RagdollLiveAuthoringHashUtility
    {
        internal static string Compute(RagdollLiveAuthoring author)
        {
            if (!author) return string.Empty;
            StringBuilder value = new StringBuilder(1024);
            Append(value, author);
            RagdollBipedReferences references = author.References;
            if (references != null)
            {
                foreach (Transform bone in references.EnumerateAll())
                    Append(value, bone);
            }
            value.Append('|').Append(JsonUtility.ToJson(author.Options));
            return Hash128.Compute(value.ToString()).ToString();
        }

        static void Append(StringBuilder value, Object target)
        {
            value.Append('|');
            if (!target)
            {
                value.Append("null");
                return;
            }
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(target);
            value.Append(id.ToString());
        }
    }

    /// <summary>
    /// Editor-owned live change detector. Serialized hash observes script edits,
    /// prefab overrides and Undo/Redo, not only changes drawn by custom inspector.
    /// </summary>
    [InitializeOnLoad]
    static class RagdollLiveAuthoringChangeMonitor
    {
        static double nextPoll;
        static bool processing;

        static RagdollLiveAuthoringChangeMonitor()
        {
            EditorApplication.update += Poll;
            Undo.undoRedoPerformed += RequestPoll;
        }

        static void RequestPoll()
        {
            nextPoll = 0d;
        }

        static void Poll()
        {
            if (processing || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.25d;
            processing = true;
            try
            {
                RagdollLiveAuthoring[] authors = Resources
                    .FindObjectsOfTypeAll<RagdollLiveAuthoring>();
                for (int index = 0; index < authors.Length; index++)
                {
                    RagdollLiveAuthoring author = authors[index];
                    if (!author || EditorUtility.IsPersistent(author)
                        || !author.RebuildOnChangeEnabled) continue;
                    string stableHash =
                        RagdollLiveAuthoringHashUtility.Compute(author);
                    if (stableHash == author.AppliedConfigurationHash) continue;
                    string error;
                    if (!author.TryValidate(out error)
                        || !author.TryValidateReplacementOwnership(out error))
                        continue;
                    RagdollLiveAuthoringEditor inspector =
                        UnityEditor.Editor.CreateEditor(
                            author, typeof(RagdollLiveAuthoringEditor))
                        as RagdollLiveAuthoringEditor;
                    if (!inspector) continue;
                    try { inspector.RebuildWithUndo(); }
                    finally { Object.DestroyImmediate(inspector); }
                }
            }
            finally { processing = false; }
        }
    }
}
