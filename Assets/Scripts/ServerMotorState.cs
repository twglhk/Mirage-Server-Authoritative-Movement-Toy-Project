using UnityEngine;

namespace WardGames.John.AuthoritativeMovement.States
{
    public struct ServerMotorState
    {
        public uint FixedFrame;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public sbyte TimingStepChange;
    }
}