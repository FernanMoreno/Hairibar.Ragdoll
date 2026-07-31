using System;
using System.Reflection;
using Hairibar.NaughtyExtensions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public static class NaughtyEditorGUI
    {
        public static void HelpBox_Layout(
            string message,
            MessageType type,
            bool logToConsole = false)
        {
            EditorGUILayout.HelpBox(message, type);
            if (logToConsole && type == MessageType.Error)
                UnityEngine.Debug.LogError(message);
        }

        public static void BeginBoxGroup_Layout(string label)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        public static void EndBoxGroup_Layout()
        {
            EditorGUILayout.EndVertical();
        }
    }
}

namespace Hairibar.EngineExtensions.Editor
{
    public static class EditorSerializationUtility
    {
        public static void Deserialize(string directory, object value, UnityEngine.Object target)
        {
            string json = EditorPrefs.GetString(Key(directory, target), string.Empty);
            if (!string.IsNullOrEmpty(json)) EditorJsonUtility.FromJsonOverwrite(json, value);
        }

        public static void Serialize(string directory, object value, UnityEngine.Object target)
        {
            EditorPrefs.SetString(Key(directory, target), EditorJsonUtility.ToJson(value));
        }

        static string Key(string directory, UnityEngine.Object target)
        {
            return directory + ":" + Hairibar.Ragdoll.RagdollUnityObjectId.Get(target);
        }
    }

    public static class ReflectionUtility
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;

        public static FieldInfo GetField(object target, string name)
        {
            return FindInHierarchy<FieldInfo>(target, type => type.GetField(name, Flags));
        }

        public static PropertyInfo GetProperty(object target, string name)
        {
            return FindInHierarchy<PropertyInfo>(target, type => type.GetProperty(name, Flags));
        }

        public static MethodInfo GetMethod(object target, string name)
        {
            return FindInHierarchy<MethodInfo>(target, type => type.GetMethod(name, Flags));
        }

        static T FindInHierarchy<T>(object target, Func<Type, T> find) where T : MemberInfo
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                T member = find(type);
                if (member != null) return member;
            }
            throw new MissingMemberException(target.GetType().FullName, typeof(T).Name);
        }
    }

    public static class SceneDragAndDrop
    {
        public static UnityEngine.Object GetAssignTarget<T>(Event current)
            where T : UnityEngine.Object
        {
            if (current == null || (current.type != EventType.DragUpdated
                && current.type != EventType.DragPerform)) return null;

            foreach (UnityEngine.Object dragged in DragAndDrop.objectReferences)
            {
                T candidate = dragged as T;
                GameObject gameObject = dragged as GameObject;
                if (!candidate && gameObject)
                    candidate = gameObject.GetComponent(typeof(T)) as T;
                Component component = dragged as Component;
                if (!candidate && component)
                    candidate = component.GetComponent(typeof(T)) as T;
                if (!candidate) continue;

                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                if (current.type == EventType.DragPerform) DragAndDrop.AcceptDrag();
                current.Use();
                return candidate;
            }
            return null;
        }
    }
}

namespace Hairibar.NaughtyExtensions.Editor
{
    public readonly struct NonLinearSliderFunction
    {
        public readonly Func<float, float> Forward;
        public readonly Func<float, float> Inverse;

        public NonLinearSliderFunction(
            Func<float, float> forward,
            Func<float, float> inverse)
        {
            Forward = forward;
            Inverse = inverse;
        }
    }

    public static class QuadraticSliderDrawer
    {
        public static NonLinearSliderFunction GetQuadraticFunction(float exponent)
        {
            exponent = Mathf.Max(0.0001f, exponent);
            float inverseExponent = 1f / exponent;
            return new NonLinearSliderFunction(
                normalized => Mathf.Pow(Mathf.Clamp01(normalized), exponent),
                normalized => Mathf.Pow(Mathf.Clamp01(normalized), inverseExponent));
        }
    }

    public static class NonLinearSliderDrawer
    {
        public static void Draw_Layout(
            SerializedProperty property,
            float minimum,
            float maximum,
            NonLinearSliderFunction function,
            GUIContent label)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            Draw(rect, property, minimum, maximum, function, label);
        }

        public static void Draw(
            Rect rect,
            SerializedProperty property,
            float minimum,
            float maximum,
            NonLinearSliderFunction function)
        {
            Draw(rect, property, minimum, maximum, function,
                new GUIContent(property.displayName, property.tooltip));
        }

        static void Draw(
            Rect rect,
            SerializedProperty property,
            float minimum,
            float maximum,
            NonLinearSliderFunction function,
            GUIContent label)
        {
            float normalized = Mathf.InverseLerp(minimum, maximum, property.floatValue);
            float sliderValue = function.Inverse != null ? function.Inverse(normalized) : normalized;
            EditorGUI.BeginChangeCheck();
            sliderValue = EditorGUI.Slider(rect, label, sliderValue, 0f, 1f);
            if (!EditorGUI.EndChangeCheck()) return;
            normalized = function.Forward != null ? function.Forward(sliderValue) : sliderValue;
            property.floatValue = Mathf.Lerp(minimum, maximum, normalized);
        }
    }

    public static class ExtraNaughtyEditorGUILayout
    {
        public static void Header(string label)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }
    }

    public static class ClampedFloatDrawer
    {
        public static void Draw_Layout(SerializedProperty property, float minimum, float maximum)
        {
            Draw_Layout(property, new GUIContent(property.displayName, property.tooltip), minimum, maximum);
        }

        public static void Draw_Layout(
            SerializedProperty property,
            GUIContent label,
            float minimum,
            float maximum)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.FloatField(label, property.floatValue);
            if (EditorGUI.EndChangeCheck()) property.floatValue = Mathf.Clamp(value, minimum, maximum);
        }
    }

    public static class ReorderableListUtility
    {
        public static ReorderableList Create(
            SerializedProperty property,
            bool draggable,
            bool displayHeader,
            bool displayAddButton,
            bool displayRemoveButton,
            string header)
        {
            ReorderableList list = new ReorderableList(
                property.serializedObject,
                property,
                draggable,
                displayHeader,
                displayAddButton,
                displayRemoveButton);
            if (displayHeader)
                list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, header);
            return list;
        }
    }

    public static class ReorderableListExtensions
    {
        public static void AddDefaultValueSetter(
            this ReorderableList list,
            Action<SerializedProperty> initialize)
        {
            ReorderableList.AddCallbackDelegate previous = list.onAddCallback;
            list.onAddCallback = current =>
            {
                int oldCount = current.serializedProperty.arraySize;
                if (previous != null) previous(current);
                else ReorderableList.defaultBehaviours.DoAddButton(current);
                if (initialize != null && current.serializedProperty.arraySize > oldCount)
                {
                    initialize(current.serializedProperty.GetArrayElementAtIndex(
                        current.serializedProperty.arraySize - 1));
                }
            };
        }
    }

    [CustomPropertyDrawer(typeof(UsePropertySetterAttribute))]
    public sealed class UsePropertySetterPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, property, label, true);
            if (!EditorGUI.EndChangeCheck()) return;
            property.serializedObject.ApplyModifiedProperties();
            InvokeSetter(
                property.serializedObject,
                fieldInfo,
                (UsePropertySetterAttribute)attribute);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        internal static void InvokeSetter(
            SerializedObject serialized,
            FieldInfo field,
            UsePropertySetterAttribute marker)
        {
            if (field == null) return;
            string propertyName = marker != null && !marker.AutoFindProperty
                ? marker.PropertyName
                : InferPropertyName(field.Name);
            foreach (UnityEngine.Object target in serialized.targetObjects)
            {
                PropertyInfo property = target.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null || !property.CanWrite) continue;
                property.SetValue(target, field.GetValue(target), null);
                EditorUtility.SetDirty(target);
            }
        }

        static string InferPropertyName(string fieldName)
        {
            string value = fieldName.TrimStart('_');
            return string.IsNullOrEmpty(value)
                ? fieldName
                : char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }

    public static class UsePropertySetterDrawer
    {
        public static void Draw_Layout(SerializedProperty property)
        {
            SerializedObject serialized = property.serializedObject;
            Type type = serialized.targetObject.GetType();
            FieldInfo field = type.GetField(
                property.name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            UsePropertySetterAttribute marker = field != null
                ? field.GetCustomAttribute<UsePropertySetterAttribute>()
                : null;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, true);
            if (!EditorGUI.EndChangeCheck()) return;
            serialized.ApplyModifiedProperties();
            UsePropertySetterPropertyDrawer.InvokeSetter(serialized, field, marker);
        }
    }

    public static class SerializedPropertyLabelExtensions
    {
        public static GUIContent GetLabelContent(this SerializedProperty property)
        {
            return new GUIContent(property.displayName, property.tooltip);
        }
    }
}
