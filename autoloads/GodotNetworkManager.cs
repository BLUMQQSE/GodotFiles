using Godot;
using Steamworks.Data;
using System;
using System.Threading;
namespace BMUtil
{
    // !NOTE! Requires adding an arg to just the main window
	public partial class GodotNetworkManager : NetworkManager
	{

        private TimeTracker delay = new TimeTracker();

        private Vector2I largeSize = new Vector2I(16 * 73, 9 * 73);
        private Vector2I largeServer = new Vector2I(1380, 35);
        private Vector2I largeClient = new Vector2I(1380, 725);


        private Vector2I smallSize = new Vector2I(16 * 45, 9 * 45);
        private Vector2I smallServer = new Vector2I(1825, 35);
        private Vector2I smallClient = new Vector2I(1825, 475);

        bool useLarge = true;

        public override void _Ready()
        {
            base._Ready();

            Multiplayer.PeerConnected += PeerConnected;
            Multiplayer.PeerDisconnected += PeerDisconnected;
            Multiplayer.ConnectedToServer += ConnectedToServer;
            Multiplayer.ConnectionFailed += ConnectionFailed;

            if (OS.GetCmdlineArgs().Length != 2)
            {
                if (useLarge)
                {
                    GetWindow().Position = largeClient;
                    GetWindow().Size = largeSize;
                }
                else
                {
                    GetWindow().Position = smallClient;
                    GetWindow().Size = smallSize;
                }
                GetWindow().Title = "Client";
                GetWindow().Transient = true;


                var peer = new ENetMultiplayerPeer();
                peer.TransferMode = MultiplayerPeer.TransferModeEnum.Reliable;
                peer.CreateClient("127.0.0.1", 550);
                IsServer = false;
                Multiplayer.MultiplayerPeer = peer;
                GetTree().CurrentScene.Name = "Client";

            }
            else
            {
                delay.WaitTime = 0.5;
                delay.Loop = false;
                delay.Start();
                delay.TimeOut += OnDelayEnd;
                if (useLarge)
                { 
                    GetWindow().Position = largeServer;
                    GetWindow().Size = largeSize;
                }
                else
                {
                    GetWindow().Position = smallServer;
                    GetWindow().Size = smallSize;
                }
                GetWindow().Title = "Server";
                GetWindow().Transient = true;

                GetTree().CurrentScene.Name = "Server";

                SaveManager.Instance.CreateSave("TestingSave");
                LevelManager.Instance.LoadLevelPartition("Empty");
            }


        }

        private void OnDelayEnd(TimeTracker tracker)
        {
            PlayerId = "1";
            Connections = 1;


            var peer = new ENetMultiplayerPeer();
            peer.TransferMode = MultiplayerPeer.TransferModeEnum.Reliable;
            peer.CreateServer(550);
            Multiplayer.MultiplayerPeer = peer;

            Node player = ResourceManager.Instance.GetResourceByName<PackedScene>("TestPlayer").Instantiate<Node>();
            MakePlayer("1", player);
            GetTree().CurrentScene.AddChild(player);

            GetWindow().GrabFocus();
            delay.TimeOut -= OnDelayEnd;
        }

        // run on both
        private void PeerDisconnected(long id)
        {
            if (IsServer)
            {
                Connections -= 1;

                RemovePlayer(id.ToString());
            }
        }
        // run on both
        private void PeerConnected(long id)
        {
            PlayerId = Multiplayer.MultiplayerPeer.GetUniqueId().ToString();
            if (IsServer)
            {
                Connections += 1;
                
                PlayerId = Multiplayer.MultiplayerPeer.GetUniqueId().ToString();

                /* HOW TO ADD PLAYER TO GAME
                 * 
                 * Node playerNode = new Node();
                 * MakePlayer(id.ToString(), playerNode);
                 * GetTree().CurrentScene.AddChild(playerNode);
                 */

                // test
                Node player = ResourceManager.Instance.GetResourceByName<PackedScene>("TestPlayer").Instantiate<Node>();
                MakePlayer(id.ToString(), player);
                GetTree().CurrentScene.AddChild(player);

                var data = FullServerData();
                SendToClientId(id.ToString(), data);
            }
            else
            {
                PlayerId = Multiplayer.MultiplayerPeer.GetUniqueId().ToString();
            }
        }
        // run on client
        private void ConnectionFailed()
        {
        }
        // run on client
        private void ConnectedToServer()
        {
            IsServer = false;
        }


        public override void SendToServer(JsonValue data, bool reliable = true)
        {
            Rpc(MethodName.OnMessage, data.ToString());
        }

        public override void SendToClientId(string clientIdentifier, JsonValue data, bool reliable = true)
        {
            RpcId(long.Parse(clientIdentifier), MethodName.OnMessage, data.ToString());
        }

        public override void SendToClients(JsonValue data, bool reliable = true)
        {
            Rpc(MethodName.OnMessage, data.ToString());
        }
        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void OnMessage(string data)
        {
            JsonValue json = new JsonValue();
            json.Parse(data);
            if (IsServer)
            {
                SocketDataRecieved(json);
            }
            else
            {
                ConnectionDataRecieved(json);
            }
        }
    }
}