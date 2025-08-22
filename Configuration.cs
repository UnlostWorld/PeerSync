// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioSync;

using Dalamud.Configuration;
using System.Collections.Generic;

public partial class Configuration : IPluginConfiguration
{
	public static Configuration Current
	{
		get
		{
			Configuration? config = Plugin.PluginInterface.GetPluginConfig() as Configuration;
			if (config == null)
				config = new();

			return config;
		}
	}

	public int Version { get; set; } = 1;
	public Dictionary<string, string> Passwords { get; init; } = new();
	public int Port { get; set; } = 1701;

	public void Save()
	{
		Plugin.PluginInterface.SavePluginConfig(this);
	}

	public string? GetPassword(string characterName, string world)
	{
		string id = $"{characterName}@{world}";
		this.Passwords.TryGetValue(id, out string? password);
		return password;
	}

	public void SetPassword(string characterName, string world, string password)
	{
		string id = $"{characterName}@{world}";
		this.Passwords[id] = password;
		this.Save();
	}
}
