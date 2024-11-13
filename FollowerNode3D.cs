using Godot;
using System;

public partial class FollowerNode3D : Node3D
{
	public Node3D FollowNode { get; protected set; }

    public void SetFollowNode(Node3D node)
    {
        FollowNode = node;
        if (FollowNode.IsValid())
            GlobalPosition = FollowNode.GlobalPosition;
        else
            GlobalPosition = Vector3.Zero;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (FollowNode.IsValid())
            GlobalPosition = FollowNode.GlobalPosition;
    }
}
