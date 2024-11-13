using Godot;
using System;
namespace BMUtil
{
    [GlobalClass]
    public partial class SoundData : Resource
    {
        [ExportCategory("BM")]
        public SoundType SoundType { get; set; }
        [ExportCategory("AudioStreamPlayer")]
        [Export] public AudioStream AudioStream { get; private set; }
        [Export] public bool IsMusic { get; private set; }
        [Export(PropertyHint.Range, "-80, 80")] public int VolumeDB { get; private set; } = 0;
        [Export(PropertyHint.Range, "0.01, 4")] public float PitchScale { get; private set; } = 1;
        [Export] public AudioStreamPlayer.MixTargetEnum MixTarget { get; private set; } = AudioStreamPlayer.MixTargetEnum.Stereo;
        [Export(PropertyHint.Range, "1, 30")] public int MaxSimultaneousInstances { get; private set; } = 1;



        [ExportCategory("AudioStreamPlayer3D")]
        [Export] public AudioStreamPlayer3D.AttenuationModelEnum AttenuationModel { get; private set; }
        [Export(PropertyHint.Range, "-24, 6")] public int MaxDB { get; private set; } = 3;
        [Export(PropertyHint.Range, "0, 4000")] public float MaxDistance { get; private set; } = 0;
        [Export(PropertyHint.Range, "0.01, 100")] public float UnitSize { get; private set; } = 10;

    }
}