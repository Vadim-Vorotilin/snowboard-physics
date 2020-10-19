//#define DRAW_DEBUG_LINES
//#define DEBUG_LOGS

using System;
using UnityEngine;

namespace SnowboardPhysics.Core {
    internal class SnowboardPhysicsModel {

        public event Action ContactLost;
        public event Action<Vector3> ContactFound;

        public Contact CurrentContact {
            get { return _currentContact; }
            private set {
                _lastContact = _currentContact;
                _currentContact = value;

                if (_currentContact != Contact.None)
                    return;
                
                FrontHit = null;
                RearHit = null;
            }
        }
        
        public Vector3 SlopeNormal { get; private set; }
        public Vector3 Velocity { get { return _velocity; } }

        public RaycastHit? FrontHit { get; private set; }
        public RaycastHit? RearHit { get; private set; }

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }

        public Vector3 Forward { get; private set; }
        public Vector3 Right { get; private set; }
        public Vector3 Up { get; private set; }

        private const float CentralInterpolationRatio = 0.2f;
        private const float MinSignificantSpeedSqr = 0.1f;
        private const float MinDynamicSpeedSqr = 1e-2f;

        private const float ContactSearchingRaycastDepth = 50f;

        private readonly PhysicsParametersInternal _parameters;
        private readonly bool _invertInputBackwards;
        private readonly float[,] _interpolationRadiuses;

        private readonly int _terrainLayerMask;
        
        private float _lastUnidirectionalitySign = 1;

        private bool _isTurningDown;
        private bool _isSlowingDown;
        private bool _updateContact = true;
        private bool _isFinished;

        private Vector3 _equipotentialLineVector;
        private float _slopeAngle;                  // α
        private float _equipotentialDeviation;      // β
        private float _tilt;                        // φ
        private float _rotationRatio;
        private Contact _currentContact = Contact.None;
        private Contact _lastContact = Contact.None;

        private Vector3 _slopeVector;

        private float _angleSnappingAngularVelocity;
        private float _carvingAngularVelocity;

        private bool _isEdgeFrictionStatic;
        private bool _isFrictionStatic;

        private Vector3 _velocity;
        private Vector3 _force;

        private float _skiddingTargetAngle;
        private float _skiddingSourceAngle;
        private float _skiddingDeltaAngle;

        private float _maxSlowingDownTilt;

        private float _angularVelocityOnContactLost;

        private float GetLandedAngularVelocity() {
            return _angleSnappingAngularVelocity + _carvingAngularVelocity;
        }
        
        private float AngularVelocity {
            get { 
                if (CurrentContact != Contact.None)
                    return GetLandedAngularVelocity();

                return _tilt / SnowboardModel.MaxTilt * _parameters.RotationInAirAngularVelocity * Mathf.Deg2Rad
                     + _angularVelocityOnContactLost;
            }
        }

        private float EdgeFrictionCoefficient {
            get { return _isEdgeFrictionStatic ?
                             _parameters.EdgeStaticFrictionCoefficient :
                             _parameters.EdgeDynamicFrictionCoefficient; }
        }

        private float SnowFrictionCoefficient {
            get { return _isFrictionStatic ?
                             _parameters.SnowStaticFrictionCoefficient :
                             _parameters.SnowDynamicFrictionCoefficient; }
        }

        private float UnidirectionalitySign { get { return _invertInputBackwards ? Mathf.Sign(Vector3.Dot(_velocity, Forward)) : 1; } }

        private float CalculatedTurnRadius {
            get {
                float tan_φ = Mathf.Tan(_tilt * Mathf.Deg2Rad);
                return _parameters.SidecutRadius / (tan_φ * 2f) * UnidirectionalitySign;
            }
        }

        private float TurnRadius {
            get {
                float radius = CalculatedTurnRadius;

                if (Mathf.Abs(radius) > _parameters.SidecutRadius)
                    return radius;

                return Mathf.Sign(radius) * Mathf.Lerp(Mathf.Abs(radius),
                                                       _parameters.SidecutRadius,
                                                       (_velocity.magnitude - 10f) / 25f);
            }
        }

        private Vector3 FrontPoint { get { return _calculatedPosition + Forward * (_parameters.BoardLength / 2f - _parameters.BoardWidth / 2f); } }
        private Vector3 RearPoint { get { return _calculatedPosition - Forward * (_parameters.BoardLength / 2f - _parameters.BoardWidth / 2f); } }

        public SnowboardPhysicsModel(Vector3 position,
                                     Quaternion rotation,
                                     PhysicsParametersInternal parameters,
                                     int terrainLayerMask,
                                     bool invertInputBackwards) {
            Position = position;
            Rotation = rotation;

            Forward = Rotation * Vector3.forward;
            Right = Rotation * Vector3.right;
            Up = Rotation * Vector3.up;

            _parameters = parameters;
            _invertInputBackwards = invertInputBackwards;

            _terrainLayerMask = terrainLayerMask;

            _interpolationRadiuses = new [,] { { _parameters.BoardLength * 0.5f, 0.3f },
                                               { _parameters.BoardLength,        0.3f },
                                               { _parameters.BoardLength * 1.5f, 0.2f } };
        }

        private Vector3 _calculatedPosition;

        public void Update(float tilt, float rotationRatio) {
            Forward = Rotation * Vector3.forward;
            Right = Rotation * Vector3.right;
            Up = Rotation * Vector3.up;

            if (_isFinished) {
                tilt = 0;
                rotationRatio = 0;
            }

            _tilt = tilt * _lastUnidirectionalitySign;
            _rotationRatio = rotationRatio;
            _isTurningDown = rotationRatio > 0.95f;
            _isSlowingDown = _isFinished || rotationRatio < -0.95f;

            _maxSlowingDownTilt = Mathf.Lerp(_maxSlowingDownTilt,
                                             _isSlowingDown ? SnowboardModel.MaxTilt : 0,
                                             0.05f);

            _calculatedPosition = Position + _velocity * Time.deltaTime;
                
            UpdateSlope();

            if (_updateContact)
                UpdateContact();

            UpdateTransform();
        }

        public void FixedUpdate() {
            if (Vector3.ProjectOnPlane(Velocity, SlopeNormal).sqrMagnitude >= MinSignificantSpeedSqr &&
                CurrentContact != Contact.None) {
                _lastUnidirectionalitySign = UnidirectionalitySign;
            }
            _angleSnappingAngularVelocity = 0;

            UpdateForces();
            UpdateSkidding();
        }

        public void Reset(Vector3 position, Quaternion rotation, float velocity = 0) {
            Position = position;
            Rotation = rotation;

            _velocity = Forward * velocity;

            _angleSnappingAngularVelocity = 0;
            _carvingAngularVelocity = 0;

            CurrentContact = Contact.None;

            _isFinished = false;
        }

        public void Finish() {
            _isFinished = true;
        }

        public void Jump() {
            if (CurrentContact == Contact.None)
                return;
            
            CurrentContact = Contact.None;
            _velocity += Up * _parameters.JumpVelocity;

            _updateContact = false;
            Delayer.ExecuteAfter(() => _updateContact = true, 0.2f);

            OnContactLost();
        }

        private void UpdateSlope() {
            Vector3 point;
            Vector3 normal;

            var success = InterpolateNormal(out point, out normal);

            if (!success) {
                _slopeVector = Vector3.zero;
                _slopeAngle = 0;
                SlopeNormal = Vector3.zero;
                _equipotentialLineVector = Vector3.zero;
                _equipotentialDeviation = 0;

                return;
            }

            SlopeNormal = normal;
            _equipotentialLineVector = Vector3.Cross(Vector3.up, SlopeNormal);
            _equipotentialDeviation = Vector3.Angle(Forward, _equipotentialLineVector) * Mathf.Sign(Vector3.Dot(Forward, _slopeVector));

            if (_equipotentialLineVector.sqrMagnitude <= 1e-6)
                _equipotentialLineVector = Right;

            _slopeAngle = Vector3.Angle(SlopeNormal, Vector3.up);

            _slopeVector = Quaternion.AngleAxis(90, _equipotentialLineVector) * SlopeNormal;
        }

        private bool InterpolateNormal(out Vector3 point, out Vector3 normal) {
            RaycastHit hit;
            bool success = Physics.Raycast(_calculatedPosition + Vector3.up * 10f, Vector3.down, out hit, 20f, _terrainLayerMask);

            if (!success) {
                point = Vector3.zero;
                normal = Vector3.zero;

                return false;
            }

            point = hit.point;
            normal = hit.normal * CentralInterpolationRatio;

            float totalRatio = CentralInterpolationRatio;

            for (int i = 0; i < _interpolationRadiuses.GetLength(0); i++) {
                int subCount = 0;
                Vector3 subNormalsSum = Vector3.zero;

                float radius = _interpolationRadiuses[i, 0];

                Vector3[] subs = {
                    _calculatedPosition + Forward * radius,
                    _calculatedPosition - Forward * radius,
                    _calculatedPosition + Right * radius,
                    _calculatedPosition - Right * radius
                };

                foreach (var sub in subs) {
                    RaycastHit subHit;

                    if (!Physics.Raycast(sub + Vector3.up * (radius + 1), Vector3.down, out subHit, 2 * (radius + 1), _terrainLayerMask))
                        continue;

                    subNormalsSum += subHit.normal;
                    subCount++;
                }

                if (subCount > 0) {
                    float ratio = _interpolationRadiuses[i, 1] * subCount / subs.Length;

                    totalRatio += ratio;
                    normal += subNormalsSum / subCount * ratio;
                }
            }

            normal *= 1f / totalRatio;

            return true;
        }

        private void UpdateContact() {
            RaycastHit hit;
            bool success = Physics.Raycast(FrontPoint + Vector3.up * ContactSearchingRaycastDepth,
                                           Vector3.down,
                                           out hit,
                                           ContactSearchingRaycastDepth * 2,
                                           _terrainLayerMask);

            bool isFrontInContact = success && (FrontPoint.y - hit.point.y <= _parameters.BoardTakeOffLimit);
            FrontHit = isFrontInContact ? new RaycastHit?(hit) : null;

            success = Physics.Raycast(RearPoint + Vector3.up * ContactSearchingRaycastDepth,
                                      Vector3.down,
                                      out hit,
                                      ContactSearchingRaycastDepth * 2,
                                      _terrainLayerMask);

            bool isRearInContact = success && (RearPoint.y - hit.point.y <= _parameters.BoardTakeOffLimit);
            RearHit = isRearInContact ? new RaycastHit?(hit) : null;

            if (isFrontInContact && isRearInContact)
                CurrentContact = Contact.Full;
            else if (isFrontInContact || isRearInContact)
                CurrentContact = Contact.Half;
            else
                CurrentContact = Contact.None;

            var lastVelocity = _velocity;

            if (CurrentContact == Contact.Full) {
                _velocity = Vector3.ProjectOnPlane(_velocity, SlopeNormal);
            }

            if (_lastContact != Contact.None && CurrentContact == Contact.None
                || _lastContact == Contact.None && CurrentContact != Contact.None) {
                if (CurrentContact != Contact.None)
                    OnContactFound(lastVelocity);
                else
                    OnContactLost();

#if DRAW_DEBUG_LINES
                var point = ContactPoint ?? _lastContactPoint ?? Position;

                Debug.DrawLine(point, point + _slopeVector, Color.green, 300f, false);
                Debug.DrawLine(point, point + lastVelocity, Color.red, 300f, false);
                Debug.DrawLine(point, point + _velocity, Color.cyan, 300f, false);
                Debug.DrawLine(point, point + _slopeNormal, Color.yellow, 300f, false);
#endif
            }

#if DEBUG_LOGS
            if (_lastContact != CurrentContact)
                Debug.LogFormat("Contact {0} -> {1}", _lastContact, CurrentContact);
#endif
        }

        private void OnContactLost() {
#if DEBUG_LOGS
            Debug.LogFormat("Contact LOST");
#endif
            _angularVelocityOnContactLost = GetLandedAngularVelocity();
            
            if (ContactLost != null)
                ContactLost();
        }

        private void OnContactFound(Vector3 lastVelocity) {
            float angle = Vector3.Angle(lastVelocity, _velocity) * Mathf.Deg2Rad;
            float coef = Mathf.Min(1, 1f / (angle + _parameters.VelocityDropOnContactCoefficient) - 0.44f);  // hyperbolic coefficient of velocity fall
            _velocity *= coef;
            _angleSnappingAngularVelocity *= coef;

#if DEBUG_LOGS
            Debug.LogFormat("Contact FOUND. Velocity {0} -> {1}. Ang: {2}. Coef: {3}", lastVelocity.magnitude, _velocity.magnitude, angle * Mathf.Rad2Deg, coef);
#endif

            if (ContactFound != null)
                ContactFound(_velocity - lastVelocity);
        }

        private void UpdateForces() {
            _force = CalculateForce();

            Vector3 airResist = CalculateAirResistance();

            Vector3 acceleration = (_force + airResist) / _parameters.BoarderMass * Time.fixedDeltaTime;

            if (Vector3.Dot(acceleration, _velocity) < 0 && Vector3.Project(acceleration, _velocity).sqrMagnitude > _velocity.sqrMagnitude)
                acceleration *= Mathf.Sqrt(_velocity.sqrMagnitude / Vector3.Project(acceleration, _velocity).sqrMagnitude);

            _velocity += acceleration;

            _isFrictionStatic = _velocity.sqrMagnitude < MinDynamicSpeedSqr;

            if (_isFrictionStatic)
                _velocity = Vector3.zero;

            if (CurrentContact == Contact.Full) {
                Vector3 projectionOnForward = Vector3.Project(_velocity, Vector3.ProjectOnPlane(Forward, SlopeNormal));
                Vector3 projectionOnRight = Vector3.Project(_velocity, Vector3.ProjectOnPlane(Right, SlopeNormal));

                if (!_isEdgeFrictionStatic &&
                    projectionOnForward.sqrMagnitude > 1e-4 &&
                    Vector3.Dot(Vector3.up, projectionOnRight) >= 0) {
                    _isEdgeFrictionStatic = true;
                } else {
                    _isEdgeFrictionStatic = Vector3.Project(_velocity, Right).sqrMagnitude < 1e-4;
                }

                if (!_isSlowingDown)
                    _velocity = Vector3.Lerp(_velocity,
                                             Vector3.Project(_velocity, Forward)
                                                 * Mathf.Lerp(1f, 0.98f, Mathf.Abs(AngularVelocity)
                                                 / (2.3f * Mathf.PI)),
                                             Mathf.Pow(Mathf.Clamp(_velocity.magnitude, 0, _parameters.PureCurvedTurnMinSpeed) /
                                                       _parameters.PureCurvedTurnMinSpeed, 2)
                                                 * _parameters.VelocityLerpBetweenCarvingAndSkiddingRatio);
                else
                    _velocity *= _parameters.VelocityPerFrameDropOnSlowingDown;
            }

#if DRAW_DEBUG_LINES
            Debug.DrawLine(Position, Position + _velocity, Color.red, 0f, false);
#endif
        }

        private Vector3 CalculateAirResistance() {
            return -_velocity.normalized * _parameters.BoarderFormFactor * _parameters.AirDensity *
                    _velocity.sqrMagnitude * _parameters.BoarderHeight * _parameters.BoardWidth / 2f;
        }

        private Vector3 CalculateForce() {
            float sin_α = Mathf.Sin(_slopeAngle * Mathf.Deg2Rad);
            float cos_α = Mathf.Cos(_slopeAngle * Mathf.Deg2Rad);

            float sin_φ = Mathf.Sin(_tilt * Mathf.Deg2Rad);

            float sin_β = Mathf.Sin(_equipotentialDeviation * Mathf.Deg2Rad);
            float cos_β = Mathf.Cos(_equipotentialDeviation * Mathf.Deg2Rad);
            float abs_cos_β = Mathf.Abs(cos_β);

            float g = -Physics.gravity.y;
            float μ0 = EdgeFrictionCoefficient;

            float μ = μ0 * Mathf.Abs(sin_φ);

            if (_isEdgeFrictionStatic) {
                float tan_α = Mathf.Tan(_slopeAngle * Mathf.Deg2Rad);

                μ = Mathf.Min(μ, tan_α);
            }

            // *** GRAVITY *** //

            Vector3 aG = new Vector3(0,
                                     0,
                                     g * sin_α);

            if (_isSlowingDown && !_isEdgeFrictionStatic)
                aG = Vector3.zero;

            // *** EDGE FRICTION ACCELERATION *** //
            Vector3 aEf = new Vector3(-g * cos_α * μ * sin_β * Mathf.Sign(Vector3.Dot(Right, _velocity)),
                                      0,
                                      -g * cos_α * μ * abs_cos_β);

            float vZ = Vector3.Project(_velocity, Forward).magnitude;

            _carvingAngularVelocity = CurrentContact == Contact.Full ? vZ / TurnRadius : 0;

            float θ = Vector3.Angle(_equipotentialLineVector, Vector3.ProjectOnPlane(_velocity, SlopeNormal)) * Mathf.Sign(Vector3.Dot(_velocity, _slopeVector));
            float sin_θ = Mathf.Sin(θ * Mathf.Deg2Rad);
            float cos_θ = Mathf.Cos(θ * Mathf.Deg2Rad);
            float muF = SnowFrictionCoefficient;

            // *** FRICTION ACCELERATION *** //
            float friction = g * cos_α * muF;

            Vector3 aF = new Vector3(-friction * cos_θ, 0, -friction * sin_θ);

            Vector3 aB = Forward.normalized * _lastUnidirectionalitySign * _rotationRatio * Mathf.Pow(Mathf.Clamp01((30 - _slopeAngle) / 30), 1.5f) * _parameters.Boost *
                Mathf.Clamp01(Mathf.Sign(Vector3.Dot(Forward * _lastUnidirectionalitySign, Vector3.down)));

            Vector3 a = aG + aEf + aF + aB;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, _slopeVector);

            a = rotation * a;

#if DRAW_DEBUG_LINES
            Vector3 p = (RearPoint + FrontPoint) / 2f;
            const float time = 0f;
            Debug.DrawLine(p, p + rotation * aEf, Color.blue, time, false);
            Debug.DrawLine(p + Forward * 0.01f, p + Forward * 0.01f + rotation * aF, Color.red, time, false);
            Debug.DrawLine(p + Forward * 0.02f, p + Forward * 0.02f + rotation * aG, Color.black, time, false);
            Debug.DrawLine(p + Forward * 0.03f, p + Forward * 0.03f + a, Color.green, time, false);
#endif

            switch (CurrentContact) {
                case Contact.None:
                    return Vector3.down * g * _parameters.BoarderMass;
                case Contact.Half:
                    return a / 2f + Vector3.down * g * _parameters.BoarderMass / 2f;
                case Contact.Full:
                    return a * _parameters.BoarderMass;
                default:
                    throw new ArgumentOutOfRangeException(string.Format("Contact enum value = {0}", CurrentContact));
            }
        }

        private void UpdateTransform() {
            Vector3 frontPoint = FrontHit.HasValue ? FrontHit.Value.point : FrontPoint;
            Vector3 rearPoint = RearHit.HasValue ? RearHit.Value.point : RearPoint;
            Vector3 centralPoint = (frontPoint + rearPoint) / 2f;
            Position = centralPoint;

            float deltaRotation = CalculateDeltaRotation();

            if (CurrentContact != Contact.None) {
                float xRotation = -Mathf.Asin((frontPoint.y - rearPoint.y) / (frontPoint - rearPoint).magnitude) * Mathf.Rad2Deg;
                float yRotation = Mathf.Atan2(frontPoint.x - rearPoint.x, frontPoint.z - rearPoint.z) * Mathf.Rad2Deg + deltaRotation;

                float tilt = _tilt;

                if (_isSlowingDown)
                    tilt = Mathf.Sign(Vector3.Dot(Right, -Velocity))
                         * _maxSlowingDownTilt
                         * Mathf.Clamp01(Velocity.sqrMagnitude / 100f);
                
                var surfaceVector = Vector3.Cross(SlopeNormal, Forward) * Mathf.Sign(Vector3.Dot(Forward, _equipotentialLineVector));
                float maxZRotation = Vector3.Angle(Vector3.up, surfaceVector) / 2;
                float visualTilt = Mathf.Sign(tilt)
                                 * Mathf.Min(Mathf.Abs(tilt) * Mathf.Clamp01(maxZRotation / SnowboardModel.MaxTilt),        // limiting the tilt by the slope angle 
                                           SnowboardModel.MaxTilt * Mathf.Clamp01((Velocity.sqrMagnitude + 10) / 100f));    // limiting the tilt by the speed in order to avoid high tilt angle on low speed
                
                float zRotation = -visualTilt;

                Rotation = Quaternion.Euler(xRotation, yRotation, zRotation);
            } else {
                Rotation *= Quaternion.AngleAxis(deltaRotation, Vector3.up);
            }
        }

        private float GetSkiddingSourceAngle(Vector3 sourceDir) {
            return Vector3.Angle(sourceDir, Forward)
                 * Mathf.Sign(Vector3.Dot(SlopeNormal, Vector3.Cross(sourceDir, Forward)));
        }

        private void UpdateSkidding() {
            if (CurrentContact == Contact.Full) {
                if (_isSlowingDown) {
                    _skiddingSourceAngle = GetSkiddingSourceAngle(Vector3.Lerp(_slopeVector, _velocity, Mathf.Clamp01(_velocity.sqrMagnitude / 50)));
                    _skiddingTargetAngle = 90f * Mathf.Sign(_skiddingSourceAngle);
                }
                else {
                    _skiddingSourceAngle = GetSkiddingSourceAngle(_velocity);
                    _skiddingTargetAngle = _isTurningDown ? 0f : _skiddingSourceAngle;
                }

                _skiddingDeltaAngle = _skiddingTargetAngle - _skiddingSourceAngle;

                if (_velocity.sqrMagnitude < MinSignificantSpeedSqr)
                    _skiddingDeltaAngle = 0;

                float maxAngularVelocity = _isSlowingDown ?
                                               _parameters.SlowingDownAngularVelocity :
                                               _parameters.RotateToSlopeAngularVelocity;

                _angleSnappingAngularVelocity = maxAngularVelocity
                                                * Mathf.Sign(_skiddingDeltaAngle)
                                                * Mathf.Abs(Mathf.Pow(Mathf.Clamp(_skiddingDeltaAngle / 45f, -1f, 1f), 2f))
                                                * Mathf.Deg2Rad;
            } else {
                _skiddingTargetAngle = 0;
                _skiddingSourceAngle = 0;
                _skiddingDeltaAngle = 0;
            }
        }

        private float CalculateDeltaRotation() {
            return AngularVelocity * Mathf.Rad2Deg * Time.deltaTime;
        }
        
    }

}
