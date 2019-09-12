using System;
using UnityEngine;

// ReSharper disable UnassignedField.Global

namespace SnowboardPhysics.Core {
    
    [Serializable]
    public class ModelParameters {
        public LayerMask TerrainLayer;
        
        public bool RotateInAir;
        public float InAirRotationAngularVelocity;
        
        public bool InvertInputBackwards;

        public bool FallOnUnsafeLandingAngle;
        public float MaxSafeLandingAngle;
        
        public bool FallOnUnsafeLandingSpeed;
        public float MaxSafeLandingSpeed;

        public float TurnAbruptness;
        public float TurnToSlopeRate;
        public float SlowingDownRate;
        public float Friction;
        public float Slipping;

        public float AirResistance;

        public float ContactOffset;

        public float BoardLength;
        public float BoardWidth;
    }
}