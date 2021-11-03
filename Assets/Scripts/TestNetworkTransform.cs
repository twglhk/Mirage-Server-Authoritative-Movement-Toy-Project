// vis2k:
// base class for NetworkTransform and NetworkTransformChild.
// New method is simple and stupid. No more 1500 lines of code.
//
// Server sends current data.
// Client saves it and interpolates last and latest data points.
//   Update handles transform movement / rotation
//   FixedUpdate handles rigidbody movement / rotation
//
// Notes:
// * Built-in Teleport detection in case of lags / teleport / obstacles
// * Quaternion > EulerAngles because gimbal lock and Quaternion.Slerp
// * Syncs XYZ. Works 3D and 2D. Saving 4 bytes isn't worth 1000 lines of code.
// * Initial delay might happen if server sends packet immediately after moving
//   just 1cm, hence we move 1cm and then wait 100ms for next packet
// * Only way for smooth movement is to use a fixed movement speed during
//   interpolation. interpolation over time is never that good.
//
using Mirage.Serialization;
using UnityEngine;
using Mirage;
using System.Collections.Generic;

namespace WardGames.John.AuthoritativeMovement
{
    public class TestNetworkTransform : NetworkBehaviour
    {
        [Header("Authority")]
        [Tooltip("Set to true if moves come from owner client, set to false if moves always come from server")]
        public bool ClientAuthority;

        // Is this a client with authority over this transform?
        // This component could be on the player object or any object that has been assigned authority to this client.
        bool IsClientWithAuthority => HasAuthority && ClientAuthority;

        // Sensitivity is added for VR where human players tend to have micro movements so this can quiet down
        // the network traffic.  Additionally, rigidbody drift should send less traffic, e.g very slow sliding / rolling.
        [Header("Sensitivity")]
        [Tooltip("Changes to the transform must exceed these values to be transmitted on the network.")]
        public float LocalPositionSensitivity = .01f;
        [Tooltip("If rotation exceeds this angle, it will be transmitted on the network")]
        public float LocalRotationSensitivity = .01f;
        [Tooltip("Changes to the transform must exceed these values to be transmitted on the network.")]
        public float LocalScaleSensitivity = .01f;

        // target transform to sync. can be on a child.
        protected Transform TargetComponent { get; }

        // server
        Vector3 lastPosition;
        Quaternion lastRotation;
        Vector3 lastScale;

        // client
        public class DataPoint
        {
            public float TimeStamp;
            // use local position/rotation for VR support
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
            public float MovementSpeed;
        }
        // interpolation start and goal
        DataPoint start;
        DataPoint goal;

        // local authority send time
        float lastClientSendTime;

        // serialization is needed by OnSerialize and by manual sending from authority
        // public only for tests
        public static void SerializeIntoWriter(NetworkWriter writer, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // serialize position, rotation, scale
            // note: we do NOT compress rotation.
            //       we are CPU constrained, not bandwidth constrained.
            //       the code needs to WORK for the next 5-10 years of development.
            writer.WriteVector3(position);
            writer.WriteQuaternion(rotation);
            writer.WriteVector3(scale);
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            /*
             *  SetDirtyBit을 할당한 다음에 네트워킹 직전에 호출되는 콜백 메서드
             *  여기서 데이터를 Wirter 클래스로 패킹합니다.
             *  그 다음 동기화할 이 오브젝트의 데이터를 모든 클라이언트에게 전송
             */

            // use local position/rotation/scale for VR support
            SerializeIntoWriter(writer, TargetComponent.localPosition, TargetComponent.localRotation, TargetComponent.localScale);
            return true;
        }

        // try to estimate movement speed for a data point based on how far it
        // moved since the previous one
        // => if this is the first time ever then we use our best guess:
        //    -> delta based on transform.localPosition
        //    -> elapsed based on send interval hoping that it roughly matches
        static float EstimateMovementSpeed(DataPoint from, DataPoint to, Transform transform, float sendInterval)
        {
            /*
             *  from은 마지막으로 갱신했던 위치 데이터
             *  to는 서버에서 받은 데이터 
             */

            // delta는 from, 즉 마지막으로 갱신했던 위치 데이터의 존재 여부에 따라서 해당 데이터로 할 지, 현재 transform의 localPosition으로 할지 결정 후 새롭게 받은 데이터로 향하는 Vector를 계산.
            Vector3 delta = to.LocalPosition - (from != null ? from.LocalPosition : transform.localPosition);

            // elapsed는 from과 to의 TimeStamp를 비교해서 두 데이터의 시간차를 계산. 만약 없었다면 sendInterval로 대체
            float elapsed = from != null ? to.TimeStamp - from.TimeStamp : sendInterval;
            
            // 최종적으로 두 데이터의 시간 차가 존재한다면, 특정 시간 대에서 두 점 사이의 특정 위치를 리턴(속도)
            // avoid NaN
            return elapsed > 0 ? delta.magnitude / elapsed : 0;
        }

        // serialization is needed by OnSerialize and by manual sending from authority
        void DeserializeFromReader(NetworkReader reader)
        {
            // temp는 서버에서 받은 새로운 위치 정보
            // put it into a data point immediately
            var temp = new DataPoint
            {
                // deserialize position
                LocalPosition = reader.ReadVector3()
            };

            // deserialize rotation & scale
            temp.LocalRotation = reader.ReadQuaternion();
            temp.LocalScale = reader.ReadVector3();

            temp.TimeStamp = Time.time;

            // movement speed: based on how far it moved since last time
            // has to be calculated before 'start' is overwritten
            temp.MovementSpeed = EstimateMovementSpeed(goal, temp, TargetComponent, syncInterval);

            // reassign start wisely
            // -> first ever data point? then make something up for previous one
            //    so that we can start interpolation without waiting for next.
            if (start == null)  // 첫 Data Point 할당이면 interpolation에 사용될 위치를 다음 데이터를 기다리지 않고 바로 업데이트.
            {
                start = new DataPoint
                {
                    TimeStamp = Time.time - syncInterval,
                    // local position/rotation for VR support
                    LocalPosition = TargetComponent.localPosition,
                    LocalRotation = TargetComponent.localRotation,
                    LocalScale = TargetComponent.localScale,
                    MovementSpeed = temp.MovementSpeed
                };
            }
            // -> second or nth data point? then update previous, but:
            //    we start at where ever we are right now, so that it's
            //    perfectly smooth and we don't jump anywhere
            //
            //    example if we are at 'x':
            //
            //        A--x->B
            //
            //    and then receive a new point C:
            //
            //        A--x--B
            //              |
            //              |
            //              C
            //
            //    then we don't want to just jump to B and start interpolation:
            //
            //              x
            //              |
            //              |
            //              C
            //
            //    we stay at 'x' and interpolate from there to C:
            //
            //           x..B
            //            \ .
            //             \.
            //              C
            //
            else
            {
                float oldDistance = Vector3.Distance(start.LocalPosition, goal.LocalPosition);  // 이전에 저장된 시작 지점과 도착 지점 사이의 거리 계산
                float newDistance = Vector3.Distance(goal.LocalPosition, temp.LocalPosition);   // 이전에 저장된 도착 지점과 서버에서 새로 받은 위치 사이의 거리 계산

                start = goal; // 시작 지점을 이번 도착지점으로 갱신

                // teleport / lag / obstacle detection: only continue at current
                // position if we aren't too far away
                // 이거를 하는 이유는 현재 타깃의 위치가 Start로부터 너무 멀리 떨어져 있지만 않으면, interpolation 자체를 현재 위치부터 해주겠다는 의미. 너무 멀리 떨어져 있으면 현재 시작 위치로 텔포 이후에 inter.
                // local position/rotation for VR support
                //
                if (Vector3.Distance(TargetComponent.localPosition, start.LocalPosition) < oldDistance + newDistance)
                {
                    // 시작 지점을 현재 위치로 갱신
                    start.LocalPosition = TargetComponent.localPosition;
                    start.LocalRotation = TargetComponent.localRotation;
                    start.LocalScale = TargetComponent.localScale;
                }
            }

            // set new destination in any case. new data is best data.
            // 도착 지점을 이제는 서버에서 받은 지점으로 갱신
            goal = temp;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            /*
             *  클라이언트가 서버로부터 이 오브젝트와 관련된 동기화용 패킷을 받은 다음 해당 부분 Deserialize.
             *  DeserializeFormReader 호출
             */

            // deserialize
            DeserializeFromReader(reader);
        }

        // local authority client sends sync message to server for broadcasting
        [ServerRpc]
        void CmdClientToServerSync(byte[] payload)
        {
            // Ignore messages from client if not in client authority mode
            if (!ClientAuthority)
                return;

            // deserialize payload
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(payload))
                DeserializeFromReader(networkReader);

            // server-only mode does no interpolation to save computations,
            // but let's set the position directly
            if (IsServer && !IsClient)
                ApplyPositionRotationScale(goal.LocalPosition, goal.LocalRotation, goal.LocalScale);

            // set dirty so that OnSerialize broadcasts it
            SetDirtyBit(1UL);
        }

        // where are we in the timeline between start and goal? [0,1]
        static float CurrentInterpolationFactor(DataPoint start, DataPoint goal)
        {
            if (start != null)
            {
                float difference = goal.TimeStamp - start.TimeStamp;

                // the moment we get 'goal', 'start' is supposed to
                // start, so elapsed time is based on:
                float elapsed = Time.time - goal.TimeStamp;
                // avoid NaN
                return difference > 0 ? elapsed / difference : 0;
            }
            return 0;
        }

        static Vector3 InterpolatePosition(DataPoint start, DataPoint goal, Vector3 currentPosition)
        {
            if (start != null)
            {
                // Option 1: simply interpolate based on time. but stutter
                // will happen, it's not that smooth. especially noticeable if
                // the camera automatically follows the player
                //   float t = CurrentInterpolationFactor();
                //   return Vector3.Lerp(start.position, goal.position, t);

                // Option 2: always += speed
                // -> speed is 0 if we just started after idle, so always use max
                //    for best results
                float speed = Mathf.Max(start.MovementSpeed, goal.MovementSpeed);
                return Vector3.MoveTowards(currentPosition, goal.LocalPosition, speed * Time.deltaTime);
            }
            return currentPosition;
        }

        static Quaternion InterpolateRotation(DataPoint start, DataPoint goal, Quaternion defaultRotation)
        {
            if (start != null)
            {
                float t = CurrentInterpolationFactor(start, goal);
                return Quaternion.Slerp(start.LocalRotation, goal.LocalRotation, t);
            }
            return defaultRotation;
        }

        static Vector3 InterpolateScale(DataPoint start, DataPoint goal, Vector3 currentScale)
        {
            if (start != null)
            {
                float t = CurrentInterpolationFactor(start, goal);
                return Vector3.Lerp(start.LocalScale, goal.LocalScale, t);
            }
            return currentScale;
        }

        // teleport / lag / stuck detection
        // -> checking distance is not enough since there could be just a tiny
        //    fence between us and the goal
        // -> checking time always works, this way we just teleport if we still
        //    didn't reach the goal after too much time has elapsed
        bool NeedsTeleport()
        {
            // calculate time between the two data points
            float startTime = start != null ? start.TimeStamp : Time.time - syncInterval;   // 이전에 받았던 데이터의 TimeStamp이기 때문에 syncInterval 만큼 빼줌. 아니면 저장된 TimeStamp 사용
            float goalTime = goal != null ? goal.TimeStamp : Time.time;
            float difference = goalTime - startTime;
            float timeSinceGoalReceived = Time.time - goalTime;
            return timeSinceGoalReceived > difference * 5;
        }

        // moved since last time we checked it?
        bool HasEitherMovedRotatedScaled()
        {
            /*
             * 이 부분은 서버 or 권한 클라에서 호출이 되는 부분
             * 즉 클라에서 인풋을 받은 다음에, 서버에서 이 오브젝트의 Transform 정보가 마지막에 저장했던 정보와 비교해서 Sensitivity 이상으로 바뀌었다면 체크한 다음에 SetDirtyBit을 해줘서
             * 다음 프레임 때 이 데이터들(position, rotation, scale)을 네트워크를 통해 동기화할 수 있도록 Serialize 합니다.
             */

            // moved or rotated or scaled?
            // local position/rotation/scale for VR support
            bool moved = Vector3.Distance(lastPosition, TargetComponent.localPosition) > LocalPositionSensitivity;
            bool scaled = Vector3.Distance(lastScale, TargetComponent.localScale) > LocalScaleSensitivity;
            bool rotated = Quaternion.Angle(lastRotation, TargetComponent.localRotation) > LocalRotationSensitivity;

            // save last for next frame to compare
            // (only if change was detected. otherwise slow moving objects might
            //  never sync because of C#'s float comparison tolerance. see also:
            //  https://github.com/vis2k/Mirror/pull/428)
            bool change = moved || rotated || scaled;
            if (change)
            {
                // local position/rotation for VR support
                // 변경이 되었다면 last transform 데이터 갱신
                lastPosition = TargetComponent.localPosition;
                lastRotation = TargetComponent.localRotation;
                lastScale = TargetComponent.localScale;
            }

            // 하나라도 변경이 됬다면 true, 아니면 false
            return change;
        }

        // set position carefully depending on the target component
        void ApplyPositionRotationScale(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // local position/rotation for VR support
            TargetComponent.localPosition = position;
            TargetComponent.localRotation = rotation;
            TargetComponent.localScale = scale;
        }

        void Update()
        {
            // if server then always sync to others.
            if (IsServer)
            {
                UpdateServer();
            }

            // no 'else if' since host mode would be both
            if (IsClient)
            {
                UpdateClient();
            }
        }

        void UpdateServer()
        {
            // just use OnSerialize via SetDirtyBit only sync when position
            // changed. set dirty bits 0 or 1
            SetDirtyBit(HasEitherMovedRotatedScaled() ? 1UL : 0UL);
        }

        void UpdateClient()
        {
            // send to server if we have local authority (and aren't the server)
            // -> only if connectionToServer has been initialized yet too
            // check only each 'syncInterval'
            if (!IsServer && IsClientWithAuthority && Time.time - lastClientSendTime >= syncInterval)
            {
                if (HasEitherMovedRotatedScaled())
                {
                    // serialize
                    // local position/rotation for VR support
                    using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                    {
                        SerializeIntoWriter(writer, TargetComponent.localPosition, TargetComponent.localRotation, TargetComponent.localScale);

                        // send to server
                        CmdClientToServerSync(writer.ToArray());
                    }
                }
                lastClientSendTime = Time.time;
            }

            // 각 클라이언트가 서버에서 받은 해당 오브젝트의 데이터를 토대로 interpolation 하는 부분
            // apply interpolation on client for all players
            // unless this client has authority over the object. could be
            // himself or another object that he was assigned authority over
            if (!IsClientWithAuthority && goal != null)
            {
                // teleport or interpolate
                if (NeedsTeleport())
                {
                    // local position/rotation for VR support
                    ApplyPositionRotationScale(goal.LocalPosition, goal.LocalRotation, goal.LocalScale);

                    // reset data points so we don't keep interpolating
                    start = null;
                    goal = null;
                }
                else
                {
                    // local position/rotation for VR support
                    ApplyPositionRotationScale(InterpolatePosition(start, goal, TargetComponent.localPosition),
                                                InterpolateRotation(start, goal, TargetComponent.localRotation),
                                                InterpolateScale(start, goal, TargetComponent.localScale));
                }
            }
        }

        static void DrawDataPointGizmo(DataPoint data, Color color)
        {
            // use a little offset because transform.localPosition might be in
            // the ground in many cases
            Vector3 offset = Vector3.up * 0.01f;

            // draw position
            Gizmos.color = color;
            Gizmos.DrawSphere(data.LocalPosition + offset, 0.5f);

            // draw forward and up
            // like unity move tool
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(data.LocalPosition + offset, data.LocalRotation * Vector3.forward);

            // like unity move tool
            Gizmos.color = Color.green;
            Gizmos.DrawRay(data.LocalPosition + offset, data.LocalRotation * Vector3.up);
        }

        static void DrawLineBetweenDataPoints(DataPoint data1, DataPoint data2, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(data1.LocalPosition, data2.LocalPosition);
        }

        // draw the data points for easier debugging
        void OnDrawGizmos()
        {
            // draw start and goal points
            if (start != null) DrawDataPointGizmo(start, Color.gray);
            if (goal != null) DrawDataPointGizmo(goal, Color.white);

            // draw line between them
            if (start != null && goal != null) DrawLineBetweenDataPoints(start, goal, Color.cyan);
        }
    }
}
