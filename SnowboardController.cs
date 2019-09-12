using Smoother;
using SnowboardPhysics.Core;
using SnowboardPhysics.Interfaces;
using UnityEngine;

namespace SnowboardPhysics {

    [RequireComponent(typeof(Rigidbody))]
    public class SnowboardController : MonoBehaviour, ISnowboardInput {
    
        const float RespawnAltitude = 2f;
        const int VelocitySmoothingCapacity = 180;

        [SerializeField] private ModelParameters _parameters;
    
        public SnowboardModel Model { get; private set; }

        public float Turning { get { return Input.GetAxis("Horizontal"); } }
        public float Speeding { get { return Input.GetAxis("Vertical"); } }
        public bool Jump { get { return Input.GetKeyDown(KeyCode.Space); } }

        public bool IsInContact {
            get { return !_isHit && Model.CurrentContact != Contact.None; }
        }
    
        Vector3Smoother _velocitySmoother;
    
        void Awake () {
            _hitPosition = transform.position;
            
            Model = new SnowboardModel(transform.position, transform.rotation, this, _parameters);
            Model.ContactLost += ModelOnContactLost;
            Model.ContactFound += ModelOnContactFound;
        }
    
        private void ModelOnContactLost() {
            //_animator.SetBool("IsInAir", true);
        }
    
        private void ModelOnContactFound(Vector3 acceleration) {
            //_animator.SetBool("IsInAir", false);
    
            //TODO: всунуть это в SnowboardModel
            if (_parameters.FallOnUnsafeLandingAngle &&
                Vector3.Angle(Vector3.ProjectOnPlane(Model.Velocity, Model.Up), Model.Forward) > _parameters.MaxSafeLandingAngle &&
                Vector3.Angle(Vector3.ProjectOnPlane(Model.Velocity, Model.Up), -Model.Forward) > _parameters.MaxSafeLandingAngle) {
                OnHit(null);
                return;
            }

            if (_parameters.FallOnUnsafeLandingSpeed &&
                acceleration.magnitude > _parameters.MaxSafeLandingSpeed)
                OnHit(null);
        }
    
        bool _isStarted;
    
        void Start() {
            _rigidbody = GetComponent<Rigidbody>();
            _velocitySmoother = new Vector3Smoother(VelocitySmoothingCapacity);
    
            Reset();
        }
    
        void Reset() {
            if (_isHit)
                return;
    
            GetUp();
        }
    
        bool _isHit = false;
        Rigidbody _rigidbody;
        Vector3 _hitPosition;
    
        void OnHit(Collider col) {
            if (col != null && col.isTrigger)
                return;
    
            if (_isHit)
                return;
    
            _isHit = true;
            _hitPosition = transform.position;
    
    //        _animator.enabled = false;
            
            _rigidbody.isKinematic = false;
            _rigidbody.AddForce(Model.Velocity, ForceMode.VelocityChange);
            
            Model.StopSimulation();
        }
    
        void Update() {
            if (!_isStarted)
                return;

            if (_isHit) {
                if (Input.GetKeyDown(KeyCode.Return))
                    GetUp();
            } else {
                Model.Update();
    
                _velocitySmoother.AddValue(Model.Velocity);
    
    //            _animator.SetFloat("TiltRatio", Model.VisualTilt / SnowboardModelSimplified.MaxTilt);
    //            _animator.SetFloat("SpeedRatio", Model.Speed);
    //            _animator.SetBool("IsPreparingForJump", Model.IsPreparingForJump);
                
                // A hack to prevent animations flickering due to Stance quickly changing at 0 speed
                if (_velocitySmoother.Value.magnitude > 1 && Model.Velocity.magnitude > 1)
                {
    //                _animator.SetBool("IsGoofy", Model.Stance == SnowboardStance.Goofy);
                }
            }
        }

        void LateUpdate() {
            if (_isHit)
                return;
            
            transform.position = Vector3.Lerp(transform.position, Model.Position, 0.95f);
            transform.rotation = Quaternion.Lerp(transform.rotation, Model.Rotation, 0.25f);
        }
    
        void GetUp() {
    //        _animator.enabled = true;
            _rigidbody.isKinematic = true;
    
            Vector3 position = _hitPosition;
            Quaternion rotation = transform.rotation;
            RaycastHit hit;
    
            if (Physics.Raycast(position + Vector3.up * 1e+6f, Vector3.down, out hit, 2e+6f, _parameters.TerrainLayer.value))
                position = hit.point + Vector3.up * RespawnAltitude;
    
            Model.ResetSimulation(position, rotation, 5);
    
            _isStarted = true;
    
            _velocitySmoother.Reset();
        }
    
        void FixedUpdate () {
            Model.FixedUpdate();
        }
    }
}
