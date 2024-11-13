using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Channels;
namespace BMUtil
{
	public enum Keybind
	{
        MoveUp,
        MoveLeft,
        MoveDown,
        MoveRight,
        ConsoleToggle,
        ConsoleShift,
        ConsoleUp,
        ConsoleDown,
        ConsoleLeft,
        ConsoleRight,
        Escape
	}
	public enum InputState
	{
        Gameplay,
        UI
	}

	public partial class InputManager : Node
	{
        private static InputManager instance;
        public static InputManager Instance { get { return instance; } }

        public InputManager()
        {
            if (instance == null)
                instance = this;
        }

        public event Action<StringName, PressState> PressStateChanged;
        //public StringName[] Keybinds { get; protected set; } = new StringName[Enum.GetNames(typeof(Keybind)).Length];

        public List<StringName>[] Keybinds = new List<StringName>[Enum.GetNames(typeof(Keybind)).Length];

        public InputState InputState { get; protected set; }
        public Dictionary<StringName, PressState> ActionStates { get; protected set; } = new Dictionary<StringName, PressState>();
        public Vector2 MousePosition { get; protected set; } = new Vector2();

        public override void _Ready()
        {
            base._Ready();

            /* Example of adding keybinds:
             * Keybinds[(int)Keybind.InventoryToggle] = new StringName("I");
             * Keybinds[(int)Keybind.RunToggle] = new StringName("Control");
             * Keybinds[(int)Keybind.ConsoleToggle] = new StringName("~");
             */

            Keybinds[(int)Keybind.MoveLeft] = new List<StringName> { new StringName("A")};
            Keybinds[(int)Keybind.MoveRight] = new List<StringName> { new StringName("D") };
            Keybinds[(int)Keybind.MoveUp] = new List<StringName> { new StringName("W") };
            Keybinds[(int)Keybind.MoveDown] = new List<StringName> { new StringName("S") };
            Keybinds[(int)Keybind.ConsoleToggle] = new List<StringName> { new StringName("`") };
            Keybinds[(int)Keybind.ConsoleShift] = new List<StringName> { new StringName("Shift") };
            Keybinds[(int)Keybind.ConsoleRight] = new List<StringName> { new StringName("Right") };
            Keybinds[(int)Keybind.ConsoleLeft] = new List<StringName> { new StringName("Left") };
            Keybinds[(int)Keybind.ConsoleUp] = new List<StringName> { new StringName("Up") };
            Keybinds[(int)Keybind.ConsoleDown] = new List<StringName> { new StringName("Down") };
            Keybinds[(int)Keybind.Escape] = new List<StringName> { new StringName("Escape") };

            foreach (var item in InputMap.GetActions())
            {
                if (!item.ToString().Contains("ui_"))
                    ActionStates[item] = PressState.NotPressed;
            }

        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            foreach (var key in ActionStates.Keys)
            {
                if (Input.IsActionPressed(key))
                {
                    if (ActionStates[key] == PressState.NotPressed)
                    {
                        ActionStates[key] = PressState.JustPressed;
                        PressStateChanged?.Invoke(key, ActionStates[key]);
                    }
                    else if (ActionStates[key] == PressState.JustPressed)
                    {
                        ActionStates[key] = PressState.Pressed;
                        PressStateChanged?.Invoke(key, ActionStates[key]);
                    }
                }
                else
                {
                    if (ActionStates[key] == PressState.Reset)
                    {
                        ActionStates[key] = PressState.NotPressed;
                        PressStateChanged?.Invoke(key, ActionStates[key]);
                    }
                    if (ActionStates[key] == PressState.JustPressed)
                    {
                        ActionStates[key] = PressState.JustReleased;
                        PressStateChanged?.Invoke(key, ActionStates[key]);
                    }
                    else if (ActionStates[key] == PressState.Pressed)
                    {
                        ActionStates[key] = PressState.JustReleased;
                        PressStateChanged?.Invoke(key, ActionStates[key]);
                    }
                    else if (ActionStates[key] == PressState.JustReleased)
                    {
                        ActionStates[key] = PressState.NotPressed;
                        PressStateChanged?.Invoke(key, ActionStates[key]); ;
                    }
                }
            }
        }

        public void SetInputState(InputState state)
        {
            InputState = state;
            ResetActionInput();
        }

        private bool InCorrectState(InputState state) { return state == InputState; }

        public bool ActionPressed(Keybind keybind, InputState inputState)
        {
            if (!InCorrectState(inputState))
                return false;

            for (int i = 0; i < Keybinds[(int)keybind].Count; i++)
            {
                if (ActionStates[Keybinds[(int)keybind][i]] == PressState.Pressed)
                        return true;
            }
            return false;
        }
        public bool ActionJustPressed(Keybind keybind, InputState inputState)
        {
            if (!InCorrectState(inputState))
                return false; 
            
            for (int i = 0; i < Keybinds[(int)keybind].Count; i++)
            {
                if (ActionStates[Keybinds[(int)keybind][i]] == PressState.JustPressed)
                    return true;
            }
            return false;
        }
        public bool ActionJustReleased(Keybind keybind, InputState inputState)
        {
            if (!InCorrectState(inputState))
                return false;

            for (int i = 0; i < Keybinds[(int)keybind].Count; i++)
            {
                if (ActionStates[Keybinds[(int)keybind][i]] == PressState.JustReleased)
                    return true;
            }
            return false;
        }

        public void ResetActionInput()
        {
            foreach (var action in ActionStates.Keys)
                ActionStates[action] = PressState.Reset;
        }

    }

    public enum PressState
    {
        NotPressed,
        JustPressed,
        Pressed,
        JustReleased,
        Reset
    }

}
