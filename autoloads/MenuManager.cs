using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
namespace BMUtil
{
    // IMPORTANT NOTE: Main scene must contain a node "UI" for menus to attach
	public partial class MenuManager : Node
	{
        public event Action<Menu> MenuOpened;
        public event Action<Menu> MenuClosed;

        private static MenuManager instance;
        public static MenuManager Instance { get { return instance; } }

        public MenuManager()
        {
            if (instance == null)
                instance = this;
        }

        protected List<Menu> TopMenus { get; set; } = new List<Menu>();
        protected Dictionary<string, Menu> SubMenus { get; set; } = new Dictionary<string, Menu>();
        private InputState priorInputState = InputState.Gameplay;
        
        private uint uiStateMenusOpen = 0;
        protected uint UIStateMenusOpen
        {
            get { return uiStateMenusOpen; }
            set
            {
                uint priorVal = uiStateMenusOpen;
                uiStateMenusOpen = value;
                if(priorVal == uiStateMenusOpen)
                    return;
                if (uiStateMenusOpen == 0)
                {
                    InputManager.Instance.SetInputState(priorInputState);
                }
                else if(uiStateMenusOpen > 0 && priorVal == 0)
                {
                    priorInputState = InputManager.Instance.InputState;
                    InputManager.Instance.SetInputState(InputState.UI);
                }
            }
        }

        public override void _Ready()
        {
            base._Ready();
            MenuOpened += OnMenuOpened;
            MenuClosed += OnMenuClosed;
        }

        private void OnMenuOpened(Menu menu)
        {
            if (menu.ModifyInputStateWhenOpen)
                UIStateMenusOpen += 1;
        }

        private void OnMenuClosed(Menu menu)
        {
            if (menu.ModifyInputStateWhenOpen)
                UIStateMenusOpen -= 1;
        }

        public virtual void AddMenu(string menuId, Menu menu, bool active = false)
        {
            menu.ZIndex = 10;
            menu.Name = menuId;

            TopMenus.Add(menu);

            
            GetTree().CurrentScene.GetNode("UI").AddChild(menu, true);

            SetActive(menuId, active);
        }

        public void RemoveMenu(string menuId)
        {
            Menu m = TopMenus.FirstOrDefault(cus => cus.Name == menuId);
            if (m != null)
            {
                if (m.Active)
                {
                    m.Active = false;
                    MenuClosed?.Invoke(m);
                }
                TopMenus.Remove(m);
                m.QueueFree();
            }
        }

        public void RegisterSubMenu(string menuId, Menu menu, bool active = false)
        {
            menu.ZIndex = 10;
            menu.Name = menuId;
            SubMenus.Add(menuId, menu);
            SetActive(menuId, active);
        }

        public void UnregisterSubMenu(string menuId)
        {
            if (SubMenus.ContainsKey(menuId))
            {
                SetActive(menuId, false);
                SubMenus.Remove(menuId);
            }
        }
        public void SetActive(string menuId, bool active)
        {
            Menu m = TopMenus.FirstOrDefault(cus => cus.Name == menuId);
            if (m != null)
            {
                if (m.Active != active)
                {
                    m.Active = active;

                    if (active)
                    {
                        MenuOpened?.Invoke(m);
                    }
                    else
                    {
                        MenuClosed?.Invoke(m);
                    }
                }
            }
            else if (SubMenus.ContainsKey(menuId))
            {
                if (SubMenus[menuId].Active != active) // only toggle active if not already in that state
                {
                    SubMenus[menuId].Active = active;
                }
            }
        }

        public void Toggle(string menuId)
        {
            Menu m = TopMenus.FirstOrDefault(cus => cus.Name == menuId);
            if (m != null)
            {   
                SetActive(menuId, !m.Active);
            }
            else if (SubMenus.ContainsKey(menuId))
            {
                SetActive(menuId, !SubMenus[menuId].Active);
            }
        }

    }
}