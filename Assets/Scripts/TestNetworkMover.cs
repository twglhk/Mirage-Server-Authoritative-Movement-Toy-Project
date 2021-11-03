using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirage;
using Mirage.Serialization;
using WardGames.John.AuthoritativeMovement;

public class TestNetworkMover : NetworkBehaviour
{
    public float Speed = 5f;
    private uint _currentInputNumber = 0;
    private List<MoveInfo> _moveInfoList = new List<MoveInfo>();
    private string _myLog;
    private Queue _myLogQueue = new Queue();
    private float _lastRecvTime = 0f;

    public struct MoveInfo
    {
        public Direction Direction;
        public Vector3 CurrentPosition;
        public uint InputNumber;
    }

    public void AddMoveInfo(MoveInfo moveInfo)
    {
        _moveInfoList.Add(moveInfo);
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        _myLog = logString;
        //string newString = "\n [" + type + "] : " + _myLog;
        //_myLogQueue.Enqueue(newString);
        //if (type == LogType.Exception)
        //{
        //    newString = "\n" + stackTrace;
        //    _myLogQueue.Enqueue(newString);
        //}
        //_myLog = string.Empty;
        //foreach (string mylog in _myLogQueue)
        //{
        //    _myLog += mylog;
        //}
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
            var moveInfo = new MoveInfo()
            {
                Direction = inputDirection,
                CurrentPosition = transform.position,
                InputNumber = ++_currentInputNumber
            };
            transform.position = Move(transform.position, inputDirection);
            AddMoveInfo(moveInfo);
            ServerRpcMove(moveInfo);
        }
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
                goal += new Vector3(0f, 0f, -1f) * Speed * Time.fixedDeltaTime; ;
                break;
        }
        return goal;
    }

    private void UpdateServer()
    {
        //ClientRpcTestPing();
        SetDirtyBit(_moveInfoList.Count != 0 ? 1UL : 0UL);
    }

    [ClientRpc]
    private void ClientRpcTestPing()
    {
        Debug.Log($"[Client] ClientRpc Ping : {Time.time - _lastRecvTime}");
        _lastRecvTime = Time.time;
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

        var lastInputNumber = _moveInfoList[_moveInfoList.Count - 1].InputNumber;
        foreach (var moveInfo in _moveInfoList)
        {
            // TODO: 이 이동이 유효한지 체크
            transform.position = Move(transform.position, moveInfo.Direction);
        }

        writer.WriteUInt32(lastInputNumber);
        _moveInfoList.Clear();
    }

    private void ClientMoveSimulation(NetworkReader reader)
    {
        if (!IsClient) return;
        if (_moveInfoList.Count.Equals(0)) return;
       
        // TODO : 여기서 해야할 것은 처리된 인풋의 제거 + 현재 클라이언트가 저장하고 있는 인풋을 사용할 수 없게 되었을 때
        // 큐를 비우는 작업.

        int removeRangeIndex = -1; 
        var serverSimulatedInputNumber = reader.ReadUInt32();
        //Debug.Log($"servrSimulatedTimeStamp - {servrSimulatedTimeStamp}");
        foreach(var moveInfo in _moveInfoList)
        {
            if (moveInfo.InputNumber <= serverSimulatedInputNumber)
            {
                //Debug.Log($"To delete moveInfo.TimeStamp - {moveInfo.TimeStamp}");
                removeRangeIndex++;
            }
            else
                break;
        }
        _moveInfoList.RemoveRange(0, removeRangeIndex);

        //foreach (var moveInfo in _moveInfoList)
        //{
        //    transform.position = moveInfo.CurrentPosition;
        //}
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
