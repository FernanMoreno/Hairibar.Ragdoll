using Hairibar.Ragdoll.Animation;
using UnityEngine;
using UnityEngine.UI;

namespace Hairibar.Ragdoll.Demo.Hanging
{
#pragma warning disable 649
    public class RotationAlphaSlider : MonoBehaviour
    {
        [SerializeField] Text label;

        RagdollAnimator animator;
        UnityEngine.UI.Slider slider;

        void Start()
        {
            slider = GetComponent<UnityEngine.UI.Slider>();
            animator = FindAnyObjectByType<RagdollAnimator>();
        }

        void Update()
        {
            float newValue = slider.value;
            animator.MasterMuscleWeight = newValue;
            label.text = string.Format("{0:0.00}", newValue);
        }

    }

}
