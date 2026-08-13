using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class RagdollBipedStaggerAnimatorLayerEditorTests
    {
        const string Folder = "Assets/__HairibarStaggerAnimatorLayerTests";
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "__HairibarStaggerAnimatorLayerTests");
            root = new GameObject("Stagger Animator Layer Test");
        }

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void DefaultAnimatorLayer_ResolvesStateFromNonBaseLayer()
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(
                Folder + "/Stagger.controller");
            controller.AddLayer("StepLayer");
            controller.layers[1].stateMachine.AddState("Idle");
            controller.layers[1].stateMachine.AddState("Forward");
            AssetDatabase.SaveAssets();

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            RagdollBipedStaggerBehaviour stagger =
                root.AddComponent<RagdollBipedStaggerBehaviour>();

            int layer = stagger.ResolveStepStateLayer(
                animator, Animator.StringToHash("StepLayer.Forward"));

            Assert.That(layer, Is.EqualTo(1));

            int stateHash = Animator.StringToHash("StepLayer.Forward");
            RagdollBipedStaggerBehaviour.CrossFadeStepState(
                animator, stateHash, 0f, layer);
            animator.Update(0f);

            Assert.That(animator.GetCurrentAnimatorStateInfo(layer).fullPathHash,
                Is.EqualTo(stateHash));
        }

        [Test]
        public void SwingFootParameter_MustExistAndBeInteger()
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(
                Folder + "/Parameters.controller");
            controller.AddParameter("StepSwingFoot", AnimatorControllerParameterType.Int);
            controller.AddParameter("WrongType", AnimatorControllerParameterType.Bool);
            AssetDatabase.SaveAssets();
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            Assert.That(RagdollBipedStaggerBehaviour.HasIntegerParameter(
                animator, "StepSwingFoot"), Is.True);
            Assert.That(RagdollBipedStaggerBehaviour.HasIntegerParameter(
                animator, "WrongType"), Is.False);
            Assert.That(RagdollBipedStaggerBehaviour.HasIntegerParameter(
                animator, "Missing"), Is.False);
        }
    }
}
