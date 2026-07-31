using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.NaughtyExtensions
{
    /// <summary>Marks a backing field whose Inspector writes must use its property.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UsePropertySetterAttribute : PropertyAttribute
    {
        public string PropertyName { get; }
        public bool AutoFindProperty => string.IsNullOrEmpty(PropertyName);

        public UsePropertySetterAttribute() { }
        public UsePropertySetterAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}

namespace NaughtyAttributes
{
    /// <summary>Compatibility metadata retained for the package samples.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionName { get; }
        public ShowIfAttribute(string conditionName) { ConditionName = conditionName; }
    }

    /// <summary>Compatibility metadata retained for sample inspector actions.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ButtonAttribute : Attribute
    {
        public string Text { get; }
        public ButtonAttribute(string text = null) { Text = text; }
    }
}

namespace Hairibar.EngineExtensions.Serialization
{
    /// <summary>Unity-serializable dictionary with explicit key/value lists.</summary>
    [Serializable]
    public abstract class SerializableDictionary<TKey, TValue>
        : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] List<TKey> keys = new List<TKey>();
        [SerializeField] List<TValue> values = new List<TValue>();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            int count = Math.Min(keys.Count, values.Count);
            for (int index = 0; index < count; index++)
            {
                if (ReferenceEquals(keys[index], null) || ContainsKey(keys[index])) continue;
                Add(keys[index], values[index]);
            }
        }
    }
}

namespace Hairibar.EngineExtensions
{
    public static class RagdollTransformExtensions
    {
        public static Transform FindChildRecursively(
            this Transform root,
            string childName)
        {
            if (!root || string.IsNullOrEmpty(childName)) return null;
            if (root.name == childName) return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = root.GetChild(index)
                    .FindChildRecursively(childName);
                if (found) return found;
            }
            return null;
        }
    }

    public static class RagdollConfigurableJointExtensions
    {
        public static void SetTargetRotation(
            this ConfigurableJoint joint,
            Quaternion targetWorldRotation,
            Quaternion startWorldRotation)
        {
            if (!joint) throw new ArgumentNullException(nameof(joint));
            Quaternion jointSpace = GetJointSpace(joint);
            joint.targetRotation = Quaternion.Inverse(jointSpace)
                * (startWorldRotation * Quaternion.Inverse(targetWorldRotation))
                * jointSpace;
        }

        public static void SetTargetRotationLocal(
            this ConfigurableJoint joint,
            Quaternion targetLocalRotation,
            Quaternion startLocalRotation)
        {
            if (!joint) throw new ArgumentNullException(nameof(joint));
            Quaternion jointSpace = GetJointSpace(joint);
            joint.targetRotation = Quaternion.Inverse(jointSpace)
                * (Quaternion.Inverse(targetLocalRotation) * startLocalRotation)
                * jointSpace;
        }

        static Quaternion GetJointSpace(ConfigurableJoint joint)
        {
            Vector3 right = joint.axis.normalized;
            Vector3 secondary = joint.secondaryAxis.normalized;
            Vector3 forward = Vector3.Cross(right, secondary).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            if (right.sqrMagnitude < 0.5f || secondary.sqrMagnitude < 0.5f
                || forward.sqrMagnitude < 0.5f)
            {
                return Quaternion.identity;
            }
            return Quaternion.LookRotation(forward, up);
        }
    }

    public static class PrimitiveHelper
    {
        static readonly Dictionary<PrimitiveType, Mesh> meshes =
            new Dictionary<PrimitiveType, Mesh>();

        public static Mesh GetPrimitiveMesh(PrimitiveType primitive)
        {
            Mesh mesh;
            if (meshes.TryGetValue(primitive, out mesh) && mesh) return mesh;

            GameObject temporary = GameObject.CreatePrimitive(primitive);
            temporary.hideFlags = HideFlags.HideAndDontSave;
            MeshFilter filter = temporary.GetComponent<MeshFilter>();
            mesh = filter ? filter.sharedMesh : null;
            if (Application.isPlaying) UnityEngine.Object.Destroy(temporary);
            else UnityEngine.Object.DestroyImmediate(temporary);
            if (!mesh)
            {
                throw new InvalidOperationException(
                    "Unity did not provide a mesh for " + primitive + ".");
            }
            meshes[primitive] = mesh;
            return mesh;
        }

        public static GameObject CreatePrimitiveGameObject(
            PrimitiveType primitive,
            bool includeCollider)
        {
            GameObject result = GameObject.CreatePrimitive(primitive);
            if (!includeCollider)
            {
                Collider collider = result.GetComponent<Collider>();
                if (collider)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
                    else UnityEngine.Object.DestroyImmediate(collider);
                }
            }
            return result;
        }
    }
}
