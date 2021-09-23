using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirage;
using WardGames.John.AuthoritativeMovement.Manager;

namespace WardGames.John.AuthoritativeMovement.Motors
{
    public class NetworkRigidbodySmoother : NetworkBehaviour
    {
        #region Private.
        /// <summary>
        /// True if subscribed to FixedUpdateManager events.
        /// </summary>
        private bool _subscribed = false;
        #endregion

        private void Awake()
        {
            Identity.OnAuthorityChanged.AddListener(OnAuthorityChanged);
        }

        private void OnAuthorityChanged(bool hasAuthority)
        {

        }

        private void OnDisable()
        {
            
        }

        /// <summary>
        /// Changes event subscriptions on the FixedUpdateManager.
        /// </summary>
        /// <param name="subscribe"></param>
        private void SubscribeToFixedUpdateManager(bool subscribe)
        {
            if (subscribe == _subscribed)
                return;

            if (subscribe)
            {
                FixedUpdateManager.OnPreFixedUpdate += FixedUpdateManager_OnPreFixedUpdate;
                FixedUpdateManager.OnPostFixedUpdate += FixedUpdateManager_OnPostFixedUpdate;
            }
            else
            {
                FixedUpdateManager.OnPreFixedUpdate -= FixedUpdateManager_OnPreFixedUpdate;
                FixedUpdateManager.OnPostFixedUpdate -= FixedUpdateManager_OnPostFixedUpdate;
            }
        }

        private void FixedUpdateManager_OnPostFixedUpdate()
        {
            
        }

        private void FixedUpdateManager_OnPreFixedUpdate()
        {
            
        }
    }
}