using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirage;
using Mirage.Serialization;

public class TestNetworkMover : NetworkBehaviour
{
    // Update is called once per frame
    void Update()
    {
        if (IsClient)
        {
            if (Input.GetKey(KeyCode.UpArrow))
            {
                ServerRpcMove(Direction.Up);
                Move(Direction.Up);
            }
            if (Input.GetKey(KeyCode.DownArrow))
            {
                ServerRpcMove(Direction.Down);
                Move(Direction.Down);
            }
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                ServerRpcMove(Direction.Left);
                Move(Direction.Left);
            }
            if (Input.GetKey(KeyCode.RightArrow))
            {
                ServerRpcMove(Direction.Right);
                Move(Direction.Right);
            }
        }
    }

    [ServerRpc]
    private void ServerRpcMove(Direction direction)
    {
        Move(direction);
    }

    private void Move(Direction direction)
    {
        if (direction.Equals(Direction.Up))
            transform.position += new Vector3(0f, 0f, 0.1f);
        else if (direction.Equals(Direction.Down))
            transform.position += new Vector3(0f, 0f, -0.1f);
        else if (direction.Equals(Direction.Left))
            transform.position += new Vector3(-0.1f, 0f, 0f);
        else if (direction.Equals(Direction.Right))
            transform.position += new Vector3(0.1f, 0f, 0f);
    }

    public override bool OnSerialize(NetworkWriter writer, bool initialState)
    {
        Debug.Log($"{gameObject.name} OnSerialize");
        return true;
    }

    public override void OnDeserialize(NetworkReader reader, bool initialState)
    {
        Debug.Log($"{gameObject.name} OnDeserialize");
    }

    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }
}
