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
            FixedUpdateManager.OnFixedUpdate += FixedUpdateManager_OnFixedUpdate;
            Identity.OnStartServer.AddListener(OnStartServer);
        }

        private void OnStartServer()
        {

        }

        /// <summary>
        /// Initialize this script for use. Should only be called once.
        /// </summary>
        private void NetworkFirstInitialize()
        {

        }

        private void FixedUpdateManager_OnFixedUpdate()
        {
            throw new System.NotImplementedException();
        }

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}