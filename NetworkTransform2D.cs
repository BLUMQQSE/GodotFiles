using Godot;
using System;
namespace BMUtil
{
	[GlobalClass]
	public partial class NetworkTransform2D : Node, INetworkData
    {
        private static readonly string _SP = "SP";
        private static readonly string _SR = "SR";

        public static readonly int MaxDistanceOff = 200;
        public Vector2 SyncPos { get; set; }
        public float SyncRot { get; set; }
        public bool IsLocalOwned { get; private set; } = false;
        public bool NetworkUpdate { get; set; }

        public override void _Ready()
        {
            base._Ready();
            if (GetParent().IsInGroup(NetworkManager.PlayerGroup))
            {
                string ownerId = GetParent().GetMeta(NetworkManager.OwnerIdMeta).ToString();
                if (NetworkManager.Instance.PlayerId == ownerId)
                    IsLocalOwned = true;
            }
            else if (NetworkManager.Instance.IsServer)
                IsLocalOwned = true;

            if (!NetworkManager.Instance.IsServer && IsLocalOwned)
                NetworkManager.Instance.NetworkUpdate_Client += UpdateServer;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            if (!IsLocalOwned)
            {
                if (GetParent<Node2D>().Position.DistanceSquaredTo(SyncPos) > Mathf.Pow(MaxDistanceOff, 2))
                {
                    if (NetworkManager.Instance.IsServer)
                    {
                        // on server we trust our own position over that of a client
                        SyncPos = GetParent<Node2D>().Position;
                    }
                    else
                    {
                        // on client, we trust server sync pos more than our own
                        GetParent<Node2D>().Position = SyncPos;
                    }
                }
                else
                    GetParent<Node2D>().Position = GetParent<Node2D>().Position.Lerp(SyncPos, 8 * (float)delta);
                GetParent<Node2D>().Rotation = SyncRot;
            }
            else
            {
                if (GetParent<Node2D>().Position.DistanceSquaredTo(SyncPos) > Mathf.Pow(MaxDistanceOff, 2))
                {
                    if (NetworkManager.Instance.IsServer)
                    {
                        // on server we trust our own position over that of a client
                        SyncPos = GetParent<Node2D>().Position;
                    }
                    else
                    {
                        // on client, we trust server sync pos more than our own
                        GetParent<Node2D>().Position = SyncPos;
                    }
                }
            }
        }

        private void ClientUpdate(Vector2 syncPos, float syncRot)
        {
            if (GetParent<Node2D>().Position.DistanceSquaredTo(SyncPos) > Mathf.Pow(MaxDistanceOff, 2))
                return;

            SyncPos = syncPos;
            SyncRot = syncRot;
        }

        [ClientToServer]
        private void UpdateServer()
        {
            NetworkManager.Instance.RpcServer(this, nameof(ClientUpdate), GetParent<Node2D>().Position, GetParent<Node2D>().Rotation);
        }


        public JsonValue SerializeNetworkData(bool forceReturn = false, bool ignoreThisUpdateOccurred = false)
        {
            JsonValue data = new JsonValue();

            data[_SP].Set(GetParent<Node2D>().Position);
            data[_SR].Set(GetParent<Node2D>().Rotation);

            return data;
        }
        public void DeserializeNetworkData(JsonValue data)
        {
            SyncPos = data[_SP].AsVector2();
            SyncRot = data[_SR].AsFloat();
        }
    }
}
