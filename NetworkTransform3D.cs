using Godot;
using System;
namespace BMUtil
{
    [GlobalClass]
    public partial class NetworkTransform3D : Node, INetworkData
    {
        private static readonly string _SP = "SP";
        private static readonly string _SR = "SR";
        private static readonly string ClientUpdateMethod = "ClientUpdate";

        public static readonly int MaxDistanceOff = 5;
        public Vector3 SyncPos { get; set; }
        public Vector3 SyncRot { get; set; }
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

            if(!IsLocalOwned)
            {
                if (GetParent<Node3D>().Position.DistanceSquaredTo(SyncPos) > Mathf.Pow(MaxDistanceOff, 2))
                {
                    if (NetworkManager.Instance.IsServer)
                    {
                        // on server we trust our own position over that of a client
                        SyncPos = GetParent<Node3D>().Position;
                    }
                    else
                    {
                        // on client, we trust server sync pos more than our own
                        GetParent<Node3D>().Position = SyncPos;
                    }
                }
                else
                    GetParent<Node3D>().Position = GetParent<Node3D>().Position.Lerp(SyncPos, 8 * (float)delta);
                GetParent<Node3D>().Rotation = SyncRot;
            }
        }

        private void ClientUpdate(Vector3 syncPos, Vector3 syncRot)
        {
            if (GetParent<Node3D>().Position.DistanceSquaredTo(SyncPos) > Mathf.Pow(MaxDistanceOff, 2))
                return;

            SyncPos = syncPos;
            SyncRot = syncRot;
        }


        private void UpdateServer()
        {
            NetworkManager.Instance.RpcServer(this, ClientUpdateMethod, GetParent<Node3D>().Position, GetParent<Node3D>().Rotation);
        }


        public JsonValue SerializeNetworkData(bool forceReturn = false, bool ignoreThisUpdateOccurred = false)
        {
            JsonValue data = new JsonValue();

            data[_SP].Set(GetParent<Node3D>().Position);
            data[_SR].Set(GetParent<Node3D>().Rotation);

            return data;
        }
        public void DeserializeNetworkData(JsonValue data)
        {
            SyncPos = data[_SP].AsVector3();
            SyncRot = data[_SR].AsVector3();
        }
    }
}