using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WardGames.John.AuthoritativeMovement.Manager;
using Mirage;

namespace WardGames.John.AuthoritativeMovement.Motors
{
    public class Motor : NetworkBehaviour
    {
        #region Private
        /// <summary>
        /// Rigidbody on this object
        /// </summary>
        private Rigidbody _rigidbody;
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

        }

        private void ProcessInputs()
        {

        }
    }
}