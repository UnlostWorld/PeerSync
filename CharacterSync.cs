// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioSync;

using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections.TCP;
using StudioOnline.Sync;

public class CharacterSync : IDisposable
{
	public readonly string CharacterName;
	public readonly string World;
	public readonly string Id;

	public string Status { get; private set; } = string.Empty;

	private bool disposed = false;
	private TCPConnection? connection;

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

			if (response == null || response.Address == null)
			{
				this.Status = "Offline";
				return;
			}

			IPAddress.TryParse(response.Address, out var address);
			if (address == null)
			{
				this.Status = "Offline";
				return;
			}

			IPAddress.TryParse(response.LocalAddress, out var localAddress);

			Plugin.Log?.Info($"Got address for Sync: {this.CharacterName}@{this.World} : {address} / {localAddress} : {response.Port}");

			this.Status = "Connecting";

			if (localAddress != null)
			{
				try
				{
					IPEndPoint endpoint = new(localAddress, response.Port);
					this.connection = TCPConnection.GetConnection(new(endpoint));
					this.connection.SendObject("Message", "hello there");

					this.Status = "Connected (LAN)";
				}
				catch (Exception)
				{
					this.connection = null;
				}
			}

			if (this.connection == null)
			{
				IPEndPoint endpoint = new(address, response.Port);
				this.connection = TCPConnection.GetConnection(new(endpoint));

				this.Status = "Connected (WAN)";
			}

			// Send who packet to identify ourselves.
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error connecting to character sync");
		}
	}
}