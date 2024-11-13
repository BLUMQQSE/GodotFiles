using Godot;
using System;
using System.Collections.Generic;


namespace BMUtil
{
    public class ServerToClientAttribute : Attribute { }
    public class ClientToServerAttribute : Attribute { }

    enum DataType
    {
        RpcCall,
        ClientInputUpdate,
        ServerUpdate,
        FullServerData,
        ServerAdd,
        ServerRemove,
        RequestForceUpdate
    }
    public abstract partial class NetworkManager : Node
    {
        protected static NetworkManager instance;
        public static NetworkManager Instance { get { return instance; } }

        public NetworkManager()
        {
            if (instance == null)
                instance = this;
        }

        public event Action<Node> ServerNodeAdded;
        public event Action<Node> ServerNodeRemoved;
        public event Action<Node> LocalNodeAdded;
        public event Action<Node> LocalNodeRemoved;
        public event Action<Node> PlayerAdded;
        public event Action<Node> PlayerRemoved;

        #region OwnerId

        protected Dictionary<string, Node> OwnerIdToPlayer { get; set; } = new Dictionary<string, Node>();
        protected void MakePlayer(string ownerId, Node playerNode)
        {
            playerNode.Name = ownerId;
            playerNode.AddToGroup(PlayerGroup);
            playerNode.SetMeta(OwnerIdMeta, ownerId);
            OwnerIdToPlayer.Add(ownerId, playerNode);
            PlayerAdded?.Invoke(playerNode);
        }

        protected void RemovePlayer(string ownerId)
        {
            Node player = OwnerIdToPlayer[ownerId];
            OwnerIdToPlayer.Remove(ownerId);
            SafeQueueFree(player);
            PlayerRemoved?.Invoke(player);
        }

        public Node GetOwnerIdToPlayer(string id)
        {
            if (OwnerIdToPlayer.ContainsKey(id))
                return OwnerIdToPlayer[id];
            else
            {
                var playerNodes = GetTree().GetNodesInGroup(PlayerGroup);
                // attempt to find
                foreach (var player in playerNodes)
                {
                    if (player.GetMeta(OwnerIdMeta).ToString() == id)
                    {
                        OwnerIdToPlayer.Add(id, player);
                        return player;
                    }   
                }
                if (!IsServer)
                    RequestForceUpdate();

                return null;
            }
        }
        public void RemoveOwnerIdToPlayer(string id)
        {
            if (!OwnerIdToPlayer.ContainsKey(id))
                return;
            OwnerIdToPlayer.Remove(id);
        }

        #endregion
        protected TimeTracker UpdateTimer { get; set; } = new TimeTracker();
        public float UpdateIntervalInMil { get; protected set; } = 50f;

        #region Flags
        protected bool RecievedFullServerData = false;
        #endregion

        #region UniqueId

        public static readonly uint FirstAvailableSelfUniqueId = uint.MaxValue - 10000000;
        protected uint nextAvailableSelfUniqueId = FirstAvailableSelfUniqueId;
        protected uint nextAvailableUniqueId = 0;

        protected Dictionary<uint, Node> uniqueIdToNode = new Dictionary<uint, Node>();

        public event Action NetworkUpdate_Client;

        public Node UniqueIdToNode(uint id)
        {
            if (uniqueIdToNode.ContainsKey(id))
                return uniqueIdToNode[id];
            else if (!IsServer)
                RequestForceUpdate();
            return null;
        }

        public bool HasUniqueIdToNode(uint id)
        {
            return uniqueIdToNode.ContainsKey(id);
        }

        public void ApplyNextAvailableUniqueId(Node node)
        {
            uint result = nextAvailableUniqueId;
            nextAvailableUniqueId++;
            node.SetMeta(UniqueIdMeta, result.ToString());

            uniqueIdToNode[result] = node;
            
            foreach (Node child in node.GetChildren())
            {
                ApplyNextAvailableUniqueId(child);
            }
        }

        public void ApplyNextAvailableSelfUniqueId(Node node)
        {
            node.AddToGroup(LocalOnlyGroup);
           
            uint result = nextAvailableSelfUniqueId;
            nextAvailableSelfUniqueId++;
            node.SetMeta(UniqueIdMeta, result.ToString());

            uniqueIdToNode[result] = node;
            
            foreach (Node child in node.GetChildren())
            {
                ApplyNextAvailableSelfUniqueId(child);
            }
        }

        #endregion

        #region NetworkNodes

        protected List<INetworkData> NetworkNodes = new List<INetworkData>();
        public void AddNetworkNodes(Node node)
        {
            if (node is INetworkData nd && !node.IsInGroup(LocalOnlyGroup))
                if (!NetworkNodes.Contains(nd))
                    NetworkNodes.Add(nd);

            foreach (Node child in node.GetChildren())
                AddNetworkNodes(child);

        }
        public void RemoveNetworkNodes(Node node)
        {
            if (node is INetworkData nd)
            {
                if (NetworkNodes.Contains(nd))
                    NetworkNodes.Remove(nd);
            }
            foreach (Node child in node.GetChildren())
                RemoveNetworkNodes(child);
        }

        #endregion


        protected static readonly string _Add = "A";
        protected static readonly string _OwnerID = "OID";
        protected static readonly string _UniqueID = "UID";

        protected static readonly string _DataType = "DAT";
        protected static readonly string _NetworkNodes = "NN";

        public static readonly StringName UniqueIdMeta = new StringName("UID");
        public static readonly StringName OwnerIdMeta = new StringName("OID");
        public static readonly StringName LocalOnlyGroup = new StringName("LocalOnly");
        public static readonly StringName PlayerGroup = new StringName("Player");
        public static readonly StringName IgnoreChildrenGroup = new StringName("IgnoreChildren");

        public string PlayerName { get; protected set; }
        public string PlayerId { get; protected set; }

        public bool IsServer { get; protected set; } = true;
        public int Connections { get; protected set; }
        public int MaxConnections { get; protected set; }

        public abstract void SendToServer(JsonValue data, bool reliable = true);
        public abstract void SendToClientId(string clientIdentifier, JsonValue data, bool reliable = true);
        public abstract void SendToClients(JsonValue data, bool reliable = true);

        public override void _Ready()
        {
            base._Ready();
            UpdateTimer.Start();
            ApplyNextAvailableUniqueId(GetTree().Root);


            GetTree().NodeAdded += NodeAdded;
            GetTree().NodeRemoved += NodeRemoved;
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            if (UpdateTimer.ElapsedMilliseconds >= UpdateIntervalInMil)
            {
                UpdateTimer.Restart();
                if (IsServer)
                {
                    if (Connections < 2) return;
                    JsonValue scenelessData = new JsonValue();
                    scenelessData[_DataType].Set((int)DataType.ServerUpdate);
                    foreach (var n in NetworkNodes)
                    {
                        scenelessData[_NetworkNodes][(n as Node).GetMeta(UniqueIdMeta).ToString()]
                        .Set((n).SerializeNetworkData(false));
                    }
                    if (scenelessData[_NetworkNodes].ToString() != "{}" && scenelessData[_NetworkNodes].ToString() != "null")
                        SendToClients(scenelessData);
                }
                else if (RecievedFullServerData) //only start sending client data after recieving full server data
                {
                    NetworkUpdate_Client?.Invoke();
                }
            }
        }
        protected void ForceUpdateClients()
        {
            JsonValue scenelessData = new JsonValue();
            scenelessData[_DataType].Set((int)DataType.ServerUpdate);
            foreach (var n in NetworkNodes)
            {
                scenelessData[_NetworkNodes][(n as Node).GetMeta(UniqueIdMeta).ToString()]
                .Set((n).SerializeNetworkData(true));
            }
            if (scenelessData[_NetworkNodes].ToString() != "{}" && scenelessData[_NetworkNodes].ToString() != "null")
                SendToClients(scenelessData);
        }
        protected void SafeQueueFree(Node node)
        {
            node?.QueueFree();
        }

        protected void NodeRemoved(Node node)
        {
            if (!IsServer && !node.IsLocalNode())
            {
                return;
            }
            if (node.IsInGroup(LocalOnlyGroup))
            {
                LocalNodeRemoved?.Invoke(node);
                SafeQueueFree(node);
            }
            else
            {
                RemoveNetworkNodes(node);

                JsonValue data = new JsonValue();
                data["UniqueId"].Set(node.GetMeta(UniqueIdMeta).ToString());


                ServerNodeRemoved?.Invoke(node);

                SafeQueueFree(node);
                // tell all clients to queue free this node
                data[_DataType].Set((int)DataType.ServerRemove);

                SendToClients(data);
            }
        }

        protected void NodeAdded(Node node)
        {
            if (node == GetTree().CurrentScene)
                return;
            if (!IsServer && !node.IsLocalNode())
            {
                return;
            }
            Node parent = node.GetParent();

            if (node.IsLocalNode())
            {
                // guaranteed local
                ApplyNextAvailableSelfUniqueId(node);
                node.AddToGroup(LocalOnlyGroup);
                LocalNodeAdded?.Invoke(node);
            }
            else
            {
                // TODO: Add as server node
                ApplyNextAvailableUniqueId(node);
                AddNetworkNodes(node);

                ServerNodeAdded?.Invoke(node);

                JsonValue data = new JsonValue();

                data[_DataType].Set((int)DataType.ServerAdd);

                // need to collect all data about the node and send to clients
                data["Owner"].Set(node.GetParent().GetMeta(UniqueIdMeta).ToString());

                data["Node"].Set(ConvertNodeToJson(node));


                SendToClients(data);
            }
        }

        #region HelperFunctions

        protected void RequestForceUpdate()
        {
            JsonValue data = new JsonValue();
            data[_DataType].Set((int)DataType.RequestForceUpdate);
            SendToServer(data);
        }

        public void AddNodeToUniqueIdDict(Node node)
        {
            string uniqueStr = node.GetMeta(UniqueIdMeta).ToString();
            uint id = uint.Parse(uniqueStr);
            uniqueIdToNode[id] = node;
            foreach (Node n in node.GetChildren())
                AddNodeToUniqueIdDict(n);
        }
        protected bool SearchForNode(string id, ref Node reference, Node searchPoint)
        {
            if (searchPoint.GetMeta(UniqueIdMeta).ToString() == id)
            {
                reference = searchPoint;
                return true;
            }
            foreach (Node child in searchPoint.GetChildren())
            {
                if (SearchForNode(id, ref reference, child))
                    return true;
            }

            return false;
        }

        #endregion

        #region Server
        protected void SocketDataRecieved(JsonValue value)
        {
            DataType dataType = (DataType)value[_DataType].AsInt();

            switch (dataType)
            {
                case DataType.RpcCall:
                    HandleRpc(value);
                    break;
                case DataType.RequestForceUpdate:
                    ForceUpdateClients();
                    break;
            }
        }

        protected JsonValue FullServerData()
        {
            JsonValue data = new JsonValue();
            data[_DataType].Set((int)DataType.FullServerData);

            foreach (Node child in GetTree().CurrentScene.GetChildren())
            {
                JsonValue nodeData = ConvertNodeToJson(child);
                data["Nodes"].Append(nodeData);
                AddNodeToUniqueIdDict(child);
            }

            return data;
        }

        #endregion

        #region Client
        protected void ConnectionDataRecieved(JsonValue value)
        {
            
            DataType dataType = (DataType)value[_DataType].AsInt();

            switch (dataType)
            {
                case DataType.ServerUpdate:
                    HandleServerUpdate(value);
                    break;
                case DataType.RpcCall:
                    HandleRpc(value);
                    break;
                case DataType.ServerAdd:
                    HandleServerAdd(value);
                    break;
                case DataType.ServerRemove:
                    // TODO: Add logic to find node of unique

                    string uniqueIdStr = value["UniqueId"].AsString();
                    uint uniqueId = uint.Parse(uniqueIdStr);
                    Node removeNode = uniqueIdToNode[uniqueId];
                    SafeQueueFree(removeNode);
                    uniqueIdToNode.Remove(uniqueId);
                    break;
                case DataType.FullServerData:
                    HandleFullServerData(value);
                    break;
            }
        }


        private void HandleServerAdd(JsonValue data)
        {
            if (!RecievedFullServerData)
                return;


            // Client does not know who owner is, so we'll ignore this add for now
            // currently an issue when player first joins
            if (!uniqueIdToNode.ContainsKey(Convert.ToUInt32(data["Owner"].AsString())))
            {
                GD.Print("HandleServerAdd: We dont know ID: " + data["Owner"].AsString());
                return;
            }

            string uniqueIdStr = data["Node"][_Meta][UniqueIdMeta].AsString();
            uint uId = Convert.ToUInt32(uniqueIdStr);
            if (uniqueIdToNode.ContainsKey(uId))
            {
                GD.Print("I already know about this node?");
                // client already knows about this object, dont add again
                return;
            }

            Node node = ConvertJsonToNode(data["Node"]);
            Node owner = uniqueIdToNode[Convert.ToUInt32(data["Owner"].AsString())];

            AddNodeToUniqueIdDict(node);
            if (node.IsInGroup(PlayerGroup))
                OwnerIdToPlayer.Add(node.GetMeta(OwnerIdMeta).ToString(), node);

            owner.CallDeferred(_AddChild, node, true);
        }

        private void HandleServerUpdate(JsonValue data)
        {
            if (!RecievedFullServerData) return;
            // first we verify we have all these nodes in our instance
            foreach (var item in data[_NetworkNodes].Object)
            {
                if (!uniqueIdToNode.ContainsKey(Convert.ToUInt32(item.Key)))
                {
                    Node n = null;
                    bool found = SearchForNode(item.Key, ref n, GetTree().Root);

                    if (!found)
                        return;
                    else
                        uniqueIdToNode[Convert.ToUInt32(item.Key)] = n;
                }

            }

            foreach (var item in data[_NetworkNodes].Object)
            {
                INetworkData n = uniqueIdToNode[Convert.ToUInt32(item.Key)] as INetworkData;

                if (n == null)
                {
                    return;
                }
                n.DeserializeNetworkData(item.Value);
            }
        }


        private void HandleFullServerData(JsonValue data)
        {
            RecievedFullServerData = true;

            foreach (var item in data["Nodes"].Array)
            {
                Node node = ConvertJsonToNode(item);
                uint id = Convert.ToUInt32(node.GetMeta(UniqueIdMeta).ToString());

                /*
                 * Steps:
                 *  1) Determine if we already know about the node
                 *      1a) If true, we need to check about its children
                 *      1b) if false, add the node and move on
                 */

                if (uniqueIdToNode.ContainsKey(id))
                {
                    // recursively find
                    FindAndAdd(UniqueIdToNode(id), node);
                }
                else
                {
                    //GD.Print(node.Name);
                    // from here we can assume this is a parent node in the main scene
                    GetTree().CurrentScene.AddChild(node);
                    AddNodeToUniqueIdDict(node);
                }

                if (node.IsInGroup(PlayerGroup))
                    OwnerIdToPlayer.Add(node.GetMeta(OwnerIdMeta).ToString(), node);
            }
        }

        private void FindAndAdd(Node addTo, Node potentialAdd)
        {
            foreach (var child in potentialAdd.GetChildren())
            {
                uint id = Convert.ToUInt32(child.GetMeta(UniqueIdMeta).ToString());

                if (uniqueIdToNode.ContainsKey(id))
                {
                    FindAndAdd(UniqueIdToNode(id), child);
                }
                else
                {
                    Node n = child.Duplicate();
                    addTo.AddChild(n);
                    AddNodeToUniqueIdDict(n);
                }
            }
        }


        #endregion


        #region RPC
        public void RpcServer(Node caller, string methodName, params Variant[] param)
        {
            JsonValue message = new JsonValue();
            message[_DataType].Set((int)DataType.RpcCall);
            message["Caller"].Set(caller.GetMeta(UniqueIdMeta).AsString());
            message["MethodName"].Set(methodName);

            foreach (Variant variant in param)
            {
                message["Params"].Append(VariantToJson(variant));
            }

            SendToServer(message);
        }

        public void RpcClients(Node caller, string methodName, params Variant[] param)
        {
            JsonValue message = new JsonValue();
            message[_DataType].Set((int)DataType.RpcCall);
            message["Caller"].Set((string)caller.GetMeta(UniqueIdMeta));
            message["MethodName"].Set(methodName);

            foreach (Variant variant in param)
            {
                message["Params"].Append(VariantToJson(variant));
            }

            SendToClients(message);
        }

        public void RpcClient(string ownerId, Node caller, string methodName, params Variant[] param)
        {
            JsonValue message = new JsonValue();
            message[_DataType].Set((int)DataType.RpcCall);
            message["Caller"].Set((string)caller.GetMeta(UniqueIdMeta));
            message["MethodName"].Set(methodName);

            foreach (Variant variant in param)
            {
                message["Params"].Append(VariantToJson(variant));
            }

            SendToClientId(ownerId, message);
        }

        /// <summary>
        /// Server Handles an Rpc call
        /// </summary>
        protected void HandleRpc(JsonValue value)
        {
            Node node = uniqueIdToNode[uint.Parse(value["Caller"].AsString())];
            if (!node.IsValid())
            {
                GD.Print("Server does not recognize node with ID: " + value["Caller"].AsString());
                return;
            }
            string methodName = value["MethodName"].AsString();

            List<Variant> args = new List<Variant>();
            foreach (JsonValue variant in value["Params"].Array)
            {
                args.Add(JsonToVariant(variant));
            }

            node.Call(methodName, args.ToArray());
        }
        #endregion

        #region JsonToVariant

        /// <summary>
        /// Converts a JsonValue object to a Variant. The JsonValue must be in the same format
        /// as provided from JsonToVarient.
        /// </summary>
        public Variant JsonToVariant(JsonValue value)
        {
            Variant result = new Variant();

            Variant.Type type = StringToVariantType(value["Type"].AsString());

            if (type == Variant.Type.Int)
            {
                int num = Convert.ToInt32(value["Value"].AsString());
                result = num;
            }
            else if (type == Variant.Type.Float)
            {
                double num = Convert.ToDouble(value["Value"].AsString());
                result = num;
            }
            else if (type == Variant.Type.Bool)
            {
                bool boolVal = Convert.ToBoolean(value["Value"].AsString());
                result = boolVal;
            }
            else if (type == Variant.Type.String)
            {
                string strVal = Convert.ToString(value["Value"].AsString());
                result = strVal;
            }
            else if (type == Variant.Type.Vector2)
            {
                Vector2 vec = new Vector2();
                vec.X = (float)JsonToVariant(value["Value"]["X"]);
                vec.Y = (float)JsonToVariant(value["Value"]["Y"]);
                result = vec;
            }
            else if (type == Variant.Type.Vector3)
            {
                Vector3 vec = new Vector3();
                vec.X = (float)JsonToVariant(value["Value"]["X"]);
                vec.Y = (float)JsonToVariant(value["Value"]["Y"]);
                vec.Z = (float)JsonToVariant(value["Value"]["Z"]);
                result = vec;
            }
            else if (type == Variant.Type.Array)
            {
                Godot.Collections.Array ar = new Godot.Collections.Array();
                foreach (JsonValue arrayElement in value["ArrayElements"].Array)
                {
                    ar.Add(JsonToVariant(arrayElement));
                }
                result = ar;
            }
            else if (type == Variant.Type.Dictionary)
            {
                Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();

                foreach (JsonValue dictElement in value["DictElements"].Array)
                {
                    dict.Add(JsonToVariant(dictElement["Key"]),
                        JsonToVariant(dictElement["Value"]));
                }
                result = dict;
            }
            else if (type == Variant.Type.Object)
            {
                Node n = UniqueIdToNode(Convert.ToUInt32(value["UniqueId"].AsString()));
                result = n;
            }

            return result;
        }
        /// <summary>
        /// Converts a Variant to a JsonValue object. 
        /// </summary>
        public JsonValue VariantToJson(Variant variant)
        {
            JsonValue result = new JsonValue();

            switch (variant.VariantType)
            {
                case Variant.Type.Int:
                case Variant.Type.Float:
                case Variant.Type.Bool:
                case Variant.Type.String:
                    {
                        result["Type"].Set(VariantTypeToString(variant.VariantType));
                        result["Value"].Set(variant.ToString());
                    }
                    break;
                case Variant.Type.Vector2:
                    {
                        result["Type"].Set(VariantTypeToString(Variant.Type.Vector2));
                        Vector2 vector = (Vector2)variant;
                        result["Value"]["X"]["Value"].Set(vector.X);
                        result["Value"]["X"]["Type"].Set(Variant.Type.Float.ToString());
                        result["Value"]["Y"]["Value"].Set(vector.Y);
                        result["Value"]["Y"]["Type"].Set(Variant.Type.Float.ToString());
                    }
                    break;
                case Variant.Type.Vector3:
                    {
                        result["Type"].Set(VariantTypeToString(Variant.Type.Vector3));
                        Vector3 vector = (Vector3)variant;
                        result["Value"]["X"]["Value"].Set(vector.X);
                        result["Value"]["X"]["Type"].Set(Variant.Type.Float.ToString());
                        result["Value"]["Y"]["Value"].Set(vector.Y);
                        result["Value"]["Y"]["Type"].Set(Variant.Type.Float.ToString());
                        result["Value"]["Z"]["Value"].Set(vector.Z);
                        result["Value"]["Z"]["Type"].Set(Variant.Type.Float.ToString());
                    }
                    break;
                case Variant.Type.Array:
                    {
                        result["Type"].Set(VariantTypeToString(Variant.Type.Array));
                        Godot.Collections.Array ar = (Godot.Collections.Array)variant;
                        foreach (Variant var in ar)
                            result["ArrayElements"].Append(VariantToJson(var));

                    }
                    break;
                case Variant.Type.Dictionary:
                    {
                        result["Type"].Set(VariantTypeToString(Variant.Type.Dictionary));
                        Godot.Collections.Dictionary dict = (Godot.Collections.Dictionary)variant;
                        foreach (KeyValuePair<Variant, Variant> var in dict)
                        {
                            JsonValue dictPart = new JsonValue();
                            dictPart["Key"] = VariantToJson(var.Key);
                            dictPart["Value"] = VariantToJson(var.Value);
                            result["DictElements"].Append(dictPart);
                        }
                    }
                    break;
                case Variant.Type.Object:
                    {
                        Node n = (Node)variant;
                        result["Type"].Set("Node");
                        result["UniqueId"].Set((string)n.GetMeta(UniqueIdMeta));
                    }
                    break;
            }

            return result;

        }
        protected Variant.Type StringToVariantType(string str)
        {
            if (str == Variant.Type.Int.ToString())
                return Variant.Type.Int;
            if (str == Variant.Type.Float.ToString())
                return Variant.Type.Float;
            if (str == Variant.Type.Bool.ToString())
                return Variant.Type.Bool;
            if (str == Variant.Type.String.ToString())
                return Variant.Type.String;
            if (str == Variant.Type.Array.ToString())
                return Variant.Type.Array;
            if (str == Variant.Type.Dictionary.ToString())
                return Variant.Type.Dictionary;
            if (str == Variant.Type.Vector2.ToString())
                return Variant.Type.Vector2;
            if (str == Variant.Type.Vector3.ToString())
                return Variant.Type.Vector3;
            if (str == "Node")
                return Variant.Type.Object;

            return Variant.Type.Nil;
        }
        protected string VariantTypeToString(Variant.Type type)
        {
            switch (type)
            {
                case Variant.Type.Int: return Variant.Type.Int.ToString();
                case Variant.Type.Float: return Variant.Type.Float.ToString();
                case Variant.Type.String: return Variant.Type.String.ToString();
                case Variant.Type.Bool: return Variant.Type.Bool.ToString();
                case Variant.Type.Vector2: return Variant.Type.Vector2.ToString();
                case Variant.Type.Vector3: return Variant.Type.Vector3.ToString();
                case Variant.Type.Dictionary: return Variant.Type.Dictionary.ToString();
                case Variant.Type.Array: return Variant.Type.Array.ToString();
                case Variant.Type.Object: return "Node";

            }
            return Variant.Type.Nil.ToString();
        }


        #endregion

        #region NodeJsonConversion
        protected static readonly string _Name = "N";
        protected static readonly string _Type = "T";
        protected static readonly string _DerivedType = "DT";
        protected static readonly string _Position = "P";
        protected static readonly string _Rotation = "R";
        protected static readonly string _Scale = "S";
        protected static readonly string _Size = "SZ";
        protected static readonly string _ZIndex = "ZI";
        protected static readonly string _ZIsRelative = "ZIR";
        protected static readonly string _YSortEnabled = "YSE";
        protected static readonly string _Meta = "M";
        protected static readonly string _Group = "G";
        protected static readonly string _Children = "C";
        protected static readonly string _INetworkData = "IND";
        protected static readonly StringName _AddChild = "add_child";

        private static readonly string _MinSize = "MS";
        private static readonly string _AnchorLeft = "AL";
        private static readonly string _AnchorRight = "AR";
        private static readonly string _AnchorBottom = "AB";
        private static readonly string _AnchorTop = "AT";
        private static readonly string _AnchorPreset = "AP";
        private static readonly string _OffsetLeft = "OL";
        private static readonly string _OffsetRight = "OR";
        private static readonly string _OffsetTop = "OT";
        private static readonly string _OffsetBottom = "OB";
        private static readonly string _PivotOffset = "PO";
        private static readonly string _Theme = "THM";
        private static readonly string _LayoutMode = "LM";
        private static readonly string _LayoutDirection = "LD";

        public static JsonValue ConvertNodeToJson(Node node)
        {
            JsonValue val = CollectNodeData(node);
            return val;
        }

        protected static JsonValue CollectNodeData(Node node)
        {
            JsonValue jsonNode = new JsonValue();

            if (node.IsInGroup(LocalOnlyGroup))
                return new JsonValue();
            if (node.GetParent().IsValid())
                if (node.GetParent().IsInGroup(IgnoreChildrenGroup))
                    return new JsonValue();

            jsonNode[_Name].Set(node.Name);
            jsonNode[_Type].Set(RemoveNamespace(node.GetType().ToString()));
            jsonNode[_DerivedType].Set(node.GetClass());
            if (node is Node2D)
            {
                Node2D node2d = (Node2D)node;
                jsonNode[_ZIsRelative].Set(node2d.ZAsRelative);
                jsonNode[_YSortEnabled].Set(node2d.YSortEnabled);
                jsonNode[_ZIndex].Set(node2d.ZIndex);

                jsonNode[_Position].Set(node2d.Position);
                jsonNode[_Rotation].Set(node2d.Rotation);
                jsonNode[_Scale].Set(node2d.Scale);
            }

            else if (node is Control c)
            {
                jsonNode[_Position].Set(c.Position);
                jsonNode[_Rotation].Set(c.Rotation);
                jsonNode[_Scale].Set(c.Scale);
                jsonNode[_Size].Set(c.Size);

                jsonNode[_MinSize].Set(c.CustomMinimumSize);
                jsonNode[_LayoutMode].Set(c.LayoutMode);
                jsonNode[_LayoutDirection].Set((int)c.LayoutDirection);
                jsonNode[_AnchorLeft].Set(c.AnchorLeft);
                jsonNode[_AnchorRight].Set(c.AnchorRight);
                jsonNode[_AnchorBottom].Set(c.AnchorBottom);
                jsonNode[_AnchorTop].Set(c.AnchorTop);
                jsonNode[_ZIndex].Set(c.ZIndex);
                jsonNode[_ZIsRelative].Set(c.ZAsRelative);
                jsonNode[_AnchorPreset].Set(c.AnchorsPreset);
                jsonNode[_OffsetLeft].Set(c.OffsetLeft);
                jsonNode[_OffsetRight].Set(c.OffsetRight);
                jsonNode[_OffsetTop].Set(c.OffsetTop);
                jsonNode[_OffsetBottom].Set(c.OffsetBottom);
                jsonNode[_PivotOffset].Set(c.PivotOffset);
                if (c.Theme.IsValid())
                {
                    jsonNode[_Theme].Set(c.Theme.ResourcePath);
                }
            }
            else if (node is Node3D)
            {
                Node3D node3d = (Node3D)node;

                jsonNode[_Position].Set(node3d.Position);
                jsonNode[_Rotation].Set(node3d.Rotation);
                jsonNode[_Scale].Set(node3d.Scale);

            }

            foreach (string meta in node.GetMetaList())
            {
                jsonNode[_Meta][meta].Set(node.GetMeta(meta).AsString());
            }
            foreach (string group in node.GetGroups())
                jsonNode[_Group].Append(group);

            for (int i = 0; i < node.GetChildCount(); i++)
                jsonNode[_Children].Append(CollectNodeData(node.GetChild(i)));

            if (node is INetworkData)
                jsonNode[_INetworkData].Set((node as INetworkData).SerializeNetworkData(true, true));

            return jsonNode;
        }

        public static Node ConvertJsonToNode(JsonValue data)
        {
            Node node = (Node)ClassDB.Instantiate(data[_DerivedType].AsString());

            // Set Basic Node Data
            node.Name = data[_Name].AsString();
            if (node is Node2D)
            {
                Node2D node2d = (Node2D)node;

                node2d.Position = data[_Position].AsVector2();
                node2d.Rotation = data[_Rotation].AsFloat();
                node2d.Scale = data[_Scale].AsVector2();

                node2d.ZIndex = data[_ZIndex].AsInt();
                node2d.ZAsRelative = data[_ZIsRelative].AsBool();
                node2d.YSortEnabled = data[_YSortEnabled].AsBool();
            }
            else if (node is Control c)
            {
                c.Position = data[_Position].AsVector2();
                c.Rotation = data[_Rotation].AsFloat();
                c.Scale = data[_Scale].AsVector2();
                c.Size = data[_Size].AsVector2();

                c.ZAsRelative = data[_ZIsRelative].AsBool();
                c.ZIndex = data[_ZIndex].AsInt();
                c.CustomMinimumSize = data[_MinSize].AsVector2();
                c.LayoutMode = data[_LayoutMode].AsInt();
                c.LayoutDirection = (Control.LayoutDirectionEnum)data[_LayoutDirection].AsInt();
                c.AnchorLeft = data[_AnchorLeft].AsFloat();
                c.AnchorRight = data[_AnchorRight].AsFloat();
                c.AnchorBottom = data[_AnchorBottom].AsFloat();
                c.AnchorTop = data[_AnchorTop].AsFloat();
                c.AnchorsPreset = data[_AnchorPreset].AsInt();
                c.OffsetLeft = data[_OffsetLeft].AsFloat();
                c.OffsetRight = data[_OffsetRight].AsFloat();
                c.OffsetBottom = data[_OffsetBottom].AsFloat();
                c.OffsetTop = data[_OffsetTop].AsFloat();
                c.PivotOffset = data[_PivotOffset].AsVector2();

                if (data[_Theme].IsValue)
                {
                    c.Theme = ResourceManager.Instance.GetResourceByPath<Theme>(data[_Theme].AsString());
                }
            }
            else if (node is Node3D)
            {
                Node3D node3d = (Node3D)node;

                node3d.Position = data[_Position].AsVector3();
                node3d.Rotation = data[_Rotation].AsVector3();
                node3d.Scale = data[_Scale].AsVector3();

            }


            // Save node instance id to re-reference after setting script
            ulong nodeID = node.GetInstanceId();
            // if type != derived-type, a script is attached
            if (!data[_Type].AsString().Equals(data[_DerivedType].AsString()))
            {
                node.SetScript(ResourceManager.Instance.GetResourceByName<Script>(data[_Type].AsString()));
            }

            node = GodotObject.InstanceFromId(nodeID) as Node;

            foreach (KeyValuePair<string, JsonValue> meta in data[_Meta].Object)
                node.SetMeta(meta.Key, meta.Value.AsString());
            foreach (JsonValue group in data[_Group].Array)
                node.AddToGroup(group.AsString());

            foreach (JsonValue child in data[_Children].Array)
                node.AddChild(ConvertJsonToNode(child));

            if (node is INetworkData ind)
                ind.DeserializeNetworkData(data[_INetworkData]);


            return node;
        }


        protected static string RemoveNamespace(string name)
        {
            int index = name.RFind(".");
            if (index < 0)
                return name;
            else
                return name.Substring(index + 1, name.Length - (index + 1));
        }

        #endregion

       

    }



    public interface INetworkData
    {
        public bool NetworkUpdate { get; set; }
        public JsonValue SerializeNetworkData(bool forceReturn = false, bool ignoreThisUpdateOccurred = false);
        public void DeserializeNetworkData(JsonValue data);
    }

    public static class NetworkExtensions
    {
        public static bool ShouldUpdate(this INetworkData data, bool forceUpdate)
        {
            if (forceUpdate) return true;
            if (data.NetworkUpdate) return true;
            return false;
        }
        #region Meta Accessors

        public static string GetUniqueId(this Node node)
        {
            return node.GetMeta(NetworkManager.UniqueIdMeta).AsString();
        }
        public static string GetOwnerId(this Node node)
        {
            if (!node.HasMeta(NetworkManager.OwnerIdMeta))
                return String.Empty;
            return node.GetMeta(NetworkManager.OwnerIdMeta).AsString();
        }

        public static void MakeLocalOnly(this Node node)
        {
            node.AddToGroup(NetworkManager.LocalOnlyGroup);
        }

        #endregion

        public static JsonValue CalculateNetworkReturn(this INetworkData data, JsonValue newData, bool ignoreThisUpdateOccured)
        {
            if (newData == null) return null;

            if (!ignoreThisUpdateOccured)
            {
                data.NetworkUpdate = false;
            }
            return newData;
        }

        public static bool IsServerNode(this Node node)
        {
            return !node.IsLocalNode();
        }

        
        public static bool IsLocalNode(this Node node)
        {
            if (node.IsInGroup(NetworkManager.LocalOnlyGroup))
                return true;
            if (node == NetworkManager.Instance.GetTree().Root)
                return false;

            return node.GetParent().IsLocalNode();
        }


    }

}