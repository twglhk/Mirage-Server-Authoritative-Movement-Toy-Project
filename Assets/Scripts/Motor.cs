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
        #region Types.
        /// <summary>
        /// Inputs which can be stored.
        /// </summary>
        private class InputData
        {
            public bool Jump = false;
        }
        #endregion

        #region Serialized.
        /// <summary>
        /// Move rate for the rigidbody.
        /// </summary>
        [Tooltip("Move rate for the rigidbody.")]
        [SerializeField]
        private float _moveRate = 3f;
        [Tooltip("How much force to apply as impulse when jump")]
        [SerializeField]
        private float _jumpImpulse = 8f;
        #endregion

        #region Private.
        /// <summary>
        /// Rigidbody on this object
        /// </summary>
        private Rigidbody _rigidbody = null;

        /// <summary>
        /// Stored client motor states.
        /// </summary>
        private List<ClientMotorState> _clientMotorStates = new List<ClientMotorState>();

        /// <summary>
        /// Motor states received from the client.
        /// </summary>
        private List<ClientMotorState> _receivedClientMotorStates = new List<ClientMotorState>();

        /// <summary>
        /// Most current moto state received from the Server.
        /// </summary>
        private ServerMotorState? _receivedServerMototState = null;

        /// <summary>
        /// Inputs stored from Update.
        /// </summary>
        private InputData _storedInputs = new InputData();
        #endregion

        #region Const.
        /// <summary>
        /// Maximum number of entries that may be held within ReceivedClientMotorStates.
        /// </summary>
        private const int MAXIMUM_RECEIVED_CLIENT_MOTOR_STATES = 10;
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

        private void Update()
        {
            if (HasAuthority)
            {
                CheckJump();
            }
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
        /// Checks if jump is pressed.
        /// </summary>
        [Client]
        private void CheckJump()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _storedInputs.Jump = true;
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
            FixedUpdateManager.AddTiming(serverState.TimingStepChange);
            _receivedServerMototState = null;

            // Remove entries which have been handled by the server
            int index = _clientMotorStates.FindIndex(x => x.FixedFrame == serverState.FixedFrame);
            if (index != -1)
                _clientMotorStates.RemoveRange(0, index);

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
            //For Host client test
            if (IsClient && HasAuthority)
                return;

            sbyte timingStepChange = 0;

            /* If there are no states then set timing change step
             * to a negative value, which will speed up the client 
             * simulation. In result this will increase the chances
             * the client will send a packet which will arrive by every
             * fixed on the server. */
            if (_receivedClientMotorStates.Count == 0)
                timingStepChange = -1;

            /* Like subtracting a step, if there is more than one entry
             * then the client is sending too fast. Send a positive step
             * which will slow the clients send rate. */
            else if (_receivedClientMotorStates.Count > 1)
                timingStepChange = 1;

            //If there is input to process.
            if (_receivedClientMotorStates.Count > 0)
            {
                //Assume using reliable trasport. (Packet will arrive in oreder)
                ClientMotorState state = _receivedClientMotorStates[0];
                _receivedClientMotorStates.RemoveAt(0);

                //Process input of last received motor state.
                ProcessInputs(state);

                ServerMotorState responseState = new ServerMotorState
                {
                    FixedFrame = state.FixedFrame,
                    Position = transform.position,
                    Rotation = transform.rotation,
                    Velocity = _rigidbody.velocity,
                    AngularVelocity = _rigidbody.angularVelocity,
                    TimingStepChange = timingStepChange
                };

                //Send results back to owner.
                TargetServerStateUpdate(responseState);
            }

            //If there is no input to process.
            else if (timingStepChange != 0)
            {
                //Send timing step change to owner.
                TargetChangeTimingStep(timingStepChange);
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

            Vector3 impulses = Vector3.zero;
            if ((ActionCodes)motorState.ActionCodes == ActionCodes.Jump)
                impulses += (Vector3.up * _jumpImpulse);
            _rigidbody.AddForce(impulses, ForceMode.Impulse);
        }

        /// <summary>
        /// Sends inputs for the client.
        /// </summary>
        [Client]
        private void ClientSendInputs()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float forward = Input.GetAxisRaw("Vertical");

            /* Action Codes. */
            ActionCodes ac = ActionCodes.None;
            if (_storedInputs.Jump)
            {
                _storedInputs.Jump = false;
                ac |= ActionCodes.Jump;
            }

            ClientMotorState state = new ClientMotorState
            {
                FixedFrame = FixedUpdateManager.FixedFrame,
                Horizontal = horizontal,
                Forward = forward,
                ActionCodes = (byte)ac
            };
            _clientMotorStates.Add(state);

            
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
            //For Host client test
            if (IsClient && HasAuthority)
                return;

            _receivedClientMotorStates.Add(motorState);
            if (_receivedClientMotorStates.Count > MAXIMUM_RECEIVED_CLIENT_MOTOR_STATES)
                _receivedClientMotorStates.RemoveAt(0);
            
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
        /// Received on the owning client after server fails to process any inputs.
        /// </summary>
        /// <param name="steps"></param>
        [ClientRpc(target = RpcTarget.Owner)]
        private void TargetChangeTimingStep(sbyte steps)
        {
            FixedUpdateManager.AddTiming(steps);
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