using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    [DisallowMultipleComponent]
    public sealed class RagdollContactRelay : MonoBehaviour
    {
        internal RagdollTelemetryRecorder Recorder { get; set; }
        void OnCollisionEnter(Collision collision) => Recorder?.RecordCollision(GetComponent<Collider>(), collision, true, false, false);
        void OnCollisionStay(Collision collision) => Recorder?.RecordCollision(GetComponent<Collider>(), collision, false, true, false);
        void OnCollisionExit(Collision collision) => Recorder?.RecordCollision(GetComponent<Collider>(), collision, false, false, true);
    }
}
