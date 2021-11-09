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
    [Range(1f, 20f)]
    [SerializeField] private float _interpolateSpeed;
    #endregion

    #region Private.
    public float Speed = 5f;
    private uint _currentInputNumber = 0;
    private List<MoveInfo> _moveInfoList = new List<MoveInfo>();
    private string _myLog;
    private Queue _myLogQueue = new Queue();
    private float _lastRecvTime = 0f;
    private Vector3 _clientRecvPos;
    private Vector3 _simulatedPos;
    private Vector3 _renderingPos;

    private bool _isBlocked = false;
    #endregion

    #region Const.
    private const float MAX_MOVEINFO_COUNT = 20;
    #endregion

    public UnityEvent OnClientDeserialize = new UnityEvent();

    public struct MoveInfo
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
        _clientRecvPos = transform.position;
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
            ProcessReceivedServerMoveInfo();
            //Debug.Log($"[Client] MoveInfo Size {_moveInfoList.Count}");
        }
    }

    private void UpdateClient()
    {
        var inputDirection = ProcessInput();

        if (_isBlocked)
            Debug.Log($"[Client] isBlocked : {_isBlocked}");

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
            //_renderingPos = Move(transform.position, inputDirection);
        }
        
    }

    private void ProcessReceivedServerMoveInfo()
    {
        if (_moveInfoList.Count == 0)
            return;

        Vector3 goal = _clientRecvPos;
        foreach (var moveInfo in _moveInfoList)
        {
            goal = Move(goal, moveInfo.Direction);
        }
        _simulatedPos = goal;

        transform.position = Vector3.MoveTowards(transform.position, _simulatedPos, Time.deltaTime * _interpolateSpeed);
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
        if (Random.Range(1, 200) == 1)
        {
            TargetRpcMoveLock();
            Invoke(nameof(TargetRpcMoveUnlock), 1f);
        }

        SetDirtyBit(_moveInfoList.Count != 0 ? 1UL : 0UL);
    }

    [ClientRpc(target = RpcTarget.Owner)]
    private void TargetRpcMoveLock()
    {
        _isBlocked = true;
    }

    [ClientRpc(target = RpcTarget.Owner)]
    private void TargetRpcMoveUnlock()
    {
        _isBlocked = false;
    }

    [ServerRpc]
    private void ServerRpcMove(MoveInfo moveInfo)
    {
        _moveInfoList.Add(moveInfo);
        if (_moveInfoList.Count > MAX_MOVEINFO_COUNT)
            _moveInfoList.RemoveAt(0);
    }

    [Server]
    private void ServerMoveSimulation(NetworkWriter writer)
    {
        if (_moveInfoList.Count == 0) return;

        //var moveInfo = _moveInfoList[0];
        //_moveInfoList.RemoveAt(0);
        Vector3 goal = transform.position;
        int count = _moveInfoList.Count;
        uint lastInputNumber = _moveInfoList[count - 1].InputNumber;
        foreach(var moveInfo in _moveInfoList)
        {
            if (_isBlocked)
                continue;
            
            goal = Move(goal, moveInfo.Direction);
        }
        _moveInfoList.RemoveRange(0, count);

        transform.position = goal;
        writer.WriteUInt32(lastInputNumber);
        writer.WriteVector3(goal);

        //Debug.Log($"[Server] write Buffer Size : {writer.ByteLength}");
    }

    private void ServerResetMoveBlock()
    {
        _isBlocked = false;
    }

    [Client]
    private void ClientRecvServerMoveResult(NetworkReader reader)
    {
        //Debug.Log($"[Client] read Buffer Size : {reader.BitLength}");

        if (_moveInfoList.Count == 0) return;

        // TODO : 여기서 해야할 것은 처리된 인풋의 제거 + 현재 클라이언트가 저장하고 있는 인풋을 사용할 수 없게 되었을 때
        // 큐를 비우는 작업.

        var lastInputNumber = reader.ReadUInt32();
        var clientRecvPos = reader.ReadVector3();
        
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

        _clientRecvPos = clientRecvPos;

        //Vector3 goal = clientRecvPos;
        //foreach (MoveInfo moveInfo in _moveInfoList)
        //{
        //    goal = Move(goal, moveInfo.Direction);
        //}
        //_simulatedPos = goal;

        //int index = _moveInfoList.FindIndex(x => x.InputNumber <= )
        //_moveInfoList.RemoveRange(0, serverReconciliatedMoveInfoList.Count);
    }

    public override bool OnSerialize(NetworkWriter writer, bool initialState)
    {
        if (initialState) return false;

        // 서버에서만 호출
        //Debug.Log($"{gameObject.name} OnSerialize");
        //Debug.Log($"[Server] OnSerialize Ping : {Time.time - _lastRecvTime}");
        _lastRecvTime = Time.time;
        ServerMoveSimulation(writer);
        return true;
    }

    public override void OnDeserialize(NetworkReader reader, bool initialState)
    {
        if (initialState) return;

        if (!HasAuthority)
        {
            Debug.Log("It is not mine");
            reader.ReadUInt32();
            reader.ReadVector3();
            return;
        }
            

        // 클라에서만 호출
        //Debug.Log($"{gameObject.name} OnDeserialize");
        //Debug.Log($"[Client] OnDeserialize Ping : {Time.time - _lastRecvTime}");
        _lastRecvTime = Time.time;
        ClientRecvServerMoveResult(reader);
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
