using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Mirage;

namespace WardGames.John.Util
{
    public class NetworkFrameSetter : NetworkBehaviour
    {
        #region Serialized.
        [Header("Frame Setting *")]
        [FormerlySerializedAs("Server Frame")]
        [SerializeField] int _serverFrame = 60;

        [FormerlySerializedAs("Client Frame")]
        [SerializeField] int _clientFrame = 60;
        #endregion

        private void Awake()
        {
            Identity.OnStartServer.AddListener(OnStartServer);
            Identity.OnStartClient.AddListener(OnStartClient);
        }

        private void OnStartServer()
        {
            Application.targetFrameRate = _serverFrame;
        }

        private void OnStartClient()
        {
            Application.targetFrameRate = _clientFrame;
        }
    }
}