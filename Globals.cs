using Godot;
using Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using static Globals;

public static class StringExtensions
{
    public static string RemovePathAndFileType(this string path)
    {
        path = path.Substring(path.RFind("/") + 1);
        int index = path.LastIndexOf('.');
        return path.Substring(0, index);
    }   
}


public static class NodeExtensions
{
    public static bool IsValid<T>(this T node) where T : GodotObject
    {
        return GodotObject.IsInstanceValid(node);
    }
}
public static class Globals
{
    public static string RemoveNamespace(string name)
    {
        int index = name.RFind(".");
        if (index < 0)
            return name;
        else
            return name.Substring(index + 1, name.Length - (index + 1));
    }

    public static bool IsOwnedBy(this Node node, Node potentialOwner)
    {
        Node temp = node;
        while (temp != potentialOwner.GetTree().Root)
        {
            if (temp.GetParent() == potentialOwner)
                return true;
            temp = node.GetParent();
        }
        return false;
    }

    public static bool Owns(this Node node, Node potentiallyOwned)
    {
        return potentiallyOwned.IsOwnedBy(node);
    }

    public static T FindParentOfType<T>(this Node node)
    {
        return FindParentOfTypeHelper<T>(node.GetParent());
    }

    private static T FindParentOfTypeHelper<T>(this Node node)
    {
        if (node == null)
            return default(T);
        if (node is T)
        {
            return (T)(object)node;
        }
        else if (node == node.GetTree().Root)
        {
            return default(T);
        }
        else
        {
            return FindParentOfTypeHelper<T>(node.GetParent());
        }
    }

    /// <summary>
    /// Function for searching for child node of Type T. Removes need for searching for a
    /// specific name of a node, reducing potential errors in name checking being inaccurate.
    /// Supports checking 5 layers of nodes. This method is ineffecient, and should never be used repetitively 
    /// in _process.
    /// </summary>
    /// <returns>First instance of Type T</returns>
    public static T GetChildOfType<T>(this Node node)
    {
        if (node == null)
            return default(T);

        foreach (Node child in node.GetChildren())
            if (child is T)
                return (T)(object)child;

        return default(T);
    }

    public static List<T> GetChildren<T>(this Node node)
    {
        List<T> result = new List<T>();
        for (int i = 0; i < node.GetChildCount(); i++)
            if (node.GetChild(i) is T)
                result.Add((T)(object)node.GetChild(i));
        return result;
    }

    /// <summary>
    /// Function for searching for children nodes of Type T.
    /// </summary>
    /// <returns>List of all instances of Type T that are children or lower.</returns>
    public static List<T> GetAllChildren<T>(this Node node)
    {
        List<T> list = new List<T>();
        list.AddRange(GetChildren<T>(node));
        for (int i = node.GetChildCount() - 1; i >= 0; i--)
            list.AddRange(GetAllChildren<T>(node.GetChild(i)));

        return list;
    }

    public static List<Node> GetAllChildren(this Node node)
    {
        return GetAllChildren<Node>(node);
    }

    /// <summary>
    /// Function for searching for sibling node of Type T. Removes need for searching for a
    /// specific name of a node, reducing potential errors in name checking being inaccurate.
    /// </summary>
    /// <returns>First instance of Type T</returns>
    public static T GetSibling<T>(this Node node)
    {
        return node.GetParent().GetChildOfType<T>();
    }
    /// <summary>
    /// Function for searching for sibling nodes of Type T.
    /// </summary>
    /// <returns>List of all instances of Type T</returns>
    public static List<T> GetSiblings<T>(this Node node)
    {
        return node.GetParent().GetChildren<T>();
    }


}
