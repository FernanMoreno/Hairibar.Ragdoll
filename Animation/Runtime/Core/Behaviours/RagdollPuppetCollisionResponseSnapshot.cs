namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Immutable diagnostics for the latest accepted collision response.</summary>
    public struct RagdollPuppetCollisionResponseSnapshot
    {
        public bool HasResponse { get; private set; }
        public RagdollBoneHandle Bone { get; private set; }
        public float FixedTime { get; private set; }
        public float ImpulseMagnitude { get; private set; }
        public float DamageImpulseMagnitude { get; private set; }
        public float SourceImpulseMultiplier { get; private set; }
        public float ReceivingImmunity { get; private set; }
        public float UnmitigatedPositionSuppression { get; private set; }
        public float TargetSpeed { get; private set; }
        public float GlobalResistance { get; private set; }
        public float LayerResistanceMultiplier { get; private set; }
        public float MuscleResistanceMultiplier { get; private set; }
        public float StateResistanceMultiplier { get; private set; }
        public float EffectiveResistance { get; private set; }
        public float PositionSuppression { get; private set; }
        public int LayerRuleIndex { get; private set; }

        public static RagdollPuppetCollisionResponseSnapshot Empty =>
            new RagdollPuppetCollisionResponseSnapshot(
                false,
                RagdollBoneHandle.Invalid,
                0f,
                0f,
                0f,
                1f,
                0f,
                0f,
                0f,
                0f,
                1f,
                1f,
                1f,
                0f,
                0f,
                -1);

        internal RagdollPuppetCollisionResponseSnapshot(
            bool hasResponse,
            RagdollBoneHandle bone,
            float fixedTime,
            float impulseMagnitude,
            float targetSpeed,
            float globalResistance,
            float layerResistanceMultiplier,
            float muscleResistanceMultiplier,
            float effectiveResistance,
            float positionSuppression,
            int layerRuleIndex)
            : this(
                hasResponse,
                bone,
                fixedTime,
                impulseMagnitude,
                impulseMagnitude,
                1f,
                0f,
                positionSuppression,
                targetSpeed,
                globalResistance,
                layerResistanceMultiplier,
                muscleResistanceMultiplier,
                1f,
                effectiveResistance,
                positionSuppression,
                layerRuleIndex)
        {
        }

        internal RagdollPuppetCollisionResponseSnapshot(
            bool hasResponse,
            RagdollBoneHandle bone,
            float fixedTime,
            float impulseMagnitude,
            float targetSpeed,
            float globalResistance,
            float layerResistanceMultiplier,
            float muscleResistanceMultiplier,
            float stateResistanceMultiplier,
            float effectiveResistance,
            float positionSuppression,
            int layerRuleIndex)
            : this(
                hasResponse,
                bone,
                fixedTime,
                impulseMagnitude,
                impulseMagnitude,
                1f,
                0f,
                positionSuppression,
                targetSpeed,
                globalResistance,
                layerResistanceMultiplier,
                muscleResistanceMultiplier,
                stateResistanceMultiplier,
                effectiveResistance,
                positionSuppression,
                layerRuleIndex)
        {
        }

        internal RagdollPuppetCollisionResponseSnapshot(
            bool hasResponse,
            RagdollBoneHandle bone,
            float fixedTime,
            float impulseMagnitude,
            float damageImpulseMagnitude,
            float sourceImpulseMultiplier,
            float receivingImmunity,
            float unmitigatedPositionSuppression,
            float targetSpeed,
            float globalResistance,
            float layerResistanceMultiplier,
            float muscleResistanceMultiplier,
            float stateResistanceMultiplier,
            float effectiveResistance,
            float positionSuppression,
            int layerRuleIndex)
        {
            HasResponse = hasResponse;
            Bone = bone;
            FixedTime = fixedTime;
            ImpulseMagnitude = impulseMagnitude;
            DamageImpulseMagnitude = damageImpulseMagnitude;
            SourceImpulseMultiplier = sourceImpulseMultiplier;
            ReceivingImmunity = receivingImmunity;
            UnmitigatedPositionSuppression = unmitigatedPositionSuppression;
            TargetSpeed = targetSpeed;
            GlobalResistance = globalResistance;
            LayerResistanceMultiplier = layerResistanceMultiplier;
            MuscleResistanceMultiplier = muscleResistanceMultiplier;
            StateResistanceMultiplier = stateResistanceMultiplier;
            EffectiveResistance = effectiveResistance;
            PositionSuppression = positionSuppression;
            LayerRuleIndex = layerRuleIndex;
        }
    }
}
