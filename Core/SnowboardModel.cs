using System;
using SnowboardPhysics.Interfaces;
using UnityEngine;

namespace SnowboardPhysics.Core {
    public class SnowboardModel {
        public event Action ContactLost {
            add { _physics.ContactLost += value; }
            remove { _physics.ContactLost -= value; }
        }
    
        public event Action<Vector3> ContactFound {
            add { _physics.ContactFound += value; }
            remove { _physics.ContactFound -= value; }
        }
    
        internal const float MaxTilt = 60f;
    
        public Vector3 Velocity { get { return _physics.Velocity; } }
        public Vector3 Position { get { return _physics.Position; } }
        public Quaternion Rotation { get { return _physics.Rotation; } }
    
        public Vector3 Right { get { return _physics.Right; } }
        public Vector3 Up { get { return _physics.Up; } }
        public Vector3 Forward { get { return _physics.Forward; } }
        
        internal Contact CurrentContact { get { return _physics.CurrentContact; } }
        internal RaycastHit? FrontHit { get { return _physics.FrontHit; } }
        internal RaycastHit? RearHit { get { return _physics.RearHit; } }
        internal Vector3 SlopeNormal { get { return _physics.SlopeNormal; } }
        internal float BoardWidth { get { return _physicsParameters.BoardWidth; } }

        private readonly ISnowboardInput _input;
        private readonly SnowboardPhysicsModel _physics;
        private readonly PhysicsParametersInternal _physicsParameters = new PhysicsParametersInternal {
            VelocityDropOnContactCoefficient = 0.9f, //0.55f
            VelocityPerFrameDropOnSlowingDown = 0.975f,
            BoardLength = 1.6f,
            BoardWidth = 0.325f,
            BoarderHeight = 1.77f,
            BoarderMass = 75f,
            SidecutRadius = 15.2f,
            BoardTakeOffLimit = 0.1f,
            AirDensity = 0.5f,
            BoarderFormFactor = 0.35f,
            EdgeStaticFrictionCoefficient = 0.03f,
            EdgeDynamicFrictionCoefficient = 0.03f,
            SnowStaticFrictionCoefficient = 0.03f,
            SnowDynamicFrictionCoefficient = 0.003f,
            PureCurvedTurnMinSpeed = 15f,
            VelocityLerpBetweenCarvingAndSkiddingRatio = 0.3f,    // 1 for skateboard
            SlowingDownAngularVelocity = 100f,
            RotateToSlopeAngularVelocity = 0f,
            JumpVelocity = 3f,
            Boost = 0f,
            StartSpeed = 5f,
            InputSensitivity = 0.55f,
            RotationInAirAngularVelocity = 360f,
            MaxLandingAngle = 60f,
            SpeedOfFall = 20f
        };

        private readonly ModelParameters _modelParameters;
    
        public SnowboardModel(Vector3 position,
                              Quaternion rotation,
                              ISnowboardInput input,
                              ModelParameters parameters) {
            _modelParameters = parameters;
            
            SetParameters();
            
            _physics = new SnowboardPhysicsModel(position,
                                                 rotation,
                                                 _physicsParameters,
                                                 parameters.TerrainLayer.value,
                                                 parameters.InvertInputBackwards);
    
            _input = input;
        }
    
        public void Update() {
#if UNITY_EDITOR
            SetParameters();
#endif
            
            if (_input.Jump)
                _physics.Jump();
    
            var turn = _input.Turning;
            
            _physics.Update(MaxTilt * Mathf.Pow(Mathf.Abs(turn), 1f / _physicsParameters.InputSensitivity) * Mathf.Sign(turn),
                            _input.Speeding);
        }
    
        public void FixedUpdate () {
            _physics.FixedUpdate();
        }
    
        public void ResetSimulation(Vector3 position, Quaternion rotation, float velocity = 0) {
            _physics.Reset(position, rotation, velocity);
        }

        public void ResetSimulation() {
            _physics.Reset(Position, Rotation);
        }
    
        public void StopSimulation() {
            _physics.Finish();
        }

        private void SetParameters() {
            _physicsParameters.RotationInAirAngularVelocity = !_modelParameters.RotateInAir
                                                                  ? 0
                                                                  : _modelParameters.InAirRotationAngularVelocity;

            _physicsParameters.BoardLength = _modelParameters.BoardLength;
            _physicsParameters.BoardWidth = _modelParameters.BoardWidth;

            _physicsParameters.MaxLandingAngle = _modelParameters.FallOnUnsafeLandingAngle
                                                     ? _modelParameters.MaxSafeLandingAngle
                                                     : float.PositiveInfinity;

            _physicsParameters.SpeedOfFall = _modelParameters.FallOnUnsafeLandingSpeed
                                                 ? _modelParameters.MaxSafeLandingSpeed
                                                 : float.PositiveInfinity;

            _physicsParameters.SidecutRadius =
                Mathf.Lerp(50f, 1f, _modelParameters.TurnAbruptness);
            
            _physicsParameters.RotateToSlopeAngularVelocity =
                Mathf.Lerp(0, 180, _modelParameters.TurnToSlopeRate);

            _physicsParameters.SlowingDownAngularVelocity =
                Mathf.Lerp(30, 180, _modelParameters.SlowingDownRate);

            _physicsParameters.VelocityLerpBetweenCarvingAndSkiddingRatio =
                Mathf.Lerp(0.1f, 1f, _modelParameters.Slipping);

            _physicsParameters.BoardTakeOffLimit =
                Mathf.Lerp(0.01f, 0.3f, _modelParameters.ContactOffset);

            var friction = Mathf.Lerp(0.01f, 0.3f, _modelParameters.Friction);

            _physicsParameters.EdgeStaticFrictionCoefficient = friction;
            _physicsParameters.EdgeDynamicFrictionCoefficient = friction;
            _physicsParameters.SnowStaticFrictionCoefficient = friction;
            _physicsParameters.SnowDynamicFrictionCoefficient = friction * 0.1f;
            
            _physicsParameters.AirDensity = Mathf.Lerp(0, 5, _modelParameters.AirResistance);
        }
    
    }


}
