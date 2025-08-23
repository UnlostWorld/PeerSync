// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioSync;

using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using StudioOnline.Sync;

public class CharacterSync : IDisposable
{
	public readonly string CharacterName;
	public readonly string World;
	public readonly string Id;

	public string Status { get; private set; } = string.Empty;

	private IPAddress? address;
	private int? port;
	private bool disposed = false;

	public CharacterSync(string characterName, string world, string password)
	{
		this.CharacterName = characterName;
		this.World = world;
		this.Id = GetSyncId(characterName, world, password);

		Plugin.Log?.Info($"Create Sync: {characterName}@{world} ({this.Id})");

		Task.Run(this.Connect);
	}

	public int ObjectTableIndex { get; set; }

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

	public void Dispose()
	{
		this.disposed = true;
		Plugin.Log?.Info($"Destroy Sync: {this.CharacterName}@{this.World} ({this.Id})");
	}

	public bool Update()
	{
		IGameObject? obj = Plugin.ObjectTable[this.ObjectTableIndex];
		if (obj == null)
			return false;

		if (obj is not IPlayerCharacter character)
			return false;

		if (character.Name.ToString() != this.CharacterName || character.HomeWorld.Value.Name != this.World)
			return false;

		return true;
	}

	private async Task Connect()
	{
		try
		{
			this.Status = "Searching";
			SyncStatus request = new();
			request.Identifier = this.Id;
			SyncStatus? response = await request.Send();

			if (disposed)
				return;

			if (response == null || response.Address == null || response.Port == null)
				return;

			this.address = IPAddress.Parse(response.Address);
			this.port = response.Port;

			this.Status = this.address == null ? "Offline" : "Online";

			Plugin.Log?.Info($"Got address for Sync: {this.CharacterName}@{this.World} : {this.address}:{this.port}");
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error conencting to character sync");
		}
	}
}