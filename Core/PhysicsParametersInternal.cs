using System;
using UnityEngine;

namespace SnowboardPhysics.Core {

    [Serializable]
    internal class PhysicsParametersInternal {
        [Range(0, 1)]       public float VelocityDropOnContactCoefficient = 0.69f;
        [Range(0.9f, 1)]    public float VelocityPerFrameDropOnSlowingDown = 0.97f;

        [Range(0, 2)]       public float BoardLength = 1.6f;
        [Range(0, 0.5f)]    public float BoardWidth = 0.325f;
        [Range(0, 2.5f)]    public float BoarderHeight = 1.77f;
        [Range(0, 150)]     public float BoarderMass = 75f;
        [Range(0, 20)]      public float SidecutRadius = 14.8f;
        [Range(0, 1)]       public float BoardTakeOffLimit = 0.1f;

        [Range(0, 2)]       public float AirDensity = 1.3f;
        [Range(0, 2)]       public float BoarderFormFactor = 0.5f;

        [Range(0, 1)]       public float EdgeStaticFrictionCoefficient = 1f;
        [Range(0, 1)]       public float EdgeDynamicFrictionCoefficient = 0.5f;

        [Range(0, 1)]       public float SnowStaticFrictionCoefficient = 0.3f;
        [Range(0, 1)]       public float SnowDynamicFrictionCoefficient = 0.25f;

        [Range(0, 50)]      public float PureCurvedTurnMinSpeed = 15f;
        [Range(0, 1)]       public float VelocityLerpBetweenCarvingAndSkiddingRatio = 0.2f;

        [Range(0, 1080)]    public float SlowingDownAngularVelocity = 100f;
        [Range(0, 1080)]    public float RotateToSlopeAngularVelocity = 100f;

        [Range(0, 10)]      public float JumpVelocity = 3f;

        [Range(0, 100)]     public float Boost = 5f;
        [Range(0, 50)]      public float StartSpeed = 5f;

        [Range(0.1f, 2f)]   public float InputSensitivity = 0.7f;

        [Range(0, 720)]     public float RotationInAirAngularVelocity = 0;

        [Range(0, 90)]      public float MaxLandingAngle = 60;
        [Range(0, 50)]      public float SpeedOfFall = 10;
    }
}