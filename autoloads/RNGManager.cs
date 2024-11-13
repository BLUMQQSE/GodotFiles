using Godot;
using System;
using System.Collections.Generic;
namespace BMUtil
{
	public partial class RNGManager : Node
	{
		private static readonly string _RNGS = "RS";

		private static RNGManager instance;
		public static RNGManager Instance { get { return instance; } }


		public RNGManager()
		{
			if (instance == null)
				instance = this;
		}

		public enum RandomType
		{
			Level,
			Drops,
			General
		}

		private Dictionary<RandomType, MyRandom> rngs = new Dictionary<RandomType, MyRandom>();

		public override void _Ready()
		{
			base._Ready();
			if (NetworkManager.Instance.IsServer)
			{
				rngs[RandomType.Level] = new MyRandom(0, 0);
				rngs[RandomType.Drops] = new MyRandom(0, 0);
				rngs[RandomType.General] = new MyRandom(0, 0);

				SaveManager.Instance.LoadSave += OnSaveLoaded;
				SaveManager.Instance.SaveEverything += OnGameSaved;

			}

		}

		public float Randf(RandomType type)
		{
			return rngs[type].RNG.Randf();
		}
		public float RandfRange(RandomType type, float from, float to)
		{
			return rngs[type].RNG.RandfRange(from, to);
		}
		public float Randfn(RandomType type, float mean, float deviation)
		{
			return rngs[type].RNG.Randfn(mean, deviation);
		}
		public uint Randi(RandomType type)
		{
			return rngs[type].RNG.Randi();
		}
		public int RandiRange(RandomType type, int from, int to)
		{
			return rngs[type].RNG.RandiRange(from, to);
		}

		private void OnGameSaved()
		{
			SaveManager.Instance.SaveData("RNG", Serialize(), SaveManager.SaveDest.Resource);
		}

		private void OnSaveLoaded()
		{
			Deserialize(SaveManager.Instance.LoadData("RNG", SaveManager.SaveDest.Resource));
		}

		private JsonValue Serialize()
		{
			JsonValue data = new JsonValue();

			foreach (var i in rngs)
			{
				data[_RNGS][(int)i.Key].Set(i.Value.Serialize());
			}

			return data;
		}
		private void Deserialize(JsonValue data)
		{
			int ind = 0;
			foreach (var i in data[_RNGS].Array)
			{
				rngs[(RandomType)ind].Deserialize(i);
				ind++;
			}
		}
	}

	public class MyRandom
	{
		private static readonly string _Seed = "S";
		private static readonly string _State = "ST";
		public RandomNumberGenerator RNG { get; private set; }
		public MyRandom(ulong seed, ulong state)
		{
			RNG = new RandomNumberGenerator();
			RNG.Seed = seed;
			RNG.State = state;
		}

		public JsonValue Serialize()
		{
			JsonValue data = new JsonValue();
			data[_Seed].Set(RNG.Seed.ToString());
			data[_State].Set(RNG.State.ToString());
			return data;
		}
		public void Deserialize(JsonValue data)
		{
			RNG.Seed = ulong.Parse(data[_Seed].AsString());
			RNG.State = ulong.Parse(data[_State].AsString());
		}
	}
}