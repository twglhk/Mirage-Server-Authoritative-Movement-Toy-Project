using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirage;

namespace WardGames.John.AuthoritativeMovement
{
    public class TestSmoother : MonoBehaviour
    {
        public float SmoothSpeed = 5f;

        private TestNetworkMover _testNetworkMover;

        [Range(0f, 1f)]
        private float _interpolationTime = 0f;

        private Vector3 _startPos;
        private Vector3 _targetPos;

        private void Start()
        {
            _testNetworkMover = FindObjectOfType<TestNetworkMover>();
            _startPos = transform.position;
            _targetPos = _testNetworkMover.transform.position;
            _testNetworkMover.OnClientDeserialize.AddListener(() =>
            {
                _startPos = transform.position;
                _targetPos = _testNetworkMover.transform.position;
                _interpolationTime = 0f;
            });
        }

        private void Update()
        {
            _interpolationTime += Time.deltaTime * SmoothSpeed;
            transform.position = Vector3.MoveTowards(_startPos, _targetPos, _interpolationTime);
        }
    }
}