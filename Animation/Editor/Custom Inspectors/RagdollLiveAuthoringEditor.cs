using UnityEditor;
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

        void RebuildWithUndo()
        {
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild live ragdoll");
            RagdollAuthoredRig previous = Author.AuthoredRig;
            if (previous)
            {
                DestroyOwned(previous.Joints);
                DestroyOwned(previous.Colliders);
                DestroyOwned(previous.Rigidbodies);
                Undo.DestroyObjectImmediate(previous);
                Author.SetAuthoredRig(null);
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
}
