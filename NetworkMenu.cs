using Godot;
using System;
using System.Collections.Generic;
namespace BMUtil
{
    [GlobalClass]
    public partial class NetworkMenu : Menu, INetworkData
    {
        private static readonly string _Active = "A";
        private static readonly string _ID = "ID";

        private string ownerId = "";
        public string OwnerId
        {
            get { return ownerId; }
            set
            {
                ownerId = value;
                List<NetworkMenu> list = this.GetAllChildren<NetworkMenu>();
                foreach (NetworkMenu menu in list)
                {
                    menu.OwnerId = ownerId;
                }
            }
        }
        public bool NetworkUpdate { get; set; }

        public bool LocalUse
        {
            get
            {
                if (OwnerId == NetworkManager.Instance.PlayerId)
                    return true;
                return false;
            }
        }

        public override bool Active
        {
            get => base.Active;
            set
            {
                base.Active = value;
                NetworkUpdate = true;
                if (!LocalUse)
                {
                    Visible = false;
                }
            }
        }

        public override void _Ready()
        {
            Active = active;

            List<NetworkMenu> allSubmenus = this.GetAllChildren<NetworkMenu>();

            foreach (var m in allSubmenus)
            {
                if (m.FindParentOfType<NetworkMenu>() == this)
                    SubMenus.Add(m);
            }
            foreach (var menu in SubMenus)
            {
                menu.ZIndex = ZIndex;

                NetworkMenuManager.Instance.RegisterNetworkSubMenu(menu.Name, menu as NetworkMenu, OwnerId, active);
            }
        }

        public JsonValue SerializeNetworkData(bool forceReturn = false, bool ignoreThisUpdateOccurred = false)
        {
            if (!this.ShouldUpdate(forceReturn) || LocalUse)
                return null;

            JsonValue data = new JsonValue();
            data[_Active].Set(Active);
            data[_ID].Set(OwnerId);
            return this.CalculateNetworkReturn(data, ignoreThisUpdateOccurred);
        }

        public void DeserializeNetworkData(JsonValue data)
        {
            for (int i = 0; i < SubMenus.Count; i++)
                SubMenus[i].Name = SubMenus[i].GetUniqueId();

            ownerId = data[_ID].AsString();

            bool preActive = active;

            Active = data[_Active].AsBool();

            if(preActive != active && LocalUse)
            {
                if (active)
                    InvokeMenuActivated(this);
                else
                    InvokeMenuDeactivated(this);
            }
        }
    }
}