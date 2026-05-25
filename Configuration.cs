// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

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
	public List<Group> Groups { get; init; } = new();
	public List<Peer> Pairs { get; init; } = new();
	public HashSet<string> BlockedCharacters { get; init; } = new();
	public ushort Port { get; set; } = 0;
	public ushort LastPort { get; set; } = 0;
	public string? CacheDirectory { get; set; }

	public int MaxUploads { get; set; } = 5;
	public int MaxDownloads { get; set; } = 10;

	public string DebugVersion { get; set; } = "Debug";

	public HashSet<string> IndexServers { get; init; } = new()
	{
		"https://peer-sync-index-server-9y4rg.ondigitalocean.app",
	};

	public void Save()
	{
		Plugin.PluginInterface.SavePluginConfig(this);
	}

	public Peer? GetFriend(string characterName, string world)
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

	public Group? GetGroup(string groupName)
	{
		foreach (Group group in this.Groups)
		{
			if (group.Name == groupName)
			{
				return group;
			}
		}

		return null;
	}

	public bool GetIsBlocked(string characterName, string world)
	{
		string compoundName = $"{characterName}@{world}";
		return this.BlockedCharacters.Contains(compoundName);
	}

	public void SetIsBlocked(string characterName, string world, bool block)
	{
		string compoundName = $"{characterName}@{world}";
		if (block)
		{
			this.BlockedCharacters.Add(compoundName);
			this.Save();
		}
		else
		{
			if (this.BlockedCharacters.Contains(compoundName))
			{
				this.BlockedCharacters.Remove(compoundName);
				this.Save();
			}
		}
	}

	public class Group
	{
		private string? fingerprint;
		public string? Name { get; set; }
		public string? Password { get; set; }

		public string GetFingerprint()
		{
			if (string.IsNullOrEmpty(this.fingerprint))
			{
				const int iterations = 1000;

				string pluginVersion = Plugin.PluginInterface.Manifest.AssemblyVersion.ToString();

#if DEBUG
				pluginVersion = Configuration.Current.DebugVersion;
#endif

				string input = $"{this.Name}{this.Password}";
				for (int i = 0; i < iterations; i++)
				{
					HashAlgorithm algorithm = SHA256.Create();
					input = $"{input}{this.Password}{pluginVersion}";
					byte[] bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(input));
					input = BitConverter.ToString(bytes);
					input = input.Replace("-", string.Empty, StringComparison.Ordinal);
				}

				this.fingerprint = input;
			}

			return this.fingerprint;
		}

		public string GetMemberFingerprint(string characterName, string world)
		{
			if (string.IsNullOrEmpty(this.fingerprint))
			{
				this.GetFingerprint();
			}

			const int iterations = 1000;

			string input = $"{characterName}{world}";
			for (int i = 0; i < iterations; i++)
			{
				HashAlgorithm algorithm = SHA256.Create();
				input = $"{input}{this.fingerprint}";
				byte[] bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(input));
				input = BitConverter.ToString(bytes);
				input = input.Replace("-", string.Empty, StringComparison.Ordinal);
			}

			return input;
		}
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
				pluginVersion = Configuration.Current.DebugVersion;
#endif

				string input = $"{this.CharacterName}{this.World}";
				for (int i = 0; i < iterations; i++)
				{
					HashAlgorithm algorithm = SHA256.Create();
					input = $"{input}{this.Password}{pluginVersion}";
					byte[] bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(input));
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
