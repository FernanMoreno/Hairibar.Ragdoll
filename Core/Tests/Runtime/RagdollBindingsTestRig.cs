using System;
using System.Reflection;
using UnityEngine;

namespace Hairibar.Ragdoll.Tests
{
    public sealed class RagdollBindingsTestRig : IDisposable
    {
        public BoneName RootName { get; } = new BoneName("Root");
        public BoneName ChildName { get; } = new BoneName("Child");

        public RagdollDefinitionBindings Bindings { get; private set; }
        public RagdollDefinition Definition { get; private set; }
        public Rigidbody RootBody { get; private set; }
        public Rigidbody ChildBody { get; private set; }
        public ConfigurableJoint RootJoint { get; private set; }
        public ConfigurableJoint ChildJoint { get; private set; }
        public Collider RootCollider { get; private set; }
        public Collider ChildCollider { get; private set; }

        GameObject rootObject;
        GameObject childObject;

        public RagdollBindingsTestRig()
        {
            rootObject = new GameObject("Registry Test Root");
            rootObject.SetActive(false);
            childObject = new GameObject("Registry Test Child");
            childObject.transform.SetParent(rootObject.transform);

            RootBody = rootObject.AddComponent<Rigidbody>();
            RootJoint = rootObject.AddComponent<ConfigurableJoint>();
            RootCollider = rootObject.AddComponent<BoxCollider>();

            ChildBody = childObject.AddComponent<Rigidbody>();
            ChildJoint = childObject.AddComponent<ConfigurableJoint>();
            ChildJoint.connectedBody = RootBody;
            ChildCollider = childObject.AddComponent<CapsuleCollider>();

            Definition = ScriptableObject.CreateInstance<RagdollDefinition>();
            SetField(Definition, "_isValid", true);
            SetField(Definition, "_root", RootName);
            SetField(Definition, "bones", new[] { RootName, ChildName });

            Bindings = rootObject.AddComponent<RagdollDefinitionBindings>();
            SetField(Bindings, "_definition", Definition);
            SetField(Bindings, "bindings", CreateBindingsDictionary());

            if (Application.isPlaying)
            {
                rootObject.SetActive(true);
            }
            else
            {
                Rebuild();
            }

            if (!Bindings.IsInitialized)
            {
                throw new InvalidOperationException("The test ragdoll failed to initialize.");
            }
        }

        public Collider AddUnregisteredChildCollider()
        {
            return childObject.AddComponent<SphereCollider>();
        }

        public void ReverseDefinitionOrderAndRebuild()
        {
            SetField(Definition, "bones", new[] { ChildName, RootName });
            Rebuild();
        }

        public void Dispose()
        {
            if (Definition != null)
            {
                DestroyObject(Definition);
                Definition = null;
            }

            if (rootObject != null)
            {
                DestroyObject(rootObject);
                rootObject = null;
                childObject = null;
            }
        }

        object CreateBindingsDictionary()
        {
            Type dictionaryType = typeof(RagdollDefinitionBindings).GetNestedType(
                "BoneJointBindingsDictionary",
                BindingFlags.NonPublic);
            if (dictionaryType == null)
            {
                throw new MissingMemberException("BoneJointBindingsDictionary was not found.");
            }

            object dictionary = Activator.CreateInstance(dictionaryType, true);
            MethodInfo addMethod = dictionaryType.GetMethod(
                "Add",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(BoneName), typeof(ConfigurableJoint) },
                null);
            if (addMethod == null)
            {
                throw new MissingMethodException(dictionaryType.FullName, "Add");
            }

            // Reverse insertion proves that runtime indices follow RagdollDefinition.Bones.
            addMethod.Invoke(dictionary, new object[] { ChildName, ChildJoint });
            addMethod.Invoke(dictionary, new object[] { RootName, RootJoint });
            return dictionary;
        }

        void Rebuild()
        {
            MethodInfo onValidate = typeof(RagdollDefinitionBindings).GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (onValidate == null)
            {
                throw new MissingMethodException(typeof(RagdollDefinitionBindings).FullName, "OnValidate");
            }

            onValidate.Invoke(Bindings, null);
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }

        static void DestroyObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
