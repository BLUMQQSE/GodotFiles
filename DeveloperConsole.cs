using Godot;
using System;
using BMUtil;
using System.Collections.Generic;
using System.Text;
public partial class DeveloperConsole : Menu
{
    [Export] LineEdit InputBar { get; set; }
    [Export] RichTextLabel Display { get; set; }
    [Export] Container HintContainer { get; set; }  
    [Export] RichTextLabel HintDisplay { get; set; }

    Node ownerOfCommand;

    public event Action<string> CommandModified;
    public override void _Ready()
    {
        base._Ready();
        
        InputBar.TextSubmitted += TextEntered;
        InputBar.TextChanged += TextChanged;


        //CloseRequested += Close;
        if (!ownerOfCommand.IsValid())
            ownerOfCommand = GetTree().CurrentScene;


        GetTree().NodeRemoved += OnNodeRemoved;

        MenuActivated += Open;
        MenuDeactivated += Close;
        Close(this);

    }

    private void OnNodeRemoved(Node node)
    {
        if(OwnerWasRemoved(node))
        {
            ownerOfCommand = GetTree().CurrentScene;
            NodeFocusChange();
        }
    }


    public override void _Process(double delta)
    {
        base._Process(delta);
        if (InputManager.Instance.ActionJustPressed(Keybind.ConsoleToggle, InputState.UI) && Active)
        {
            MenuManager.Instance.Toggle("DeveloperConsole");
        }
        if (Active)
        {
            if (InputManager.Instance.ActionJustPressed(Keybind.ConsoleUp, InputState.UI) &&
                InputManager.Instance.ActionPressed(Keybind.ConsoleShift, InputState.UI))
            {
                int index = ownerOfCommand.GetIndex();

                if (index != 0)
                {
                    ownerOfCommand = ownerOfCommand.GetParent().GetChild(index - 1);
                }
                NodeFocusChange();
            }
            else if (InputManager.Instance.ActionJustPressed(Keybind.ConsoleLeft, InputState.UI) &&
                InputManager.Instance.ActionPressed(Keybind.ConsoleShift, InputState.UI))
            {
                if (ownerOfCommand != GetTree().Root)
                    ownerOfCommand = ownerOfCommand.GetParent();
                NodeFocusChange();
            }
            else if (InputManager.Instance.ActionJustPressed(Keybind.ConsoleDown, InputState.UI) &&
                InputManager.Instance.ActionPressed(Keybind.ConsoleShift, InputState.UI))
            {
                int index = ownerOfCommand.GetIndex();
                if (index != ownerOfCommand.GetParent().GetChildCount() - 1 && ownerOfCommand != GetTree().Root)
                {
                    ownerOfCommand = ownerOfCommand.GetParent().GetChild(index + 1);
                }
                NodeFocusChange();
            }
            else if (InputManager.Instance.ActionJustPressed(Keybind.ConsoleRight, InputState.UI) &&
                InputManager.Instance.ActionPressed(Keybind.ConsoleShift, InputState.UI))
            {
                if (ownerOfCommand.GetChildCount() > 0)
                {
                    ownerOfCommand = ownerOfCommand.GetChild(0);
                }
                NodeFocusChange();
            }
        }
        else if (InputManager.Instance.ActionJustPressed(Keybind.ConsoleToggle, InputState.Gameplay))
        {
            MenuManager.Instance.Toggle("DeveloperConsole");
        }
    }

    public void SetOwnerOfCommand(Node owner)
    {
        ownerOfCommand = owner;
    }

    private void NodeFocusChange()
    {
        InputBar.Text = "[" + ownerOfCommand.Name + "] ";
        InputBar.CaretColumn = InputBar.Text.Length;


        HandleHints(PossibleCommands(""));
    }

    private void Open(Menu menu)
    {
        InputBar.GrabFocus();
        NodeFocusChange();
    }

    private void Close(Menu menu)
    {
        InputBar.ReleaseFocus();
    }

    private void TextChanged(string newText)
    {
        
        if(newText.Count("(") > newText.Count(")"))
        {
            var c = InputBar.CaretColumn;
            if (c == newText.Length)
                InputBar.Text += ")";
            else
                InputBar.Text = InputBar.Text.Insert(c, ")");

            InputBar.CaretColumn = c;
        }
        else if(newText.Count(")") > newText.Count("("))
        {
            var c = InputBar.CaretColumn;
            InputBar.Text.Find(")", c);
            InputBar.Text = InputBar.Text.Remove(c, 1);
            InputBar.CaretColumn = c;
        }
        if (newText.Count("[") > newText.Count("]"))
        {
            var c = InputBar.CaretColumn;
            InputBar.Text = InputBar.Text.Insert(c, "]");
            InputBar.CaretColumn = c;
        }
        
        string command = newText.Substring(newText.Find("]") + 1).Trim();

        List<string> possibleCommands = PossibleCommands(command);
        HandleHints(possibleCommands);
    }

    void HandleHints(List<string> possibleCommands)
    {
        HintDisplay.Text = "";
        if (possibleCommands.Count == 0)
            HintContainer.Visible = false;
        else
            HintContainer.Visible = true;

        if (possibleCommands.Count < 4)
        {
            HintContainer.Size = new Vector2(HintContainer.Size.X, possibleCommands.Count * 25);
        }
        else
            HintContainer.Size = new Vector2(HintContainer.Size.X, 100);

        foreach(string command in possibleCommands)
        {
            HintDisplay.Text += command + '\n'; 
        }

    }

    List<string> PossibleCommands(string command)
    {
        List<string> result = new List<string>();

        if(command.Contains("("))
            command = command.Substr(0, command.IndexOf('('));

        var methodInfo = ownerOfCommand.GetMethodList();
        foreach (var method in methodInfo)
        {
            string potentialCommand = method["name"].AsString();
            if (potentialCommand.Length < command.Length) continue;

            if (!potentialCommand.StartsWith(command)) continue;
            if (potentialCommand.Contains('_')) continue;
            if (!char.IsUpper(potentialCommand[0])) continue;

            potentialCommand += "(";

            var methodArgs = method["args"].AsGodotArray<Godot.Collections.Dictionary>();
            foreach (var arg in methodArgs)
            {
                potentialCommand += (Variant.Type)(int)arg["type"];
                potentialCommand += ",";
            }
            potentialCommand = potentialCommand.Trim(',');
            potentialCommand += ")";

            result.Add(potentialCommand);
        }
        return result;
    }

    private void TextEntered(string newText)
    {
        if (newText.Length == 0)
            return;

        
        Display.Text += newText + "\n";
        HandleCommand(newText.Substring(newText.Find("]") + 1).Trim());

        NodeFocusChange();
    }


    private void HandleCommand(string message)
    {
        if (message.ToLower().Equals("save"))
        {
            SaveManager.Instance.SaveAll();
            return;
        }
        if (message.ToLower() == "clear")
        {
            Clear();
            return;
        }

        int index = message.IndexOf("(");
        if (index == -1)
        {
            if (ownerOfCommand.HasMethod(message))
            {
                if (ownerOfCommand.GetMethodArgumentCount(message) == 0)
                {
                    ownerOfCommand.Call(message);
                }
                else
                    GD.Print("[Error] " + message + " invalid parameter count");
            }
            else
                GD.Print("[Error] " + message + "does not exist on " + ownerOfCommand.Name);

            return;
        }
        string methodName = message.Substr(0, index);

        message = message.Substring(index + 1);
        message = message.TrimEnd(')');

        List<string> args = SplitComma(message);


        Godot.Collections.Array varArgs = new Godot.Collections.Array();

        for (int i = 0; i < args.Count; i++)
        {
            varArgs.Add(ConvertToVar(args[i]));
        }

        // convert string to vars
        if (ownerOfCommand.HasMethod(methodName))
        {
            if (ownerOfCommand.GetMethodArgumentCount(methodName) == varArgs.Count)
                ownerOfCommand.Callv(methodName, varArgs);
            else
                GD.Print("[Error] " + methodName + " invalid parameter count");
        }
        else
            GD.Print("[Error] " + methodName + "does not exist on " + ownerOfCommand.Name);

    }


    private List<string> SplitComma(string message)
    {
        List<string> result = new List<string>();
        int index = 0;
        bool in_para = false;
        bool in_square = false;
        bool in_string = false;
        StringBuilder sb = new StringBuilder();
        while (index <= message.Length-1)
        {
            if (message[index] == '\"')
                in_string = !in_string;
            if (message[index] == '(')
                in_para = true;
            if (message[index] == ')')
                in_para = false;
            if (message[index] == '[')
                in_square = true;
            if (message[index] == ']')
                in_square = false;
            if (message[index] == ',' && !in_square && !in_para)
            {
                string value = sb.ToString();
                result.Add(value);
                sb.Clear();
                index++;
                continue;
            }
            if (message[index] == ' ' && !in_string)
            {
                index++;
                continue;
            }
            
            sb.Append(message[index]);
            index++;
        }

        result.Add(sb.ToString());

        return result;
    }

    private Variant ConvertToVar(string value)
    {
        if (value[0] == '\"')
        {
            return value;
        }
        else if (value.Contains("Vector3"))
        {
            value = value.Substring(value.IndexOf('(') + 1);
            value = value.TrimEnd(')');

            string[] args = value.Split(',');

            return new Vector3(float.Parse(args[0]), float.Parse(args[1]), float.Parse(args[2]));
        }
        else if (value.Contains("Vector2"))
        {
            value = value.Substring(value.IndexOf('(') + 1);
            value = value.TrimEnd(')');

            string[] args = value.Split(',');
            return new Vector2(float.Parse(args[0]), float.Parse(args[1]));
        }
        else if (value.StartsWith('['))
        {
            string s = value.TrimStart('[');
            s = s.TrimEnd(']');
            uint id = Convert.ToUInt32(s);
            return NetworkManager.Instance.UniqueIdToNode(id);
        }
        else if (value.StartsWith('#'))
        {
            string path = value.Trim('#');
            return ResourceManager.Instance.GetResourceByName<Resource>(path);
        }
        else if (value.Contains('.'))
        {
            return Convert.ToDouble(value);
        }
        else if (value.ToLower() == "false" || value.ToLower() == "true")
        {
            if (value.ToLower() == "false")
                return false;
            else
                return true;
        }
        else
        {
            return Convert.ToInt32(value);
        }
    }

    private void Clear()
    {
        Display.Text = "";
    }

    private bool OwnerWasRemoved(Node n)
    {
        if (n == ownerOfCommand)
            return true;

        foreach (var m in n.GetChildren())
        {
            if (m == ownerOfCommand)
                return true;
        }
        return false;
    }
}
