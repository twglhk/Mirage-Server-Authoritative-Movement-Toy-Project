using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Mirage;
using Mirage.Serialization;
using WardGames.John.AuthoritativeMovement;

public class TestNetworkMover : NetworkBehaviour
{
    #region Serialized.
    [Range(1f, 10f)]
    [SerializeField] private float _interpolateSpeed;
    #endregion

    #region Private.
    public float Speed = 5f;
    private uint _currentInputNumber = 0;
    private List<MoveInfo> _moveInfoList = new List<MoveInfo>();
    private string _myLog;
    private Queue _myLogQueue = new Queue();
    private float _lastRecvTime = 0f;
    private Vector3 _simulatedPos;
    private Vector3 _renderingPos;
    
    [SyncVar]
    private bool _isBlocked = false;
    #endregion

    #region Const.
    private const float MAX_MOVEINFO_COUNT = 20;
    #endregion

    public UnityEvent OnClientDeserialize = new UnityEvent();

    [System.Serializable]
    public class MoveInfo
    {
        public uint InputNumber;
        public Direction Direction;
        public Vector3 CurrentPos;
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
            ProcessReceivedServerMoveInfo();
            UpdateClient();
        }
    }

    private void UpdateClient()
    {
        var inputDirection = ProcessInput();
        if (!inputDirection.Equals(Direction.NONE) && !_isBlocked)
        {
            //transform.position = Move(transform.position, inputDirection);
            var moveInfo = new MoveInfo()
            {
                Direction = inputDirection,
                CurrentPos = transform.position,
                InputNumber = ++_currentInputNumber
            };
            AddMoveInfo(moveInfo);
            ServerRpcMove(moveInfo);

            _renderingPos = Move(transform.position, inputDirection);
        }
        transform.position = _renderingPos = Vector3.MoveTowards(_renderingPos, _simulatedPos, Time.deltaTime * _interpolateSpeed);
    }

    private void ProcessReceivedServerMoveInfo()
    {
        if (_moveInfoList.Count == 0)
            return;

        Vector3 goal = _moveInfoList[0].CurrentPos;
        foreach (var moveInfo in _moveInfoList)
        {
            goal = Move(goal, moveInfo.Direction);
        }
        _simulatedPos = goal;
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
        if (_moveInfoList.Count > MAX_MOVEINFO_COUNT)
            _moveInfoList.RemoveAt(0);
    }

    private void ServerMoveSimulation(NetworkWriter writer)
    {
        if (!IsServer) return;
        if (_moveInfoList.Count.Equals(0)) return;

        //var moveInfo = _moveInfoList[0];
        //_moveInfoList.RemoveAt(0);
        Vector3 goal = transform.position;
        int count = _moveInfoList.Count;
        uint lastInputNumber = _moveInfoList[count - 1].InputNumber;
        foreach(var moveInfo in _moveInfoList)
        {
            goal = Move(goal, moveInfo.Direction);
        }
        _moveInfoList.RemoveRange(0, count);

        //if (Random.Range(1, 20) <= 1)
        //{
        //    Debug.Log($"[Server] Move Block : {moveInfo.InputNumber}");
        //    _isBlocked = true;
        //    Invoke(nameof(ServerResetMoveBlock), 1f);
        //}
        //else
        //{
        //    goal = Move(start, moveInfo.Direction);
        //}

        transform.position = goal;
        writer.WriteUInt32(lastInputNumber);
        writer.WriteVector3(goal);
    }

    private void ServerResetMoveBlock()
    {
        _isBlocked = false;
    }

    [Client]
    private void ClientMoveSimulation(NetworkReader reader)
    {
        if (!IsClient) return;
        if (_moveInfoList.Count.Equals(0)) return;

        // TODO : 여기서 해야할 것은 처리된 인풋의 제거 + 현재 클라이언트가 저장하고 있는 인풋을 사용할 수 없게 되었을 때
        // 큐를 비우는 작업.

        var lastInputNumber = reader.ReadUInt32();
        var simulatedPos = reader.ReadVector3();
        //var startPos = reader.ReadVector3();
        //var serverReconciliatedMoveInfoList = reader.ReadList<MoveInfo>();
        //if (serverReconciliatedMoveInfoList.Count.Equals(0)) return;
        //Vector3 goal = startPos;
        //for (int i = 0; i < serverReconciliatedMoveInfoList.Count; ++i)
        //{
        //    if (serverReconciliatedMoveInfoList[i].isValid)
        //        goal = Move(goal, serverReconciliatedMoveInfoList[i].Direction);
        //}

        int inputIndex = _moveInfoList.FindIndex((x) => lastInputNumber == x.InputNumber);

        if (inputIndex != -1)
            _moveInfoList.RemoveRange(0, inputIndex + 1);   

        //int index = _moveInfoList.FindIndex(x => x.InputNumber <= )
        //_moveInfoList.RemoveRange(0, serverReconciliatedMoveInfoList.Count);
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
