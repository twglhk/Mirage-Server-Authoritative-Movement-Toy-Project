using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Mirage;
using Mirage.Serialization;
using WardGames.John.AuthoritativeMovement;

public class TestNetworkMover : NetworkBehaviour
{
    #region Private.
    public float Speed = 5f;
    private uint _currentInputNumber = 0;
    private List<MoveInfo> _moveInfoList = new List<MoveInfo>();
    private string _myLog;
    private Queue _myLogQueue = new Queue();
    private float _lastRecvTime = 0f;
    private Vector3 _simulatedPos;
    private Vector3 _renderingPos;
    #endregion

    public UnityEvent OnClientDeserialize = new UnityEvent();

    [System.Serializable]
    public class MoveInfo
    {
        public uint InputNumber;
        public Direction Direction;
        public Vector3 MovedPosition;
        public bool isValid;
    }

    private void Awake()
    {
        Identity.OnStartClient.AddListener(OnStartClient);
    }

    private void OnStartClient()
    {
        _renderingPos = transform.position;
        _simulatedPos = transform.position;
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    public void AddMoveInfo(MoveInfo moveInfo)
    {
        _moveInfoList.Add(moveInfo);
    }
    
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        _myLog = logString;
        string newString = "\n [" + type + "] : " + _myLog;

        if (_myLogQueue.Count > 10)
            _myLogQueue.Clear();

        _myLogQueue.Enqueue(newString);
        if (type == LogType.Exception)
        {
            newString = "\n" + stackTrace;
            _myLogQueue.Enqueue(newString);
        }
        _myLog = string.Empty;
        foreach (string mylog in _myLogQueue)
        {
            _myLog += mylog;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (IsServer)
        {
            UpdateServer();
        }

        if (HasAuthority)
        {
            UpdateClient();
        }
    }

    private void UpdateClient()
    {
        var inputDirection = ProcessInput();
        if (!inputDirection.Equals(Direction.NONE))
        {
            //transform.position = Move(transform.position, inputDirection);
            _renderingPos = Move(_renderingPos, inputDirection);
            var moveInfo = new MoveInfo()
            {
                Direction = inputDirection,
                MovedPosition = transform.position,
                InputNumber = ++_currentInputNumber
            };
            AddMoveInfo(moveInfo);
            ServerRpcMove(moveInfo);
        }

        transform.position = _renderingPos
            = Vector3.MoveTowards(_renderingPos, _simulatedPos, 0.01f);
    }

    private Direction ProcessInput()
    {
        if (Input.GetKey(KeyCode.UpArrow))
        {
            //transform.position += new Vector3(0f, 0f, 0.1f);
            return Direction.Up;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            //transform.position += new Vector3(0f, 0f, -0.1f);
            return Direction.Down;
        }
        return Direction.NONE;
    }

    private Vector3 Move(Vector3 startPos, Direction direction)
    {
        Vector3 goal = startPos;
        switch(direction)
        {
            case Direction.Up:
                goal += new Vector3(0f, 0f, 1f) * Speed * Time.fixedDeltaTime;
                break;
            case Direction.Down:
                goal += new Vector3(0f, 0f, -1f) * Speed * Time.fixedDeltaTime;
                break;
        }
        return goal;
    }

    private void UpdateServer()
    {
        SetDirtyBit(_moveInfoList.Count != 0 ? 1UL : 0UL);
    }

    [ServerRpc]
    private void ServerRpcMove(MoveInfo moveInfo)
    {
        _moveInfoList.Add(moveInfo);
    }

    private void ServerMoveSimulation(NetworkWriter writer)
    {
        if (!IsServer) return;
        if (_moveInfoList.Count.Equals(0)) return;

        Vector3 start = transform.position;
        Vector3 goal = transform.position;
        foreach (var moveInfo in _moveInfoList)
        {
            if (Random.Range(1, 10) <= 2)
            {
                Debug.Log($"[Server] Move Block : {moveInfo.InputNumber}");
                moveInfo.isValid = false;
            }
            else
            {
                goal = Move(goal, moveInfo.Direction);
                moveInfo.isValid = true;
            }
        }
        transform.position = goal;
        writer.WriteVector3(start);
        writer.WriteList(_moveInfoList);
        _moveInfoList.Clear();
        
    }

    private void ClientMoveSimulation(NetworkReader reader)
    {
        if (!IsClient) return;
        if (_moveInfoList.Count.Equals(0)) return;

        // TODO : 여기서 해야할 것은 처리된 인풋의 제거 + 현재 클라이언트가 저장하고 있는 인풋을 사용할 수 없게 되었을 때
        // 큐를 비우는 작업.

        var startPos = reader.ReadVector3();
        var serverReconciliatedMoveInfoList = reader.ReadList<MoveInfo>();
        if (serverReconciliatedMoveInfoList.Count.Equals(0)) return;
        Vector3 goal = startPos;
        for (int i = 0; i < serverReconciliatedMoveInfoList.Count; ++i)
        {
            if (serverReconciliatedMoveInfoList[i].isValid)
                goal = Move(goal, serverReconciliatedMoveInfoList[i].Direction);
        }

        _simulatedPos = goal;
        _moveInfoList.RemoveRange(0, serverReconciliatedMoveInfoList.Count);
    }

    public override bool OnSerialize(NetworkWriter writer, bool initialState)
    {
        // 서버에서만 호출
        Debug.Log($"{gameObject.name} OnSerialize");
        Debug.Log($"[Server] OnSerialize Ping : {Time.time - _lastRecvTime}");
        _lastRecvTime = Time.time;
        ServerMoveSimulation(writer);
        return true;
    }

    public override void OnDeserialize(NetworkReader reader, bool initialState)
    {
        // 클라에서만 호출
        Debug.Log($"{gameObject.name} OnDeserialize");
        Debug.Log($"[Client] OnDeserialize Ping : {Time.time - _lastRecvTime}");
        _lastRecvTime = Time.time;
        ClientMoveSimulation(reader);
    }

    private void OnGUI()
    {
        GUI.contentColor = Color.red;
        GUILayout.Label(_myLog);
    }

    public enum Direction
    {
        NONE,
        Up,
        Down,
        Left,
        Right
    }
}
