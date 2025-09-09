// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

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
	public List<Character> Characters { get; init; } = new();
	public List<Pair> Pairs { get; init; } = new();
	public ushort Port { get; set; } = 0;
	public string? CacheDirectory { get; set; }
	public int MaxConcurrentUploads { get; set; } = 10;
	public int MaxConcurrentDownloads { get; set; } = 10;

	// TODO: In production we'll want to have at least one pre-configured
	// index server here.
	public List<string> IndexServers { get; init; } = new();

	public void Save()
	{
		Plugin.PluginInterface.SavePluginConfig(this);
	}

	public Pair? GetPair(string characterName, string world)
	{
		foreach (Pair pair in this.Pairs)
		{
			if (pair.CharacterName == characterName && pair.World == world)
			{
				return pair;
			}
		}

		return null;
	}

	public class Pair
	{
		public string? CharacterName { get; set; }
		public string? World { get; set; }
		public string? Password { get; set; }

		public bool IsTestPair => this.World == "Earth";

		private string? identifier;

		public string GetIdentifier()
		{
			if (string.IsNullOrEmpty(this.identifier))
			{
				const int iterations = 1000;

				// The Identifier is sent to the index servers, and it contains the character name and world, so
				// ensure its cryptographically secure in case of bad actors controlling servers.
				string pluginVersion = Plugin.PluginInterface.Manifest.AssemblyVersion.ToString();

#if DEBUG
				pluginVersion = "debug";
#endif

				string input = $"{this.CharacterName}{this.World}";
				for (int i = 0; i < iterations; i++)
				{
					HashAlgorithm algorithm = SHA256.Create();
					byte[] bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes($"{input}{this.Password}{pluginVersion}"));
					input = BitConverter.ToString(bytes);
					input = input.Replace("-", string.Empty, StringComparison.Ordinal);
				}

				this.identifier = input;
			}

			return this.identifier;
		}

		public void ClearIdentifier()
		{
			this.identifier = null;
		}

		public int CompareTo(Pair other)
		{
			string a = this.GetIdentifier();
			string b = other.GetIdentifier();

			return a.CompareTo(b);
		}

		public override string ToString()
		{
#if DEBUG
			return $"{this.CharacterName} @ {this.World} -> {this.GetIdentifier()}";
#else
			return this.GetIdentifier();
#endif

		}
	}

	public class Character : Pair
	{
		public void GeneratePassword(int length = 10)
		{
			StringBuilder sb = new();

			for (int i = 0; i < length; i++)
			{
				char letter = (char)Random.Shared.Next(33, 125);
				sb.Append(letter);
			}

			this.Password = sb.ToString();
		}
	}
}
