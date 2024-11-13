using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
namespace BMUtil
{
	public partial class NetworkMenuManager : MenuManager
	{
        private static NetworkMenuManager instance;
        public static NetworkMenuManager Instance { get { return instance; } }


        private Dictionary<string, uint> PlayerUIStateMenusOpen = new Dictionary<string, uint>();

        private Dictionary<string, Control> PlayerUINodes = new Dictionary<string, Control>();
        private Dictionary<string, List<Menu>> PlayerTopMenus = new Dictionary<string, List<Menu>>();
        private Dictionary<string, Dictionary<string, Menu>> PlayerSubMenus = new Dictionary<string, Dictionary<string, Menu>>();



        public NetworkMenuManager()
        {
            if (instance == null)
                instance = this;
        }

        public override void _Ready()
        {
            base._Ready();


            NetworkManager.Instance.PlayerAdded += OnPlayerAdded;
            NetworkManager.Instance.PlayerRemoved += OnPlayerRemoved;
        }


        public override void AddMenu(string menuId, Menu menu, bool active = false)
        {
            menu.MakeLocalOnly();
            base.AddMenu(menuId, menu, active);
        }
        public void AddNetworkMenu(string menuId, NetworkMenu networkMenu, string playerId, bool active = false)
        {

            networkMenu.ZIndex = 10;
            networkMenu.Name = menuId;
            networkMenu.OwnerId = playerId;

            PlayerUINodes[playerId].AddChild(networkMenu, true);
            SetActive(menuId, active);
            PlayerTopMenus[playerId].Add(networkMenu);
        }

        public void RemoveNetworkMenu(string menuId, string playerId)
        {
            PlayerUINodes[playerId].GetNode(menuId).QueueFree();
            PlayerTopMenus[playerId].Remove(PlayerUINodes[playerId].GetNode<NetworkMenu>(menuId));
        }

        public void RegisterNetworkSubMenu(string menuId, NetworkMenu networkMenu, string playerId, bool active = false)
        {
            networkMenu.ZIndex = 10;
            networkMenu.Name= menuId;
            networkMenu.OwnerId = playerId;

            PlayerSubMenus[playerId].Add(menuId, networkMenu);
            SubMenus.Add(menuId, networkMenu);
            SetActive(menuId, active);
        }
        public void UnregisterNetworkSubMenu(string subMenuId, string playerId)
        {
            if (PlayerSubMenus[playerId].ContainsKey(subMenuId))
            {
                NetworkSetActive(subMenuId, playerId, false);
                SubMenus.Remove(subMenuId);
            }
        }

        public void NetworkSetActive(string menuId, string playerId, bool active)
        {
            if (!NetworkManager.Instance.IsServer)
            {
                NetworkManager.Instance.RpcServer(this, nameof(NetworkSetActive), menuId, playerId, active);
                return;
            }
            NetworkMenu m = PlayerUINodes[playerId].GetNode<NetworkMenu>(menuId);
            if (m != null)
            {
                if (m.Active != active)
                {
                    m.Active = active;
                }
                else if (SubMenus.ContainsKey(menuId))
                {
                    if (SubMenus[menuId].Active != active)
                    {
                        SubMenus[menuId].Active = active;
                    }
                }
                
            }

            int change = 1;
            if (!active)
                change = -1;
            if (playerId == NetworkManager.Instance.PlayerId)
                UpdateUIMenusOpen(change);
            else
                NetworkManager.Instance.RpcClient(playerId, this, nameof(UpdateUIMenusOpen), change);
            
        }

        public void NetworkToggle(string menuId, string playerId)
        {
            if (!NetworkManager.Instance.IsServer)
            {
                NetworkManager.Instance.RpcServer(this, nameof(NetworkToggle), menuId, playerId);
                return;
            }

            NetworkMenu m = PlayerUINodes[playerId].GetNode<NetworkMenu>(menuId);
            if (m != null)
            {
                SetActive(menuId, !m.Active);
            }
        }

        [ServerToClient]
        private void UpdateUIMenusOpen(int menuCountChange)
        {
            if (menuCountChange < 0)
                UIStateMenusOpen -= 1;
            else
                UIStateMenusOpen += 1;
        }

        private void OnPlayerAdded(Node node)
        {
            if (!NetworkManager.Instance.IsServer) return;

            string ownerId = node.GetOwnerId();
            
            PlayerUIStateMenusOpen.Add(ownerId, 0);

            Control playerUIContainer = new Control();
            playerUIContainer.Name = "Player_Menus_"+ownerId;
            //if (ownerId != "1")
            //    return;
            // the issue must be this getting called to soon...
            GetTree().CurrentScene.GetNode("UI").AddChild(playerUIContainer);
            
            
            
            
            PlayerUINodes.Add(ownerId, playerUIContainer);

            PlayerTopMenus.Add(ownerId, new List<Menu>());

            PlayerSubMenus.Add(ownerId, new Dictionary<string, Menu>());
        }

        private void OnPlayerRemoved(Node node)
        {

            if (!NetworkManager.Instance.IsServer) return;

            string ownerId = node.GetOwnerId();
            
            PlayerUIStateMenusOpen.Remove(ownerId);

            GetNode(ownerId).QueueFree();
            PlayerUINodes.Remove(ownerId);

            PlayerTopMenus.Remove(ownerId);

            PlayerSubMenus.Remove(ownerId);
        }


    }
}