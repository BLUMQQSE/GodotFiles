using Godot;
using System;
using System.Collections.Generic;
namespace BMUtil
{
	public partial class Menu : Control
	{
        public event Action<Menu> MenuActivated;
        public event Action<Menu> MenuDeactivated;

        [Export] public bool ModifyInputStateWhenOpen;

        protected bool active = false;

        public List<Menu> SubMenus = new List<Menu>();

        public Menu()
        {
            AddToGroup(SaveManager.NotPersistentGroup);
        }

        protected void InvokeMenuActivated(Menu menu)
        {
            MenuActivated?.Invoke(menu);
        }
        protected void InvokeMenuDeactivated(Menu menu)
        {
            MenuDeactivated?.Invoke(menu);
        }

        public virtual bool Active
        {
            get { return active; }
            set
            {
                if (active != value)
                    active = value;

                Visible = active;

                if (active)
                    MenuActivated?.Invoke(this);
                else
                    MenuDeactivated?.Invoke(this);
            }
        }

        public override void _Ready()
        {
            base._Ready();
            Active = active;

            List<Menu> allSubmenus = this.GetAllChildren<Menu>();

            foreach (var m in allSubmenus)
            {
                if (m.FindParentOfType<Menu>() == this)
                    SubMenus.Add(m);
            }
            foreach (var menu in SubMenus)
            {
                menu.ZIndex = ZIndex;
                MenuManager.Instance.RegisterSubMenu(menu.Name, menu, active);
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            foreach (var menu in SubMenus)
            {
                MenuManager.Instance.UnregisterSubMenu(menu.GetUniqueId());
            }
        }

    }
}