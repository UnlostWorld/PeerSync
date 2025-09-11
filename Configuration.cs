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
	public List<Peer> Pairs { get; init; } = new();
	public ushort Port { get; set; } = 0;
	public string? CacheDirectory { get; set; }

	public int MaxUploads { get; set; } = 5;
	public int MaxDownloads { get; set; } = 10;

	// TODO: In production we'll want to have at least one pre-configured
	// index server here.
	public List<string> IndexServers { get; init; } = new();

	public void Save()
	{
		Plugin.PluginInterface.SavePluginConfig(this);
	}

	public Peer? GetPeer(string characterName, string world)
	{
		foreach (Peer pair in this.Pairs)
		{
			if (pair.CharacterName == characterName && pair.World == world)
			{
				return pair;
			}
		}

		return null;
	}

	public class Peer
	{
		private string? fingerprint;

		public string? CharacterName { get; set; }
		public string? World { get; set; }
		public string? Password { get; set; }

		public string GetFingerprint()
		{
			if (string.IsNullOrEmpty(this.fingerprint))
			{
				const int iterations = 1000;

				// The Fingerprint is sent to the index servers, and it contains the character name and world, so
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

				this.fingerprint = input;
			}

			return this.fingerprint;
		}

		public void ClearFingerprint()
		{
			this.fingerprint = null;
		}

		public int CompareTo(Peer other)
		{
			string a = this.GetFingerprint();
			string b = other.GetFingerprint();

			return a.CompareTo(b);
		}

		public override string ToString()
		{
#if DEBUG
			return $"{this.CharacterName} @ {this.World} -> {this.GetFingerprint()}";
#else
			return this.GetFingerprint();
#endif

		}
	}

	public class Character : Peer
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
