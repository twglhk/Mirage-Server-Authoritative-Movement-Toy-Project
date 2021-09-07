using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WardGames.John.AuthoritativeMovement.Manager
{
    public class FixedUpdateManager : MonoBehaviour
    {
        #region public
        /// <summary>
        /// Dispatched before a simulated fixed update occurs.
        /// </summary>
        public static event Action OnPreFixedUpdate;
        /// <summary>
        /// Dispatched when a simulated fixed update occurs.
        /// </summary>
        public static event Action OnFixedUpdate;
        /// <summary>
        /// Dispatched after a simulated fixed update occurs. Physics would have simulated prior to this event.
        /// </summary>
        public static event Action OnPostFixedUpdate;

        public static uint FixedFrame = 0;
        #endregion

        #region Private
        /// <summary>
        /// Ticks applied from updates.
        /// </summary>
        private float _updateTicks = 0f;
        #endregion

        // Start is called before the first frame update
        void Awake()
        {
            Physics.autoSimulation = false;
            Physics2D.autoSyncTransforms = false;
        }

        // Update is called once per frame
        void Update()
        {
            UpdateTicks();
        }

        /// <summary>
        /// Initializes this script for use. Should only be completed once.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void FirstInitialize()
        {
            GameObject go = new GameObject();
            go.AddComponent<FixedUpdateManager>();
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// Adds the current deltaTime to update ticks and processes simulated fixed update.
        /// </summary>
        private void UpdateTicks()
        {
            _updateTicks += Time.deltaTime;
            while (_updateTicks >= Time.fixedDeltaTime) // Update timing catches FixedUpdate timing
            {
                _updateTicks -= Time.fixedDeltaTime;
                /* If at maximum value then reset fixed frame.
                * This would probably break the game but even at
                * 128t/s it would take over a year of the server
                 * running straight to ever reach this value! */
                if (FixedFrame == uint.MaxValue)
                    FixedFrame = 0;
                FixedFrame++;

                OnPreFixedUpdate?.Invoke();
                OnFixedUpdate?.Invoke();        // == MonoBehavior's void FixedUpdate()

                Physics2D.Simulate(Time.fixedDeltaTime);
                Physics.Simulate(Time.fixedDeltaTime);

                OnPostFixedUpdate?.Invoke();
            }
        }
    }
}