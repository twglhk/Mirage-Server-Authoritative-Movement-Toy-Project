using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirage;
using WardGames.John.AuthoritativeMovement.Manager;

namespace WardGames.John.AuthoritativeMovement.Motors
{
    public class NetworkRigidbodySmoother : NetworkBehaviour
    {
        #region Serialized.
        [Tooltip("How quickly to smooth to zero")]
        [SerializeField]
        private float _smoothRate = 30f;
        #endregion

        #region Private.
        /// <summary>
        /// True if subscribed to FixedUpdateManager events.
        /// </summary>
        private bool _subscribed = false;
        /// <summary>
        /// Position before simulation is performed.
        /// </summary>
        private Vector3 _position;
        /// <summary>
        /// Rotation before simulation is performed.
        /// </summary>
        private Quaternion _rotation;
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
            float distance;
            distance = Mathf.Max(0.01f, Vector3.Distance(transform.localPosition, Vector3.zero));
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, Vector3.zero, distance * _smoothRate * Time.deltaTime);
            distance = Mathf.Max(1f, Quaternion.Angle(transform.localRotation, Quaternion.identity));
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.identity, distance * _smoothRate * Time.deltaTime);


            //_frameTimePassed += (Time.deltaTime * FRAME_TIME_MULTIPLIER);
            //float percent = Mathf.InverseLerp(0f, FixedUpdateManager.AdjustedFixedDeltaTime, _frameTimePassed);

            //transform.position = Vector3.Lerp(_position, transform.position, percent);
            //transform.rotation = Quaternion.Lerp(_rotation, transform.rotation, percent);
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
            transform.localPosition = _position;
            transform.localRotation = _rotation;
        }

        private void FixedUpdateManager_OnPreFixedUpdate()
        {
            _position = transform.localPosition;
            _rotation = transform.localRotation;
        }
    }
}