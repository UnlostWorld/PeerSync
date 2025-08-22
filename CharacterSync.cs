// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioSync;

using System.Net;
using System.Security.Cryptography;
using System.Text;

public class CharacterSync
{
	public string Id;
	public IPAddress? Address;
	public int? Port;

	public CharacterSync(string characterName, string world, string password)
	{
		this.Id = GetSyncId(characterName, world, password);

		Plugin.Log?.Info($"Create Sync: {characterName}@{world} ({this.Id})");
	}

	public static string GetSyncId(string characterName, string world, string password)
	{
		characterName = characterName.ToLowerInvariant();
		world = world.ToLowerInvariant();

		HashAlgorithm algorithm = SHA256.Create();
		byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes($"{characterName}{world}{password}"));

		StringBuilder sb = new();
		foreach (byte b in hash)
			sb.Append(b.ToString("X2"));

		return sb.ToString();
	}
}