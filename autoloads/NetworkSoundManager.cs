using BMUtil;
using Godot;
using System;
namespace BMUtil
{
    public partial class NetworkSoundManager : SoundManager
    {
        private static NetworkSoundManager instance = null;
        public static NetworkSoundManager Instance { get { return instance; } }


        public override void _Ready()
        {
            SoundNode = GetTree().CurrentScene.GetNode("Sound");

            Node musicNode = new Node();
            musicNode.MakeLocalOnly();
            musicNode.Name = SoundType.Music.ToString();
            SoundNode.AddChild(musicNode);

            int music = (int)SoundType.Music;
            for (int i = 0; i < music; i++)
            {
                AudioStreamPlayer p = new AudioStreamPlayer();
                p.MakeLocalOnly();
                p.Name = i.ToString();
                musicNode.AddChild(p);
            }


            Node uiNode = new Node();
            uiNode.MakeLocalOnly();
            uiNode.Name = SoundType.UI.ToString();
            SoundNode.AddChild(uiNode);

            int ui = (int)SoundType.UI;
            for (int i = 0; i < ui; i++)
            {
                AudioStreamPlayer p = new AudioStreamPlayer();
                p.MakeLocalOnly();
                p.Name = i.ToString();
                uiNode.AddChild(p);
            }

            Node ambNode = new Node();
            ambNode.MakeLocalOnly();
            ambNode.Name = SoundType.Ambiance.ToString();
            SoundNode.AddChild(ambNode);

            int amb = (int)SoundType.Ambiance;
            for (int i = 0; i < amb; i++)
            {
                AudioStreamPlayer p = new AudioStreamPlayer();
                p.MakeLocalOnly();
                p.Name = i.ToString();
                ambNode.AddChild(p);
            }

            Node sfxNode = new Node();
            sfxNode.MakeLocalOnly();
            sfxNode.Name = SoundType.SFX.ToString();
            SoundNode.AddChild(sfxNode);

            int sfx = (int)SoundType.SFX;
            for (int i = 0; i < sfx; i++)
            {
                FollowerNode3D f = new FollowerNode3D();
                f.MakeLocalOnly();
                f.Name = i.ToString();
                AudioStreamPlayer3D p = new AudioStreamPlayer3D();
                p.MakeLocalOnly();
                f.AddChild(p);
                sfxNode.AddChild(f);
            }

        }

        public void PlayNetworkSound(SoundData soundData, string playerId)
        {
            if (!NetworkManager.Instance.IsServer)
                return;
            if (playerId == NetworkManager.Instance.PlayerId)
                PlaySound(soundData);
            else
                NetworkManager.Instance.RpcClient(playerId, this, nameof(RpcPlayNetworkSound), soundData.ResourcePath.RemovePathAndFileType());
        }
        public void PlayNetworkSoundAll(SoundData soundData)
        {
            if (!NetworkManager.Instance.IsServer)
                return;
            PlaySound(soundData);
            NetworkManager.Instance.RpcClients(this, nameof(RpcPlayNetworkSound), soundData.ResourcePath.RemovePathAndFileType());
        }

        [ServerToClient]
        private void RpcPlayNetworkSound(string soundData)
        {
            PlaySound(ResourceManager.Instance.GetResourceByName<SoundData>(soundData));
        }

        public void PlayNetworkSound(SoundData soundData, Node3D origin, string playerId, bool follow = false)
        {
            if (!NetworkManager.Instance.IsServer)
                return;
            if (playerId == NetworkManager.Instance.PlayerId)
                PlaySound(soundData, origin, follow);
            else
                NetworkManager.Instance.RpcClient(playerId, this, nameof(RpcPlayNetSound), soundData.ResourcePath.RemovePathAndFileType(), origin, follow);
        }
        public void PlayNetworkSoundAll(SoundData soundData, Node3D origin, bool follow = false)
        {
            if (!NetworkManager.Instance.IsServer)
                return;
            PlaySound(soundData, origin, follow);
            NetworkManager.Instance.RpcClients(this, nameof(RpcPlayNetSound), soundData.ResourcePath.RemovePathAndFileType(), origin, follow);
        }

        [ServerToClient]
        private void RpcPlayNetSound(string soundData, Node3D origin, bool follow)
        {
            PlaySound(ResourceManager.Instance.GetResourceByName<SoundData>(soundData), origin, follow);
        }
    }
}