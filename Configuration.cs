// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using Dalamud.Configuration;
using System.Collections.Generic;

public partial class Configuration : IPluginConfiguration
{
	private static Configuration? current;

	public static Configuration Current
	{
		get
		{
			if (current == null)
				current = Plugin.PluginInterface.GetPluginConfig() as Configuration;

			if (current == null)
				current = new();

			return current;
		}
	}

	public int Version { get; set; } = 1;
	public List<Pair> Pairs { get; init; } = new();
	public ushort Port { get; set; } = 0;
	public string? CacheDirectory { get; set; }

	public void Save()
	{
		Plugin.PluginInterface.SavePluginConfig(this);
	}

	public string? GetPassword(string characterName, string world)
	{
		string id = $"{characterName}@{world}";
		foreach (Pair pair in this.Pairs)
		{
			if (pair.CharacterName == characterName && pair.World == world)
			{
				return pair.Password;
			}
		}

		return null;
	}

	public void SetPassword(string characterName, string world, string? password)
	{
		string id = $"{characterName}@{world}";

		foreach (Pair pair in this.Pairs)
		{
			if (pair.CharacterName == characterName && pair.World == world)
			{
				pair.Password = password;
				this.Save();
				return;
			}
		}

		Pair newPair = new();
		newPair.CharacterName = characterName;
		newPair.World = world;
		newPair.Password = password;
		this.Pairs.Add(newPair);
		this.Save();
	}

	public class Pair
	{
		public string? CharacterName { get; set; }
		public string? World { get; set; }
		public string? Password { get; set; }
	}
}
