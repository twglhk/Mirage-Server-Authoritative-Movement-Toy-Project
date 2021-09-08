using UnityEngine;

namespace WardGames.John.AuthoritativeMovement.States
{
    public struct ClientMotorState
    {
        public uint FixedFrame;
        public float Horizontal;
        public float Forward;
        public byte ActionCodes;
    }
}