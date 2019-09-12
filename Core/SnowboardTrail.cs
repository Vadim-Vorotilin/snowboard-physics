using System;
using UnityEngine;

namespace SnowboardPhysics.Core {

    [RequireComponent(typeof(SnowboardController))]
    public class SnowboardTrail : MonoBehaviour {

        [SerializeField] private float _trailOffset;
        [SerializeField] private float _minDistanceBetweenSegments;
        [SerializeField] private int _maxTrisCount;
        [SerializeField] private Material _trailMaterial;

        private int _pointsCount = 0;
        private int _trisCount = 0;
        private float _minDistanceBetweenSegmentsSqr;

        private class TrailPoint {
            public Vector3 Point1;
            public Vector3 Point2;
            public Vector3 Normal;
            public bool ConnectsNext;
            public bool ConnectsPrevious;
            public Vector3 DeltaPosition;

            public Vector3 Vector {
                get { return Point2 - Point1; }
            }

            public TrailPoint(Vector3 p1, Vector3 p2, Vector3 normal, bool connectsNext, bool connectsPrevious) {
                Point1 = p1;
                Point2 = p2;
                Normal = normal;
                ConnectsNext = connectsNext;
                ConnectsPrevious = connectsPrevious;
            }

            public override string ToString() {
                return string.Format("Trail point: P1: {0}, P2: {1}, N: {2}, CN: {3}, CP: {4}", Point1, Point2, Normal,
                    ConnectsNext, ConnectsPrevious);
            }
        }

        private Mesh _mesh;
        private SnowboardController _snowboard;
        private TrailPoint[] _points;

        private Vector3 _lastPos;

        void Start() {
            _points = new TrailPoint[_maxTrisCount];

            _mesh = new Mesh();
            _snowboard = gameObject.GetComponent<SnowboardController>();

            var trail = new GameObject("Trail");
            trail.transform.position = Vector3.zero;
            trail.transform.rotation = Quaternion.identity;

            trail.AddComponent<MeshRenderer>().material = _trailMaterial;
            trail.AddComponent<MeshFilter>().mesh = _mesh;

            _mesh.vertices = new Vector3[_maxTrisCount * 2];
            _mesh.uv = new Vector2[_maxTrisCount * 2];
            _mesh.normals = new Vector3[_maxTrisCount * 2];
            _mesh.colors = new Color[_maxTrisCount * 2];
            _mesh.triangles = new int[_maxTrisCount * 3];

            _lastPos = _snowboard.Model.Position;

            _minDistanceBetweenSegmentsSqr = _minDistanceBetweenSegments * _minDistanceBetweenSegments;
        }

        bool _discontinuous;

        void Update() {
            if (_trisCount > _maxTrisCount - 2) {
                ShiftMeshArrays();
            }

            Vector3 deltaPos = _snowboard.Model.Position - _lastPos;
            _lastPos = _snowboard.Model.Position;

            deltaPos = Vector3.ProjectOnPlane(deltaPos, _snowboard.Model.SlopeNormal);

            if (deltaPos.sqrMagnitude > 1e-8) {
                var point = GetCurrentPoint(deltaPos);

                if (point != null && (_pointsCount == 0 || CheckShouldBeAdded(point, _points[_pointsCount - 1]))) {
                    if (!_discontinuous && _pointsCount > 0) {
                        _points[_pointsCount - 1].ConnectsNext = true;
                        point.ConnectsPrevious = true;
                    }

                    point.DeltaPosition = deltaPos;
                    _points[_pointsCount++] = point;

                    UpdatePoint(_pointsCount - 1);

                    _discontinuous = false;
                }
            }

            _discontinuous |= !_snowboard.IsInContact;
        }

        private int _lastIndex;
        private const float AngleTolerance = 2f;

        private TrailPoint GetCurrentPoint(Vector3 deltaPos) {
            if (!_snowboard.IsInContact)
                return null;

            bool half = _snowboard.Model.CurrentContact == Contact.Half;
            Vector3[] points = new Vector3[half ? 2 : 4];

            if (_snowboard.Model.FrontHit.HasValue) {
                points[0] = Vector3.ProjectOnPlane(-_snowboard.Model.Right * _snowboard.Model.BoardWidth * 0.35f,
                                _snowboard.Model.FrontHit.Value.normal)
                            + _snowboard.Model.FrontHit.Value.point;

                points[1] = Vector3.ProjectOnPlane(_snowboard.Model.Right * _snowboard.Model.BoardWidth * 0.35f,
                                _snowboard.Model.FrontHit.Value.normal)
                            + _snowboard.Model.FrontHit.Value.point;
            }

            if (_snowboard.Model.RearHit.HasValue) {
                points[half ? 0 : 2] = Vector3.ProjectOnPlane(
                                           -_snowboard.Model.Right * _snowboard.Model.BoardWidth * 0.35f,
                                           _snowboard.Model.RearHit.Value.normal)
                                       + _snowboard.Model.RearHit.Value.point;

                points[half ? 1 : 3] = Vector3.ProjectOnPlane(
                                           _snowboard.Model.Right * _snowboard.Model.BoardWidth * 0.35f,
                                           _snowboard.Model.RearHit.Value.normal)
                                       + _snowboard.Model.RearHit.Value.point;
            }

            if (half) {
                Vector3 normal = _snowboard.Model.FrontHit.HasValue
                    ? _snowboard.Model.FrontHit.Value.normal
                    : _snowboard.Model.RearHit.Value.normal;

                return new TrailPoint(points[0], points[1], normal, false, false);
            }

            int[,] pairs = {
                {0, 1}, // Front
                {2, 3}, // Rear
                {0, 2}, // Right
                {1, 3}, // Left
                {0, 3}, // Diagonals
                {1, 2}
            };

            float angleFront = AngleTo90(points[pairs[0, 0]] - points[pairs[0, 1]], deltaPos);
            float angleRight = AngleTo90(points[pairs[2, 0]] - points[pairs[2, 1]], deltaPos);
            float angleDiag0 = AngleTo90(points[pairs[4, 0]] - points[pairs[4, 1]], deltaPos);
            float angleDiag1 = AngleTo90(points[pairs[5, 0]] - points[pairs[5, 1]], deltaPos);

            Vector3 avgNormal = (_snowboard.Model.RearHit.Value.normal + _snowboard.Model.FrontHit.Value.normal) / 2f;

            if (_lastIndex != -1) {
                if (((_lastIndex == 0 || _lastIndex == 1) && angleFront > 90 - 2 * AngleTolerance) ||
                    ((_lastIndex == 2 || _lastIndex == 3) && angleRight > 90 - 2 * AngleTolerance) ||
                    ((_lastIndex == 4 || _lastIndex == 5) &&
                     Mathf.Abs(angleDiag0 - angleDiag1) <= 2 * AngleTolerance)) {
                    Vector3 normal = Vector3.zero;

                    switch (_lastIndex) {
                        case 0:
                            normal = _snowboard.Model.FrontHit.Value.normal;
                            break;
                        case 1:
                            normal = _snowboard.Model.RearHit.Value.normal;
                            break;
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                            normal = avgNormal;
                            break;
                    }

                    return new TrailPoint(points[pairs[_lastIndex, 0]], points[pairs[_lastIndex, 1]], normal, false,
                        false);
                }
            }

            int index;

            if (angleFront > 90 - AngleTolerance)
                index = 0;
            else if (angleRight > 90 - AngleTolerance)
                index = 2;
            else {
                if (angleDiag0 > angleDiag1)
                    index = 4;
                else
                    index = 5;
            }

            _lastIndex = index;

            switch (index) {
                case 0:
                case 1:
                    bool forwardMove = Vector3.Dot(deltaPos, _snowboard.Model.Forward) >= 0;

                    if (forwardMove)
                        return new TrailPoint(points[pairs[0, 0]], points[pairs[0, 1]],
                            _snowboard.Model.FrontHit.Value.normal, false, false);
                    else
                        return new TrailPoint(points[pairs[1, 0]], points[pairs[1, 1]],
                            _snowboard.Model.RearHit.Value.normal, false, false);
                case 2:
                case 3:
                    bool rightMove = Vector3.Dot(deltaPos, _snowboard.Model.Right) >= 0;

                    if (rightMove)
                        return new TrailPoint(points[pairs[2, 0]], points[pairs[2, 1]], avgNormal, false, false);
                    else
                        return new TrailPoint(points[pairs[3, 0]], points[pairs[3, 1]], avgNormal, false, false);
                case 4:
                case 5:
                    return new TrailPoint(points[pairs[index, 0]], points[pairs[index, 1]], avgNormal, false, false);
            }

            return null;
        }

        private static float AngleTo90(Vector3 a, Vector3 b) {
            float angle = Vector3.Angle(a, b);

            if (angle > 90)
                angle = 180 - angle;

            return angle;
        }

        private bool CheckShouldBeAdded(TrailPoint newPoint, TrailPoint referencePoint) {
            Vector3 diff = (newPoint.Point2 + newPoint.Point1) / 2 -
                           (referencePoint.Point2 + referencePoint.Point1) / 2;

            if (diff.sqrMagnitude < _minDistanceBetweenSegmentsSqr)
                return false;

            if (diff.sqrMagnitude > newPoint.Vector.sqrMagnitude &&
                diff.sqrMagnitude > referencePoint.Vector.sqrMagnitude)
                return true;

            if (AngleTo90(newPoint.Point1 - newPoint.Point2, referencePoint.Point1 - referencePoint.Point2) < 15f)
                return true;

            Vector3 referencePerpendicular =
                Vector3.Cross(referencePoint.Point2 - referencePoint.Point1, referencePoint.Normal);
            Vector3 halfPlaneDirection = referencePerpendicular *
                                         Mathf.Sign(Vector3.Dot(referencePerpendicular, referencePoint.DeltaPosition));

            int c = 0;

            if (Vector3.Dot(newPoint.Point1 - referencePoint.Point1, halfPlaneDirection) > 0)
                c++;

            if (Vector3.Dot(newPoint.Point1 - referencePoint.Point2, halfPlaneDirection) > 0)
                c++;

            if (Vector3.Dot(newPoint.Point2 - referencePoint.Point1, halfPlaneDirection) > 0)
                c++;

            if (Vector3.Dot(newPoint.Point2 - referencePoint.Point2, halfPlaneDirection) > 0)
                c++;

            return c >= 3;
        }

        private void UpdatePoint(int index) {
            var point = _points[index];
            bool addTris = point.ConnectsPrevious;

            var verts = _mesh.vertices;
            var uvs = _mesh.uv;
            var cols = _mesh.colors;
            var norms = _mesh.normals;

            var normal = point.Normal.normalized;

            verts[index * 2] = point.Point1 + normal * _trailOffset;
            verts[index * 2 + 1] = point.Point2 + normal * _trailOffset;

            bool isPoint1Left = Vector3.Dot(Vector3.Cross(point.Point2 - point.Point1, normal), point.DeltaPosition) >=
                                0;

            uvs[index * 2] = new Vector2(isPoint1Left ? 0 : 1, 0);
            uvs[index * 2 + 1] = new Vector2(isPoint1Left ? 1 : 0, 0);

            Vector3 dir = Vector3.Cross(normal, point.DeltaPosition).normalized * (isPoint1Left ? 1 : -1);

            cols[index * 2] = new Color(dir.x, dir.y, dir.z, 1);
            cols[index * 2 + 1] = new Color(-dir.x, -dir.y, -dir.z, 1);

            norms[index * 2] = normal;
            norms[index * 2 + 1] = normal;

            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.colors = cols;
            _mesh.normals = norms;

            if (addTris) {
                var tris = new int[(_trisCount + 2) * 3];
                Array.Copy(_mesh.triangles, 0, tris, 0, _trisCount * 3);

                if (Vector3.Dot(point.Point1 - point.Point2, _points[index - 1].Point1 - _points[index - 1].Point2) >=
                    0) {
                    tris[_trisCount * 3] = index * 2;
                    tris[_trisCount * 3 + 1] = index * 2 - 2;
                    tris[_trisCount * 3 + 2] = index * 2 - 1;

                    tris[_trisCount * 3 + 3] = index * 2;
                    tris[_trisCount * 3 + 4] = index * 2 - 1;
                    tris[_trisCount * 3 + 5] = index * 2 + 1;
                }
                else {
                    tris[_trisCount * 3] = index * 2 + 1;
                    tris[_trisCount * 3 + 1] = index * 2 - 2;
                    tris[_trisCount * 3 + 2] = index * 2 - 1;

                    tris[_trisCount * 3 + 3] = index * 2 + 1;
                    tris[_trisCount * 3 + 4] = index * 2 - 1;
                    tris[_trisCount * 3 + 5] = index * 2;
                }

                if (Vector3.Dot(Vector3.Cross(verts[tris[_trisCount * 3]] - verts[tris[_trisCount * 3 + 1]],
                        verts[tris[_trisCount * 3]] - verts[tris[_trisCount * 3 + 2]]), normal) < 0) {
                    int t = tris[_trisCount * 3 + 1];
                    tris[_trisCount * 3 + 1] = tris[_trisCount * 3 + 2];
                    tris[_trisCount * 3 + 2] = t;
                }

                if (Vector3.Dot(Vector3.Cross(verts[tris[_trisCount * 3 + 3]] - verts[tris[_trisCount * 3 + 4]],
                        verts[tris[_trisCount * 3 + 3]] - verts[tris[_trisCount * 3 + 5]]), normal) < 0) {
                    int t = tris[_trisCount * 3 + 4];
                    tris[_trisCount * 3 + 4] = tris[_trisCount * 3 + 5];
                    tris[_trisCount * 3 + 5] = t;
                }

                _trisCount += 2;

                _mesh.triangles = tris;
            }

            _mesh.RecalculateBounds();
        }

        private void ShiftMeshArrays() {
            bool removeTwo = _points[0].ConnectsNext && _pointsCount > 1 && !_points[1].ConnectsNext;
            bool removeTris = removeTwo || _points[0].ConnectsNext;

            var newPoints = new TrailPoint[_points.Length];
            Array.Copy(_points, removeTwo ? 2 : 1, newPoints, 0, _points.Length - (removeTwo ? 2 : 1));
            _points = newPoints;

            Vector3[] verts = new Vector3[_mesh.vertices.Length];
            Vector2[] uvs = new Vector2[_mesh.uv.Length];
            Color[] cols = new Color[_mesh.colors.Length];
            Vector3[] norms = new Vector3[_mesh.normals.Length];

            int vertsToRemove = removeTwo ? 4 : 2;

            Array.Copy(_mesh.vertices, vertsToRemove, verts, 0, _mesh.vertices.Length - vertsToRemove);
            Array.Copy(_mesh.uv, vertsToRemove, uvs, 0, _mesh.uv.Length - vertsToRemove);
            Array.Copy(_mesh.colors, vertsToRemove, cols, 0, _mesh.colors.Length - vertsToRemove);
            Array.Copy(_mesh.normals, vertsToRemove, norms, 0, _mesh.normals.Length - vertsToRemove);

            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.colors = cols;
            _mesh.normals = norms;

            var tris = _mesh.triangles;

            for (int i = 0; i < tris.Length; i++) {
                tris[i] -= vertsToRemove;

                if (tris[i] < 0)
                    tris[i] = 0;
            }

            if (removeTris) {
                _trisCount -= 2;

                var newTris = new int[_trisCount * 3];
                Array.Copy(tris, 6, newTris, 0, tris.Length - 6);

                tris = newTris;
            }

            _mesh.triangles = tris;

            _pointsCount -= removeTwo ? 2 : 1;
            _points[0].ConnectsPrevious = false;
        }

    }
}