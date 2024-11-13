using BMUtil;
using Godot;
using System;

namespace BMUtil
{
    public partial class Main : Node
    {
        private Main instance = null;
        public Main Instance { get { return instance; } }

        public Main()
        {
            if (instance == null)
                instance = this;
        }

        public Node UI { get; private set; }
        public Node Sound { get; private set; }

        public override void _Ready()
        {
            base._Ready();

            UI = GetNode("UI");
            Sound = GetNode("Sound");

            DeveloperConsole d = ResourceManager.Instance.GetResourceByName<PackedScene>("DeveloperConsole").Instantiate<DeveloperConsole>();
            d.ModifyInputStateWhenOpen = true;
            MenuManager.Instance.AddMenu("DeveloperConsole", d, false);
        }
    }
}