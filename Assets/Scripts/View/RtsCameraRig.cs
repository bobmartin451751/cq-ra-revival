using UnityEngine;
using UnityEngine.InputSystem;

namespace CQRa.View
{
    /// <summary>
    /// Classic RTS camera: orbits around a ground pivot with pan, zoom, yaw rotation,
    /// middle-mouse drag and optional edge scrolling. The pivot follows the heightmap
    /// and the camera is kept above the surface.
    /// </summary>
    public sealed class RtsCameraRig : MonoBehaviour
    {
        private const float Deg2Rad = Mathf.Deg2Rad;

        [Header("Pose")]
        [SerializeField] private Vector3 _pivot = Vector3.zero;
        [SerializeField] private float _yaw = 0f;
        [SerializeField] private float _pitch = 52f;
        [SerializeField] private float _distance = 74f;

        [Header("Limits")]
        [SerializeField] private float _minDistance = 20f;
        [SerializeField] private float _maxDistance = 140f;
        [SerializeField] private float _minPitch = 30f;
        [SerializeField] private float _maxPitch = 74f;

        [Header("Input")]
        [SerializeField] private float _panSpeed = 46f;
        [SerializeField] private float _rotateSpeed = 95f;
        [SerializeField] private float _zoomStep = 0.12f;
        [SerializeField] private float _dragScale = 0.0019f;
        [SerializeField] private float _edgeScrollPixels = 10f;
        [SerializeField] private bool _enableEdgeScroll = true;
        [SerializeField] private bool _enableInput = true;

        [Header("Feel")]
        [SerializeField] private float _smoothing = 13f;
        [SerializeField] private float _cameraGroundClearance = 2.5f;

        [Header("Scene")]
        [SerializeField] private HeightmapTerrain _terrain;
        [SerializeField] private Camera _camera;

        private Vector3 _pivotTarget;
        private float _yawTarget;
        private float _pitchTarget;
        private float _distanceTarget;

        private bool _dragging;
        private Vector2 _lastDragPosition;

        public HeightmapTerrain Terrain => _terrain;

        public void Configure(HeightmapTerrain terrain, Camera camera, bool enableEdgeScroll = true)
        {
            _terrain = terrain;
            _camera = camera;
            _enableEdgeScroll = enableEdgeScroll;
            if (terrain != null)
            {
                _maxDistance = Mathf.Max(_maxDistance, terrain.Size * 0.45f);
            }
        }

        public void SetPose(Vector3 pivot, float yaw, float pitch, float distance)
        {
            _pivot = pivot;
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, _minPitch, _maxPitch);
            _distance = Mathf.Clamp(distance, _minDistance, _maxDistance);
            SnapTargetsToPose();
            ApplyPose();
        }

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            if (_terrain == null)
            {
                _terrain = FindFirstObjectByType<HeightmapTerrain>();
            }

            if (_terrain != null)
            {
                _maxDistance = Mathf.Max(_maxDistance, _terrain.Size * 0.45f);
            }

            SnapTargetsToPose();
            GroundPivot(_pivot);
            _pivotTarget = _pivot;
            ApplyPose();
        }

        private void Update()
        {
            if (!_enableInput)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            ReadKeyboard(dt);
            ReadMouse(dt);
        }

        private void LateUpdate()
        {
            var t = 1f - Mathf.Exp(-_smoothing * Time.unscaledDeltaTime);

            _pivot = Vector3.Lerp(_pivot, _pivotTarget, t);
            _yaw = Mathf.LerpAngle(_yaw, _yawTarget, t);
            _pitch = Mathf.Lerp(_pitch, _pitchTarget, t);
            _distance = Mathf.Lerp(_distance, _distanceTarget, t);

            ApplyPose();
        }

        private void ReadKeyboard(float dt)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var moveX = 0f;
            var moveZ = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                moveX -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                moveX += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                moveZ -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                moveZ += 1f;
            }

            if (moveX != 0f || moveZ != 0f)
            {
                var direction = ForwardOnGround() * moveZ + RightOnGround() * moveX;
                direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;

                // Panning feels natural when it scales with how close you are to the ground.
                var speedScale = Mathf.Clamp(_distance / 74f, 0.45f, 1.8f);
                _pivotTarget += direction * (_panSpeed * speedScale * dt);
            }

            if (keyboard.qKey.isPressed)
            {
                _yawTarget -= _rotateSpeed * dt;
            }

            if (keyboard.eKey.isPressed)
            {
                _yawTarget += _rotateSpeed * dt;
            }

            ClampTargets();
        }

        private void ReadMouse(float dt)
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var notches = scroll / 120f;
                _distanceTarget = Mathf.Clamp(
                    _distanceTarget * (1f - notches * _zoomStep),
                    _minDistance,
                    _maxDistance);
            }

            if (mouse.middleButton.wasPressedThisFrame)
            {
                _dragging = true;
                _lastDragPosition = mouse.position.ReadValue();
            }

            if (_dragging)
            {
                var position = mouse.position.ReadValue();

                if (mouse.middleButton.isPressed)
                {
                    var delta = position - _lastDragPosition;
                    var scale = _distanceTarget * _dragScale;
                    _pivotTarget -= RightOnGround() * (delta.x * scale);
                    _pivotTarget -= ForwardOnGround() * (delta.y * scale);
                }

                _lastDragPosition = position;

                if (mouse.middleButton.wasReleasedThisFrame)
                {
                    _dragging = false;
                }
            }

            if (_enableEdgeScroll && Application.isFocused && !_dragging)
            {
                var position = mouse.position.ReadValue();
                var edge = _edgeScrollPixels;

                var edgeX = 0f;
                var edgeZ = 0f;

                if (position.x < edge)
                {
                    edgeX -= 1f;
                }
                else if (position.x > Screen.width - edge)
                {
                    edgeX += 1f;
                }

                if (position.y < edge)
                {
                    edgeZ -= 1f;
                }
                else if (position.y > Screen.height - edge)
                {
                    edgeZ += 1f;
                }

                if (edgeX != 0f || edgeZ != 0f)
                {
                    var direction = ForwardOnGround() * edgeZ + RightOnGround() * edgeX;
                    var speedScale = Mathf.Clamp(_distance / 74f, 0.45f, 1.8f);
                    _pivotTarget += direction * (_panSpeed * speedScale * dt);
                }
            }

            ClampTargets();
        }

        private void ClampTargets()
        {
            _distanceTarget = Mathf.Clamp(_distanceTarget, _minDistance, _maxDistance);
            _pitchTarget = Mathf.Clamp(_pitchTarget, _minPitch, _maxPitch);

            if (_terrain == null)
            {
                return;
            }

            var limit = _terrain.Size * 0.5f - 8f;
            _pivotTarget.x = Mathf.Clamp(_pivotTarget.x, -limit, limit);
            _pivotTarget.z = Mathf.Clamp(_pivotTarget.z, -limit, limit);
        }

        private void GroundPivot(Vector3 pivot)
        {
            if (_terrain != null)
            {
                pivot.y = _terrain.SampleHeight(pivot.x, pivot.z);
            }

            _pivot = pivot;
        }

        private void ApplyPose()
        {
            if (_terrain != null)
            {
                _pivot.y = _terrain.SampleHeight(_pivot.x, _pivot.z);
                _pivotTarget.y = _pivot.y;
            }

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var position = _pivot + rotation * new Vector3(0f, 0f, -_distance);

            if (_terrain != null)
            {
                var floor = _terrain.SampleHeight(position.x, position.z) + _cameraGroundClearance;
                if (position.y < floor)
                {
                    position.y = floor;
                }
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        private Vector3 ForwardOnGround()
        {
            var yawRad = _yawTarget * Deg2Rad;
            return new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
        }

        private Vector3 RightOnGround()
        {
            var yawRad = _yawTarget * Deg2Rad;
            return new Vector3(Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad));
        }

        private void SnapTargetsToPose()
        {
            _pivotTarget = _pivot;
            _yawTarget = _yaw;
            _pitchTarget = _pitch;
            _distanceTarget = _distance;
        }
    }
}
