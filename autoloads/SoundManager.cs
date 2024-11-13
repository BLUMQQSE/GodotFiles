using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
namespace BMUtil
{
    public enum SoundType
    {
        Music = 10,
        UI = 12,
        Ambiance = 30,
        SFX = 30
    }
    // IMPORTANT NOTE: Main scene must contain a node "Sound" for sounds to attach
    public partial class SoundManager : Node
	{
        private static SoundManager instance = null;
        public static SoundManager Instance { get { return instance; } }
        
        protected Node SoundNode;

        protected static StringName SoundDataMeta = new StringName();

        public struct SoundEffect
        {
            private static int DESTROY_DELAY = 5;
            public SoundEffect(AudioStreamPlayer player)
            {
                this.player = player;
                tt = new TimeTracker();
                this.player.Finished += Finish;
            }
            public SoundEffect(AudioStreamPlayer2D player2d, Vector2 position)
            {
                this.player2d.GlobalPosition = position;
                this.player2d = player2d;
                tt = new TimeTracker();
                this.player2d.Finished += Finish;
            }
            public SoundEffect(AudioStreamPlayer3D player3d, Vector3 position)
            {
                this.player3d.GlobalPosition = position;
                this.player3d = player3d;
                tt = new TimeTracker();
                this.player3d.Finished += Finish;
            }

            public AudioStreamPlayer player = null;
            public AudioStreamPlayer3D player3d = null;
            public AudioStreamPlayer2D player2d = null;
            public TimeTracker tt;

            public bool Destroy
            {
                get
                {
                    if (!tt.IsRunning)
                        return false;

                    if (player.Playing)
                    {
                        tt.Stop();
                        return false;
                    }

                    return tt.ElapsedSeconds > DESTROY_DELAY;
                }
            }
            public void Dispose()
            {
                tt.Dispose();
            }

            private void Finish()
            {
                tt.Start();
            }
        }

        public override void _Ready()
        {
            base._Ready();

            SoundNode = GetTree().CurrentScene.GetNode("Sound");
            
            Node musicNode = new Node();
            musicNode.Name = SoundType.Music.ToString();
            SoundNode.AddChild(musicNode);

            int music = (int)SoundType.Music;
            for (int i = 0; i < music; i++)
            {
                AudioStreamPlayer p = new AudioStreamPlayer();
                p.Name = i.ToString();
                musicNode.AddChild(p);
            }


            Node uiNode = new Node();
            uiNode.Name = SoundType.UI.ToString();
            SoundNode.AddChild(uiNode);

            int ui = (int)SoundType.UI;
            for (int i = 0; i < ui; i++)
            {
                AudioStreamPlayer p = new AudioStreamPlayer();
                p.Name = i.ToString();
                uiNode.AddChild(p);
            }

            Node ambNode = new Node();
            ambNode.Name = SoundType.Ambiance.ToString();
            SoundNode.AddChild(ambNode);

            int amb = (int)SoundType.Ambiance;
            for (int i = 0; i < amb; i++)
            {
                AudioStreamPlayer p = new AudioStreamPlayer();
                p.Name = i.ToString();
                ambNode.AddChild(p);
            }

            Node sfxNode = new Node();
            sfxNode.Name = SoundType.SFX.ToString();
            SoundNode.AddChild(sfxNode);

            int sfx = (int)SoundType.SFX;
            for (int i = 0; i < sfx; i++)
            {
                FollowerNode3D f = new FollowerNode3D();
                f.Name = i.ToString();
                AudioStreamPlayer3D p = new AudioStreamPlayer3D();
                f.AddChild(p);
                sfxNode.AddChild(f);
            }

        }





        public void PlaySound(SoundData soundData)
        {
            if (!SoundNode.IsValid())
                CollectSoundNode();

            Node n = SoundNode.GetNode(soundData.SoundType.ToString());

            if (soundData.SoundType != SoundType.SFX)
            { 
                var result = FindAudioPlayer(soundData, n.GetChildren<AudioStreamPlayer>());
                if (result.Item2)
                {
                    n.GetChild<AudioStreamPlayer>(result.Item1).Play();
                }
                else if (result.Item1 != -1)
                {
                    AudioStreamPlayer playerStream = n.GetChild<AudioStreamPlayer>(result.Item1);

                    playerStream.Stream = soundData.AudioStream;
                    playerStream.VolumeDb = soundData.VolumeDB;
                    playerStream.PitchScale = soundData.PitchScale;
                    playerStream.MaxPolyphony = soundData.MaxSimultaneousInstances;

                    playerStream.Play();
                }
            }

        }

        public void PlaySound(SoundData soundData, Node3D origin, bool follow = false)
        {
            if (!SoundNode.IsValid())
                CollectSoundNode();

            Node n = SoundNode.GetNode(soundData.SoundType.ToString());

            if (soundData.SoundType == SoundType.SFX)
            {
                var result = FindAudioPlayer3D(soundData, n.GetChildren<Node>(), origin, origin.GlobalPosition);
                if (result.Item2)
                {
                    n.GetChild(result.Item1).GetChildOfType<AudioStreamPlayer3D>().Play();
                }
                else if (result.Item1 != -1)
                {
                    FollowerNode3D fn3 = n.GetChild<FollowerNode3D>(result.Item1);
                    fn3.GlobalPosition = origin.GlobalPosition;
                    if (follow)
                        fn3.SetFollowNode(origin);
                    else
                        fn3.SetFollowNode(null);
                    AudioStreamPlayer3D playerStream = fn3.GetChildOfType<AudioStreamPlayer3D>();

                    playerStream.Stream = soundData.AudioStream;
                    playerStream.VolumeDb = soundData.VolumeDB;
                    playerStream.PitchScale = soundData.PitchScale;
                    playerStream.MaxPolyphony = soundData.MaxSimultaneousInstances;
                    playerStream.AttenuationModel = soundData.AttenuationModel;
                    playerStream.MaxDb = soundData.MaxDB;
                    playerStream.MaxDistance = soundData.MaxDistance;
                    playerStream.UnitSize = soundData.UnitSize;

                    playerStream.Play();
                }
            }
            
        }


        private void CollectSoundNode()
        {
            SoundNode = GetTree().CurrentScene.GetNode("Sound");
        }

        /// <summary>
        /// Returns location of available play stream, and true if a match was found
        /// </summary>
        /// <param name="matching"></param>
        /// <param name="audioPlayers"></param>
        /// <returns></returns>
        private (int, bool) FindAudioPlayer(SoundData matching, List<AudioStreamPlayer> audioPlayers)
        {
            int firstAvailable = -1;

            for (int i = 0; i < audioPlayers.Count; i++)
            {
                if (audioPlayers[i].Playing)
                {
                    if (audioPlayers[i].Stream == matching.AudioStream &&
                        audioPlayers[i].VolumeDb == matching.VolumeDB &&
                        audioPlayers[i].PitchScale == matching.PitchScale &&
                        audioPlayers[i].MixTarget == matching.MixTarget &&
                        audioPlayers[i].MaxPolyphony == matching.MaxSimultaneousInstances)
                    {
                        return (i, true);
                    }
                }
                if (!audioPlayers[i].Playing && firstAvailable == -1)
                    firstAvailable = i;
            }

            return (firstAvailable, false);            
        }

        private (int, bool) FindAudioPlayer3D(SoundData matching, List<Node> followNodes, Node3D followNode, Vector3 position)
        {
            int firstAvailable = -1;

            for (int i = 0; i < followNodes.Count; i++)
            {
                if (followNodes[i].GetChild<AudioStreamPlayer3D>(0).Playing)
                {
                    if (followNodes[i].GetChild<AudioStreamPlayer3D>(0).Stream == matching.AudioStream &&
                        followNodes[i].GetChild<AudioStreamPlayer3D>(0).VolumeDb == matching.VolumeDB &&
                        followNodes[i].GetChild<AudioStreamPlayer3D>(0).PitchScale == matching.PitchScale &&
                        followNodes[i].GetChild<AudioStreamPlayer3D>(0).MaxPolyphony == matching.MaxSimultaneousInstances &&
                        followNodes[i].GetChild<AudioStreamPlayer3D>(0).AttenuationModel == matching.AttenuationModel &&
                        followNodes[i].GetChild<AudioStreamPlayer3D>(0).MaxDb == matching.MaxDB &&
                        followNodes[i].GetChild<AudioStreamPlayer3D>(0).MaxDistance == matching.MaxDistance &&
                        followNodes[i].GetChild<AudioStreamPlayer3D>(0).UnitSize == matching.UnitSize &&
                        ((followNodes[i] as FollowerNode3D).FollowNode == followNode || (followNodes[i] as Node3D).GlobalPosition == position))
                    {
                        return (i, true);
                    }
                }
                if (!followNodes[i].GetChild<AudioStreamPlayer3D>(0).Playing && firstAvailable == -1)
                    firstAvailable = i;
            }

            return (firstAvailable, false);
        }

    }
}