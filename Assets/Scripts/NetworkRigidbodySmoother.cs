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
        /// <summary>
        /// Position before simulation is performed.
        /// </summary>
        private Vector3 _prePosition;
        /// <summary>
        /// Rotation before simulation is performed.
        /// </summary>
        private Quaternion _preRotation;
        /// <summary>
        /// Position after simulation is performed.
        /// </summary>
        private Vector3 _postPosition;
        /// <summary>
        /// Rotation after simulation is performed.
        /// </summary>
        private Quaternion _postRotation;
        /// <summary>
        /// Time passed since last fixed frame.
        /// </summary>
        private float _frameTimePassed = 0f;
        #endregion

        #region Const.
        /// <summary>
        /// Multiplier to apply towards delta time.
        /// </summary>
        private const float FRAME_TIME_MULTIPLIER = 0.75f;
        #endregion

        private void Awake()
        {
            Identity.OnAuthorityChanged.AddListener(OnAuthorityChanged);
        }

        private void OnAuthorityChanged(bool hasAuthority)
        {
            if(hasAuthority)
                SubscribeToFixedUpdateManager(true);
            else
                SubscribeToFixedUpdateManager(false);
        }

        private void OnEnable()
        {
            if (HasAuthority)
                SubscribeToFixedUpdateManager(true);
        }

        private void OnDisable()
        {
            SubscribeToFixedUpdateManager(false);
        }

        private void Update()
        {
            if (HasAuthority)
                Smooth();
        }

        /// <summary>
        /// Smooths position and rotation to zero values.
        /// </summary>
        private void Smooth()
        {
            _frameTimePassed += (Time.deltaTime * FRAME_TIME_MULTIPLIER);
            float percent = Mathf.InverseLerp(0f, FixedUpdateManager.AdjustedFixedDeltaTime, _frameTimePassed);

            transform.position = Vector3.Lerp(_prePosition, transform.position, percent);
            transform.rotation = Quaternion.Lerp(_preRotation, transform.rotation, percent);
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
            _frameTimePassed = 0f;
            transform.position = _prePosition;
            transform.rotation = _preRotation;

            _postPosition = transform.position;
            _postRotation = transform.rotation;
        }

        private void FixedUpdateManager_OnPreFixedUpdate()
        {
            _prePosition = transform.position;
            _preRotation = transform.rotation;
        }
    }
}