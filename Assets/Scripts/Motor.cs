using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WardGames.John.AuthoritativeMovement.Manager;
using Mirage;
using WardGames.John.AuthoritativeMovement.States;

namespace WardGames.John.AuthoritativeMovement.Motors
{
    public class Motor : NetworkBehaviour
    {
        #region Serialized
        /// <summary>
        /// Move rate for the rigidbody.
        /// </summary>
        [Tooltip("Move rate for the rigidbody.")]
        [SerializeField]
        private float _moveRate = 3f;
        #endregion

        #region Private
        /// <summary>
        /// Rigidbody on this object
        /// </summary>
        private Rigidbody _rigidbody = null;

        /// <summary>
        /// Stored client motor states.
        /// </summary>
        private List<ClientMotorState> _clientMotorState = new List<ClientMotorState>();

        /// <summary>
        /// Most current motor state received from the client.
        /// </summary>
        private ClientMotorState _receivedClientMotorState;
        #endregion

        private void Awake()
        {
            FirstInitialize();
            Identity.OnStartServer.AddListener(OnStartServer);
        }

        private void OnStartServer()
        {

        }

        /// <summary>
        /// Initialize this script for use. Should only be called once.
        /// </summary>
        private void FirstInitialize()
        {
            _rigidbody = GetComponent<Rigidbody>();
            FixedUpdateManager.OnFixedUpdate += FixedUpdateManager_OnFixedUpdate;
        }

        /// <summary>
        /// Initialize this script for use. Should only be called once.
        /// </summary>
        private void NetworkFirstInitialize()
        {

        }

        /// <summary>
        /// Received when a simulated fixed update occurs.
        /// </summary>
        private void FixedUpdateManager_OnFixedUpdate()
        {
            // Prevent a object that the client don't has authority to run physics simulation.
            if (!Identity.HasAuthority && !IsServer)
            {
                CancelVelocity(false);
            }

            if (Identity.HasAuthority)
            {
                ClientSendInputs();
            }
        }

        /// <summary>
        /// Cancels velocity on the rigidbody.
        /// </summary>
        /// <param name="useForces"></param>
        [Client]
        private void CancelVelocity(bool useForces)
        {
            if (useForces)
            {
                _rigidbody.AddForce(-_rigidbody.velocity, ForceMode.Impulse);
                _rigidbody.AddTorque(-_rigidbody.angularVelocity, ForceMode.Impulse);
            }
            else
            {
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Sends inputs for the client.
        /// </summary>
        [Client]
        private void ClientSendInputs()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float forward = Input.GetAxisRaw("Vertical");

            ClientMotorState state = new ClientMotorState
            {
                FixedFrame = FixedUpdateManager.FixedFrame,
                Horizontal = horizontal,
                Forward = forward,
                ActionCodes = 0
            };
            _clientMotorState.Add(state);

            ProcessInputs(state);
            ServerRpcSendInputs(state);
        }

        /// <summary>
        /// Send inputs from client to server.
        /// </summary>
        /// <param name="motorState"></param>
        [ServerRpc]
        private void ServerRpcSendInputs(ClientMotorState motorState)
        {
            // If state received is older than last received state then ignore it.
            if (motorState.FixedFrame < _receivedClientMotorState.FixedFrame)
                return;

            _receivedClientMotorState = motorState;
        }

        /// <summary>
        /// Processes input from a state.
        /// </summary>
        /// <param name="motorState"></param>
        private void ProcessInputs(ClientMotorState motorState)
        {
            motorState.Horizontal = PreciseSign(motorState.Horizontal);
            motorState.Forward = PreciseSign(motorState.Forward);

            Vector3 forces = new Vector3(motorState.Horizontal, 0f, motorState.Forward) * _moveRate;
            _rigidbody.AddForce(forces, ForceMode.Impulse);
        }

        /// <summary>
        /// Returns negative-one, zero, or positive-one of a value instead of just negative-one or positive one.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static float PreciseSign(float value)
        {
            if (value == 0f)
                return 0f;
            else
                return (Mathf.Sign(value));
        }
    }
}