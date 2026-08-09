using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor
{
    [CustomEditor(typeof(RagdollAuthoredRig))]
    public sealed class RagdollAuthoredRigEditor : UnityEditor.Editor
    {
        enum EditMode { Colliders, Joints }

        EditMode mode;
        int selectedCollider;
        int selectedJoint;
        bool symmetry = true;
        float symmetryDistance = 0.25f;
        float massMultiplier = 1f;

        RagdollAuthoredRig Rig => (RagdollAuthoredRig)target;
        internal float SymmetryDistance
        {
            get => symmetryDistance;
            set => symmetryDistance = Mathf.Max(0f, value);
        }

        public override void OnInspectorGUI()
        {
            mode = (EditMode)GUILayout.Toolbar((int)mode, new[] { "Colliders", "Joints" });
            symmetry = EditorGUILayout.Toggle("Symmetry", symmetry);
            if (symmetry)
                symmetryDistance = Mathf.Max(0f, EditorGUILayout.FloatField(
                    "Symmetry Max Distance", symmetryDistance));
            EditorGUILayout.Space();

            DrawGlobalBodyTools();
            DrawHierarchyTools();
            DrawDiagnostics();
            if (mode == EditMode.Colliders) DrawColliderTools();
            else DrawJointTools();

            EditorGUILayout.Space();
            if (GUILayout.Button("Remove Authored Ragdoll")) RemoveRigWithUndo();
        }

        void DrawHierarchyTools()
        {
            EditorGUILayout.LabelField("Hierarchy", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(Rig.IsFlatHierarchy))
                {
                    if (GUILayout.Button("Flat"))
                    {
                        Undo.RegisterFullObjectHierarchyUndo(
                            Rig.transform.root.gameObject,
                            "Flatten ragdoll hierarchy");
                        Rig.SetFlatHierarchy(
                            Rig.transform.parent ? Rig.transform.parent : Rig.transform);
                    }
                }
                using (new EditorGUI.DisabledScope(!Rig.IsFlatHierarchy))
                {
                    if (GUILayout.Button("Tree"))
                    {
                        Undo.RegisterFullObjectHierarchyUndo(
                            Rig.transform.root.gameObject,
                            "Restore ragdoll hierarchy");
                        Rig.SetTreeHierarchy();
                    }
                }
            }
        }

        void DrawGlobalBodyTools()
        {
            EditorGUILayout.LabelField("Rigidbodies", EditorStyles.boldLabel);
            massMultiplier = EditorGUILayout.FloatField("Mass Multiplier", massMultiplier);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Mass"))
                {
                    Rigidbody[] bodies = Rig.Rigidbodies;
                    Undo.RecordObjects(bodies, "Multiply ragdoll mass");
                    for (int index = 0; index < bodies.Length; index++)
                    {
                        if (bodies[index]) bodies[index].mass *= Mathf.Max(0.001f, massMultiplier);
                    }
                }
                if (GUILayout.Button("Kinematic")) SetKinematic(true);
                if (GUILayout.Button("Dynamic")) SetKinematic(false);
            }
        }

        void DrawColliderTools()
        {
            Collider[] colliders = Rig.Colliders;
            selectedCollider = Mathf.Clamp(selectedCollider, 0, Mathf.Max(0, colliders.Length - 1));
            selectedCollider = EditorGUILayout.Popup(
                "Selected",
                selectedCollider,
                ComponentNames(colliders));
            if (colliders.Length == 0 || !colliders[selectedCollider]) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select GameObject"))
                    Selection.activeGameObject = colliders[selectedCollider].gameObject;
                if (GUILayout.Button("To Box"))
                    ConvertSelectedCollider(typeof(BoxCollider));
                if (GUILayout.Button("To Capsule"))
                    ConvertSelectedCollider(typeof(CapsuleCollider));
                if (GUILayout.Button("To Sphere"))
                    ConvertSelectedCollider(typeof(SphereCollider));
                if (GUILayout.Button("Rotate 90°")) RotateCollider();
            }
        }

        void DrawJointTools()
        {
            ConfigurableJoint[] joints = Rig.Joints;
            selectedJoint = Mathf.Clamp(selectedJoint, 0, Mathf.Max(0, joints.Length - 1));
            selectedJoint = EditorGUILayout.Popup("Selected", selectedJoint, ComponentNames(joints));
            if (joints.Length == 0 || !joints[selectedJoint]) return;

            ConfigurableJoint joint = joints[selectedJoint];
            Rigidbody connected = (Rigidbody)EditorGUILayout.ObjectField(
                "Connected Body", joint.connectedBody, typeof(Rigidbody), true);
            if (connected != joint.connectedBody)
            {
                string error;
                if (!TrySetSelectedConnectedBody(connected, out error))
                    UnityEngine.Debug.LogError(error, joint);
            }

            DrawJointLimitsAndProjection(joint);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Yellow / Green")) SwapAxes(joint, 0);
                if (GUILayout.Button("Yellow / Blue")) SwapAxes(joint, 1);
                if (GUILayout.Button("Green / Blue")) SwapAxes(joint, 2);
                if (GUILayout.Button("Invert Yellow"))
                {
                    Undo.RecordObject(joint, "Invert joint axis");
                    joint.axis = -joint.axis;
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Disable Preprocessing")) SetPreprocessing(false);
                if (GUILayout.Button("Enable Preprocessing")) SetPreprocessing(true);
                using (new EditorGUI.DisabledScope(!symmetry))
                {
                    if (GUILayout.Button("Mirror Joint")) MirrorSelectedJoint();
                }
            }
        }

        void DrawJointLimitsAndProjection(ConfigurableJoint joint)
        {
            JointProjectionMode projection = (JointProjectionMode)
                EditorGUILayout.EnumPopup("Projection", joint.projectionMode);
            bool preprocessing = EditorGUILayout.Toggle(
                "Enable Preprocessing",
                joint.enablePreprocessing);
            float lowX = EditorGUILayout.Slider(
                "Low Angular X",
                joint.lowAngularXLimit.limit,
                -177f,
                0f);
            float highX = EditorGUILayout.Slider(
                "High Angular X",
                joint.highAngularXLimit.limit,
                0f,
                177f);
            float y = EditorGUILayout.Slider(
                "Angular Y",
                joint.angularYLimit.limit,
                0f,
                177f);
            float z = EditorGUILayout.Slider(
                "Angular Z",
                joint.angularZLimit.limit,
                0f,
                177f);
            if (projection == joint.projectionMode
                && preprocessing == joint.enablePreprocessing
                && Mathf.Approximately(lowX, joint.lowAngularXLimit.limit)
                && Mathf.Approximately(highX, joint.highAngularXLimit.limit)
                && Mathf.Approximately(y, joint.angularYLimit.limit)
                && Mathf.Approximately(z, joint.angularZLimit.limit))
            {
                return;
            }

            Undo.RecordObject(joint, "Edit ragdoll joint limits");
            joint.projectionMode = projection;
            joint.enablePreprocessing = preprocessing;
            SoftJointLimit limit = joint.lowAngularXLimit;
            limit.limit = lowX;
            joint.lowAngularXLimit = limit;
            limit = joint.highAngularXLimit;
            limit.limit = highX;
            joint.highAngularXLimit = limit;
            limit = joint.angularYLimit;
            limit.limit = y;
            joint.angularYLimit = limit;
            limit = joint.angularZLimit;
            limit.limit = z;
            joint.angularZLimit = limit;
            EditorUtility.SetDirty(joint);
            if (symmetry) MirrorSelectedJoint();
        }

        void DrawDiagnostics()
        {
            if (!GUILayout.Button("Diagnose Authored Rig")) return;
            string[] issues = Rig.GetDiagnostics();
            if (issues.Length == 0)
            {
                UnityEngine.Debug.Log("No authored-rig ownership or topology issues found.", Rig);
                return;
            }
            UnityEngine.Debug.LogWarning(string.Join("\n", issues), Rig);
        }

        void OnSceneGUI()
        {
            if (mode == EditMode.Colliders) DrawColliderSceneGUI();
            else DrawJointSceneGUI();
        }

        void DrawColliderSceneGUI()
        {
            Collider[] colliders = Rig.Colliders;
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (!collider) continue;
                Vector3 center = WorldCenter(collider);
                float size = HandleUtility.GetHandleSize(center) * 0.08f;
                Handles.color = index == selectedCollider ? Color.yellow : Color.green;
                if (Handles.Button(center, Quaternion.identity, size, size, Handles.SphereHandleCap))
                {
                    selectedCollider = index;
                    Repaint();
                }
            }
            if (selectedCollider < 0 || selectedCollider >= colliders.Length) return;
            Collider selected = colliders[selectedCollider];
            if (!selected) return;

            Vector3 oldCenter = WorldCenter(selected);
            EditorGUI.BeginChangeCheck();
            Vector3 newCenter = Handles.PositionHandle(oldCenter, selected.transform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selected, "Move collider center");
                SetCenter(selected, selected.transform.InverseTransformPoint(newCenter));
                if (symmetry) MirrorCollider(selected);
            }

            DrawColliderScaleHandle(selected);
        }

        void DrawColliderScaleHandle(Collider collider)
        {
            Vector3 scale = ColliderSize(collider);
            EditorGUI.BeginChangeCheck();
            Vector3 changed = Handles.ScaleHandle(
                scale,
                WorldCenter(collider),
                collider.transform.rotation,
                HandleUtility.GetHandleSize(WorldCenter(collider)));
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(collider, "Resize collider");
            SetColliderSize(collider, changed);
            if (symmetry) MirrorCollider(collider);
        }

        void DrawJointSceneGUI()
        {
            ConfigurableJoint[] joints = Rig.Joints;
            for (int index = 0; index < joints.Length; index++)
            {
                ConfigurableJoint joint = joints[index];
                if (!joint) continue;
                float size = HandleUtility.GetHandleSize(joint.transform.position) * 0.08f;
                Handles.color = index == selectedJoint ? Color.yellow : Color.cyan;
                if (Handles.Button(joint.transform.position, Quaternion.identity,
                    size, size, Handles.CubeHandleCap))
                {
                    selectedJoint = index;
                    Repaint();
                }
            }
            if (selectedJoint < 0 || selectedJoint >= joints.Length) return;
            ConfigurableJoint selected = joints[selectedJoint];
            if (!selected) return;

            Quaternion local = Quaternion.LookRotation(
                Vector3.Cross(selected.axis, selected.secondaryAxis),
                selected.secondaryAxis);
            Quaternion world = selected.transform.rotation * local;
            EditorGUI.BeginChangeCheck();
            Quaternion changed = Handles.RotationHandle(world, selected.transform.position);
            if (!EditorGUI.EndChangeCheck()) return;

            Quaternion changedLocal = Quaternion.Inverse(selected.transform.rotation) * changed;
            string error;
            if (!TrySetSelectedJointAxes(
                changedLocal * Vector3.right,
                changedLocal * Vector3.up,
                out error))
            {
                UnityEngine.Debug.LogError(error, selected);
            }
        }

        /// <summary>
        /// Validates and applies the two local vectors that define a
        /// ConfigurableJoint frame. Validation happens before Undo records or
        /// authored components are changed, so rejected editor input cannot
        /// partially mutate the rig.
        /// </summary>
        internal bool TrySetSelectedJointAxes(
            Vector3 axis,
            Vector3 secondaryAxis,
            out string error)
        {
            ConfigurableJoint[] joints = Rig.Joints;
            if (selectedJoint < 0 || selectedJoint >= joints.Length
                || !joints[selectedJoint])
            {
                error = "No authored ConfigurableJoint is selected.";
                return false;
            }
            Vector3 normalizedAxis;
            Vector3 normalizedSecondary;
            if (!TryNormalizeJointAxes(axis, secondaryAxis,
                out normalizedAxis, out normalizedSecondary, out error))
                return false;

            ConfigurableJoint selected = joints[selectedJoint];
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rotate joint axes");
            try
            {
                Undo.RecordObject(selected, "Rotate joint axes");
                selected.axis = normalizedAxis;
                selected.secondaryAxis = normalizedSecondary;
                EditorUtility.SetDirty(selected);
                if (symmetry) MirrorSelectedJoint();
                Undo.CollapseUndoOperations(group);
                error = string.Empty;
                return true;
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                error = "Joint axes were not changed: " + exception.Message;
                return false;
            }
        }

        internal bool TrySetSelectedConnectedBody(
            Rigidbody connectedBody,
            out string error)
        {
            ConfigurableJoint[] joints = Rig.Joints;
            if (selectedJoint < 0 || selectedJoint >= joints.Length
                || !joints[selectedJoint])
            {
                error = "No authored ConfigurableJoint is selected.";
                return false;
            }
            ConfigurableJoint joint = joints[selectedJoint];
            Rigidbody ownBody = joint.GetComponent<Rigidbody>();
            if (connectedBody == ownBody)
            {
                error = "A ConfigurableJoint cannot connect to its own Rigidbody.";
                return false;
            }
            if (connectedBody && !System.Array.Exists(
                Rig.Rigidbodies, body => body == connectedBody))
            {
                error = "The connectedBody must belong to the authored ragdoll.";
                return false;
            }

            Undo.RecordObject(joint, "Change connected body");
            joint.connectedBody = connectedBody;
            EditorUtility.SetDirty(joint);
            error = string.Empty;
            return true;
        }

        internal bool TryApplySymmetricEdit(
            Vector3 center,
            Vector3 size,
            Vector3 axis,
            Vector3 secondaryAxis,
            float lowAngularX,
            float highAngularX,
            float angularY,
            float angularZ,
            out string error)
        {
            Collider[] colliders = Rig.Colliders;
            ConfigurableJoint[] joints = Rig.Joints;
            if (selectedCollider < 0 || selectedCollider >= colliders.Length
                || !colliders[selectedCollider]
                || selectedJoint < 0 || selectedJoint >= joints.Length
                || !joints[selectedJoint])
            {
                error = "An authored collider and joint must be selected.";
                return false;
            }
            Collider sourceCollider = colliders[selectedCollider];
            ConfigurableJoint sourceJoint = joints[selectedJoint];
            Collider mirrorCollider = FindMirror(sourceCollider, colliders);
            ConfigurableJoint mirrorJoint = FindMirror(sourceJoint, joints);
            if (!mirrorCollider || mirrorCollider.GetType() != sourceCollider.GetType()
                || !mirrorJoint)
            {
                error = "A compatible symmetric collider and joint are required.";
                return false;
            }
            if (!IsFinite(center) || !IsFinite(size)
                || size.x <= 0f || size.y <= 0f || size.z <= 0f)
            {
                error = "Collider center and strictly positive size must be finite.";
                return false;
            }
            Vector3 normalizedAxis;
            Vector3 normalizedSecondary;
            if (!TryNormalizeJointAxes(axis, secondaryAxis,
                out normalizedAxis, out normalizedSecondary, out error))
                return false;
            if (!IsFinite(lowAngularX) || !IsFinite(highAngularX)
                || !IsFinite(angularY) || !IsFinite(angularZ)
                || lowAngularX > 0f || highAngularX < 0f
                || angularY < 0f || angularZ < 0f)
            {
                error = "Joint limits are outside the supported angular ranges.";
                return false;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Edit symmetric authored ragdoll");
            try
            {
                Undo.RecordObjects(new UnityEngine.Object[]
                {
                    sourceCollider, mirrorCollider, sourceJoint, mirrorJoint
                }, "Edit symmetric authored ragdoll");
                SetCenter(sourceCollider, center);
                SetColliderSize(sourceCollider, size);
                sourceJoint.axis = normalizedAxis;
                sourceJoint.secondaryAxis = normalizedSecondary;
                SetJointLimits(sourceJoint, lowAngularX, highAngularX,
                    angularY, angularZ);
                MirrorCollider(sourceCollider);
                MirrorSelectedJoint();
                EditorUtility.SetDirty(sourceCollider);
                EditorUtility.SetDirty(sourceJoint);
                Undo.CollapseUndoOperations(group);
                error = string.Empty;
                return true;
            }
            catch (System.Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                error = "Symmetric edit was not applied: " + exception.Message;
                return false;
            }
        }

        void SetKinematic(bool value)
        {
            Rigidbody[] bodies = Rig.Rigidbodies;
            Undo.RecordObjects(bodies, "Set ragdoll kinematic state");
            for (int index = 0; index < bodies.Length; index++)
                if (bodies[index]) bodies[index].isKinematic = value;
        }

        void SetPreprocessing(bool value)
        {
            ConfigurableJoint[] joints = Rig.Joints;
            Undo.RecordObjects(joints, "Set joint preprocessing");
            for (int index = 0; index < joints.Length; index++)
                if (joints[index]) joints[index].enablePreprocessing = value;
        }

        void SwapAxes(ConfigurableJoint joint, int operation)
        {
            Undo.RecordObject(joint, "Swap joint axes");
            Vector3 yellow = joint.axis;
            Vector3 green = joint.secondaryAxis;
            Vector3 blue = Vector3.Cross(yellow, green).normalized;
            if (operation == 0) { joint.axis = green; joint.secondaryAxis = yellow; }
            else if (operation == 1) { joint.axis = blue; joint.secondaryAxis = green; }
            else { joint.secondaryAxis = blue; }
        }

        internal bool ConvertSelectedCollider(System.Type colliderType)
        {
            if (colliderType != typeof(BoxCollider)
                && colliderType != typeof(CapsuleCollider)
                && colliderType != typeof(SphereCollider))
            {
                throw new System.ArgumentException(
                    "Only BoxCollider, CapsuleCollider and SphereCollider are supported.",
                    nameof(colliderType));
            }
            Collider old = Rig.Colliders[selectedCollider];
            if (!old) return false;
            if (old.GetType() == colliderType) return false;
            Vector3 center = Center(old);
            Vector3 size = ColliderSize(old);
            GameObject owner = old.gameObject;
            bool wasEnabled = old.enabled;
            bool wasTrigger = old.isTrigger;
            PhysicsMaterial material = old.sharedMaterial;
            float contactOffset = old.contactOffset;
            bool providesContacts = old.providesContacts;
            int layerOverridePriority = old.layerOverridePriority;
            LayerMask includeLayers = old.includeLayers;
            LayerMask excludeLayers = old.excludeLayers;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Convert authored collider");
            try
            {
                Undo.DestroyObjectImmediate(old);
                Collider replacement;
                if (colliderType == typeof(BoxCollider))
                {
                    BoxCollider value = Undo.AddComponent<BoxCollider>(owner);
                    value.center = center;
                    value.size = size;
                    replacement = value;
                }
                else if (colliderType == typeof(CapsuleCollider))
                {
                    CapsuleCollider value = Undo.AddComponent<CapsuleCollider>(owner);
                    value.center = center;
                    int axis = DominantAxis(size);
                    value.direction = axis;
                    value.radius = Mathf.Max(0.0001f,
                        Mathf.Max(size[(axis + 1) % 3],
                            size[(axis + 2) % 3]) * 0.5f);
                    value.height = Mathf.Max(size[axis], value.radius * 2f);
                    replacement = value;
                }
                else
                {
                    SphereCollider value = Undo.AddComponent<SphereCollider>(owner);
                    value.center = center;
                    value.radius = Mathf.Max(
                        size.x,
                        Mathf.Max(size.y, size.z)) * 0.5f;
                    replacement = value;
                }

                replacement.enabled = wasEnabled;
                replacement.isTrigger = wasTrigger;
                replacement.sharedMaterial = material;
                replacement.contactOffset = contactOffset;
                replacement.providesContacts = providesContacts;
                replacement.layerOverridePriority = layerOverridePriority;
                replacement.includeLayers = includeLayers;
                replacement.excludeLayers = excludeLayers;
                Undo.RecordObject(Rig, "Replace authored collider");
                Rig.ReplaceCollider(selectedCollider, replacement);
                Undo.CollapseUndoOperations(group);
                return true;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        void RotateCollider()
        {
            Collider collider = Rig.Colliders[selectedCollider];
            Undo.RecordObject(collider, "Rotate collider geometry");
            BoxCollider box = collider as BoxCollider;
            if (box)
            {
                box.size = new Vector3(box.size.z, box.size.x, box.size.y);
                box.center = new Vector3(box.center.z, box.center.x, box.center.y);
            }
            CapsuleCollider capsule = collider as CapsuleCollider;
            if (capsule) capsule.direction = (capsule.direction + 1) % 3;
        }

        void MirrorCollider(Collider source)
        {
            Collider mirror = FindMirror(source, Rig.Colliders);
            if (!mirror || mirror.GetType() != source.GetType()) return;
            Undo.RecordObject(mirror, "Mirror collider");
            Vector3 rootLocal = Rig.transform.InverseTransformPoint(WorldCenter(source));
            rootLocal.x = -rootLocal.x;
            SetCenter(mirror, mirror.transform.InverseTransformPoint(
                Rig.transform.TransformPoint(rootLocal)));
            SetColliderSize(mirror, ColliderSize(source));
        }

        internal void MirrorSelectedJoint()
        {
            ConfigurableJoint source = Rig.Joints[selectedJoint];
            ConfigurableJoint mirror = FindMirror(source, Rig.Joints);
            if (!mirror) return;
            Undo.RecordObject(mirror, "Mirror joint");
            mirror.lowAngularXLimit = source.lowAngularXLimit;
            mirror.highAngularXLimit = source.highAngularXLimit;
            mirror.angularYLimit = source.angularYLimit;
            mirror.angularZLimit = source.angularZLimit;
            Vector3 worldAxis = source.transform.TransformDirection(source.axis);
            Vector3 worldSecondary = source.transform.TransformDirection(source.secondaryAxis);
            mirror.axis = mirror.transform.InverseTransformDirection(ReflectDirection(worldAxis));
            mirror.secondaryAxis = mirror.transform.InverseTransformDirection(
                ReflectDirection(worldSecondary));
        }

        T FindMirror<T>(T source, T[] values) where T : Component
        {
            Vector3 local = Rig.transform.InverseTransformPoint(source.transform.position);
            local.x = -local.x;
            Vector3 expected = Rig.transform.TransformPoint(local);
            T best = null;
            float distance = float.PositiveInfinity;
            for (int index = 0; index < values.Length; index++)
            {
                T candidate = values[index];
                if (!candidate || candidate == source) continue;
                float candidateDistance = (candidate.transform.position - expected).sqrMagnitude;
                if (candidateDistance < distance)
                {
                    distance = candidateDistance;
                    best = candidate;
                }
            }
            return best && distance <= symmetryDistance * symmetryDistance
                ? best
                : null;
        }

        Vector3 ReflectDirection(Vector3 worldDirection)
        {
            Vector3 local = Rig.transform.InverseTransformDirection(worldDirection);
            local.x = -local.x;
            return Rig.transform.TransformDirection(local);
        }

        void RemoveRigWithUndo()
        {
            foreach (ConfigurableJoint joint in Rig.Joints)
                if (joint) Undo.DestroyObjectImmediate(joint);
            foreach (Collider collider in Rig.Colliders)
                if (collider) Undo.DestroyObjectImmediate(collider);
            foreach (Rigidbody body in Rig.Rigidbodies)
                if (body) Undo.DestroyObjectImmediate(body);
            Undo.DestroyObjectImmediate(Rig);
        }

        static string[] ComponentNames<T>(T[] values) where T : Component
        {
            string[] names = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
                names[index] = values[index] ? values[index].name : "Missing";
            return names;
        }

        static Vector3 Center(Collider value)
        {
            BoxCollider box = value as BoxCollider;
            if (box) return box.center;
            CapsuleCollider capsule = value as CapsuleCollider;
            if (capsule) return capsule.center;
            SphereCollider sphere = value as SphereCollider;
            return sphere ? sphere.center : Vector3.zero;
        }

        static void SetCenter(Collider value, Vector3 center)
        {
            BoxCollider box = value as BoxCollider;
            if (box) box.center = center;
            CapsuleCollider capsule = value as CapsuleCollider;
            if (capsule) capsule.center = center;
            SphereCollider sphere = value as SphereCollider;
            if (sphere) sphere.center = center;
        }

        static Vector3 WorldCenter(Collider value)
        {
            return value.transform.TransformPoint(Center(value));
        }

        static Vector3 ColliderSize(Collider value)
        {
            BoxCollider box = value as BoxCollider;
            if (box) return box.size;
            SphereCollider sphere = value as SphereCollider;
            if (sphere) return Vector3.one * sphere.radius * 2f;
            CapsuleCollider capsule = value as CapsuleCollider;
            if (capsule)
            {
                Vector3 size = Vector3.one * capsule.radius * 2f;
                size[capsule.direction] = capsule.height;
                return size;
            }
            return Vector3.one;
        }

        static void SetColliderSize(Collider value, Vector3 size)
        {
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            BoxCollider box = value as BoxCollider;
            if (box) box.size = size;
            SphereCollider sphere = value as SphereCollider;
            if (sphere) sphere.radius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f;
            CapsuleCollider capsule = value as CapsuleCollider;
            if (capsule)
            {
                capsule.height = size[capsule.direction];
                capsule.radius = Mathf.Max(0.0001f,
                    Mathf.Max(size[(capsule.direction + 1) % 3],
                        size[(capsule.direction + 2) % 3]) * 0.5f);
            }
        }

        static int DominantAxis(Vector3 value)
        {
            if (value.x >= value.y && value.x >= value.z) return 0;
            return value.y >= value.z ? 1 : 2;
        }

        static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static bool TryNormalizeJointAxes(
            Vector3 axis,
            Vector3 secondaryAxis,
            out Vector3 normalizedAxis,
            out Vector3 normalizedSecondary,
            out string error)
        {
            normalizedAxis = Vector3.right;
            normalizedSecondary = Vector3.up;
            if (!IsFinite(axis) || !IsFinite(secondaryAxis))
            {
                error = "Joint axes must contain finite values.";
                return false;
            }
            const float minimumSqrMagnitude = 0.00000001f;
            if (axis.sqrMagnitude <= minimumSqrMagnitude
                || secondaryAxis.sqrMagnitude <= minimumSqrMagnitude)
            {
                error = "Joint axes must be non-zero vectors.";
                return false;
            }

            normalizedAxis = axis.normalized;
            Vector3 orthogonalSecondary = secondaryAxis
                - Vector3.Project(secondaryAxis, normalizedAxis);
            if (orthogonalSecondary.sqrMagnitude <= minimumSqrMagnitude)
            {
                error = "Joint axis and secondary axis must not be parallel.";
                return false;
            }
            normalizedSecondary = orthogonalSecondary.normalized;
            error = string.Empty;
            return true;
        }

        static void SetJointLimits(
            ConfigurableJoint joint,
            float lowAngularX,
            float highAngularX,
            float angularY,
            float angularZ)
        {
            SoftJointLimit limit = joint.lowAngularXLimit;
            limit.limit = lowAngularX;
            joint.lowAngularXLimit = limit;
            limit = joint.highAngularXLimit;
            limit.limit = highAngularX;
            joint.highAngularXLimit = limit;
            limit = joint.angularYLimit;
            limit.limit = angularY;
            joint.angularYLimit = limit;
            limit = joint.angularZLimit;
            limit.limit = angularZ;
            joint.angularZLimit = limit;
        }
    }
}
