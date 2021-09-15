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
        private List<ClientMotorState> _clientMotorStates = new List<ClientMotorState>();

        /// <summary>
        /// Most current motor state received from the client.
        /// </summary>
        private ClientMotorState? _receivedClientMotorState = null;

        /// <summary>
        /// Most current moto state received from the Server.
        /// </summary>
        private ServerMotorState? _receivedServerMototState = null;

        /// <summary>
        /// Number of predictions left that the server may use.
        /// </summary>
        private byte _remainingClientStatePredictions = 0;
        #endregion

        #region Const
        /// <summary>
        /// Maximum times the server can predict the client state.
        /// </summary>
        private const byte MAXIMUM_CLIENT_STATE_PREDICTIONS = 20;
        #endregion

        private void Awake()
        {
            FirstInitialize();
            Identity.OnStartServer.AddListener(OnStartServer);
        }

        private void OnStartServer()
        {

        }

        private void OnEnable()
        {
            FixedUpdateManager.OnFixedUpdate += FixedUpdateManager_OnFixedUpdate;
        }

        private void OnDisable()
        {
            FixedUpdateManager.OnFixedUpdate -= FixedUpdateManager_OnFixedUpdate;
        }

        /// <summary>
        /// Initialize this script for use. Should only be called once.
        /// </summary>
        private void FirstInitialize()
        {
            _rigidbody = GetComponent<Rigidbody>();
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
                ProcessReceivedServerMotorState();  // Client Rollback stage
                ClientSendInputs();
            }

            if (IsServer)
            {
                ProcessReceivedClientMotorState();
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
        /// Processes the last received server motor state.
        /// </summary>
        [Client]
        private void ProcessReceivedServerMotorState()
        {
            if (_receivedServerMototState == null)
                return;

            ServerMotorState serverState = _receivedServerMototState.Value;
            _receivedServerMototState = null;

            // Remove entries which have been handled by the server
            int index = _clientMotorStates.FindIndex(x => x.FixedFrame == serverState.FixedFrame);
            if (index != -1)
                _clientMotorStates.RemoveRange(0, index + 1);

            // Snap motor to server values.
            transform.position = serverState.Position;
            transform.rotation = serverState.Rotation;
            _rigidbody.velocity = serverState.Velocity;
            _rigidbody.angularVelocity = serverState.AngularVelocity;

            // If this don't be called, it won't actually put transform at that position until the next fixed frame.
            Physics.SyncTransforms();

            foreach (ClientMotorState clientState in _clientMotorStates)
            {
                ProcessInputs(clientState);

                // Simulates every rollback
                Physics.Simulate(Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Processes the last received client motor state.
        /// </summary>
        [Server]
        private void ProcessReceivedClientMotorState()
        {
            if (_receivedClientMotorState == null || _remainingClientStatePredictions == 0)
                return;

            // True if this is the first time this input is being run.
            // To send predicted input to clients only new received input helps saving bandwidth from server to client 
            bool newInput = (_remainingClientStatePredictions == MAXIMUM_CLIENT_STATE_PREDICTIONS);
            // Process input of last received motor state.
            ProcessInputs(_receivedClientMotorState.Value);
            // Remove from prediction count.
            _remainingClientStatePredictions--;

            if (newInput)
            {
                ServerMotorState responseState = new ServerMotorState
                {
                    FixedFrame = _receivedClientMotorState.Value.FixedFrame,
                    Position = transform.position,
                    Rotation = transform.rotation,
                    Velocity = _rigidbody.velocity,
                    AngularVelocity = _rigidbody.angularVelocity
                };

                // Send results back to client.
                TargetServerStateUpdate(responseState);
            }
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
            _rigidbody.AddForce(forces, ForceMode.Acceleration);
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
            _clientMotorStates.Add(state);

            /* Only send inputs if client only
             * since sending inputs here otherwise would
             * result in them running both on clientg and 
             * server. This would result in inputs running
             * twice in one frame.
             */
            if (IsClientOnly)
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
            if (_receivedClientMotorState != null && motorState.FixedFrame < _receivedClientMotorState.Value.FixedFrame)
                return;

            _remainingClientStatePredictions = MAXIMUM_CLIENT_STATE_PREDICTIONS;
            _receivedClientMotorState = motorState;
        }

        /// <summary>
        /// Received on the owning client after the server processes ClientMotorState.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="motorState"></param>
        [ClientRpc(target = RpcTarget.Owner)]
        private void TargetServerStateUpdate(ServerMotorState motorState)
        {
            // Exit if received state is older than most current.
            // This situation will be occur when it use unreliable transport.
            if (_receivedServerMototState != null && motorState.FixedFrame < _receivedServerMototState.Value.FixedFrame)
                return;

            _receivedServerMototState = motorState;

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