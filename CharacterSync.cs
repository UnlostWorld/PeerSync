// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using StudioOnline.Sync;

public class CharacterSync : IDisposable
{
	public readonly string CharacterName;
	public readonly string World;
	public readonly string Identifier;

	public Status CurrentStatus { get; private set; } = Status.None;
	public ConnectionTypes ConnectionType { get; private set; } = ConnectionTypes.None;

	private bool disposed = false;
	private Connection? incomingConnection;
	private TCPConnection? outgoingConnection;

	public CharacterSync(string characterName, string world, string password)
	{
		this.CharacterName = characterName;
		this.World = world;
		this.Identifier = GetSyncId(characterName, world, password);

		Plugin.Log?.Info($"Create Sync: {characterName}@{world} ({this.Identifier})");

		Task.Run(this.Connect);
	}

	public enum Status
	{
		None,

		// Querying the server for this characters connection details.
		Searching,

		// This character had no connection details, they are either offline, or don't exist.
		Offline,

		// We are attempting to establish a connection.
		Connecting,

		// We've established a connection and are now identifying ourselves.
		Handshake,

		// They've established a connection back.
		Connected,
	}

	public enum ConnectionTypes
	{
		None,

		Local,
		Internet,
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

	public void Reconnect()
	{
		Plugin.Log?.Info($"Reconnect Sync: {this.CharacterName}@{this.World} ({this.Identifier})");

		this.incomingConnection?.Dispose();
		this.incomingConnection = null;

		this.outgoingConnection?.Dispose();
		this.outgoingConnection = null;
		this.CurrentStatus = Status.None;

		Task.Run(this.Connect);
	}

	public void SetIncomingConnection(Connection connection)
	{
		this.incomingConnection = connection;
		this.incomingConnection.AppendShutdownHandler(this.OnIncomingConnectionClosed);
		this.incomingConnection.AppendIncomingPacketHandler<byte[]>("SomethingNew", this.OnIncomingData);

		this.CurrentStatus = Status.Connected;
	}

	public void Dispose()
	{
		this.disposed = true;
		Plugin.Log?.Info($"Destroy Sync: {this.CharacterName}@{this.World} ({this.Identifier})");
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
			this.ConnectionType = ConnectionTypes.None;
			this.CurrentStatus = Status.Searching;
			SyncStatus request = new();
			request.Identifier = this.Identifier;
			SyncStatus? response = await request.Send();

			if (disposed)
				return;

			if (response == null || response.Address == null)
			{
				this.CurrentStatus = Status.Offline;
				return;
			}

			IPAddress.TryParse(response.Address, out var address);
			if (address == null)
			{
				this.CurrentStatus = Status.Offline;
				return;
			}

			IPAddress.TryParse(response.LocalAddress, out var localAddress);

			this.CurrentStatus = Status.Connecting;

			if (localAddress != null)
			{
				try
				{
					IPEndPoint endpoint = new(localAddress, response.Port);
					this.outgoingConnection = TCPConnection.GetConnection(new(endpoint));
					this.ConnectionType = ConnectionTypes.Local;
				}
				catch (Exception)
				{
					this.outgoingConnection = null;
				}
			}

			if (this.outgoingConnection == null)
			{
				IPEndPoint endpoint = new(address, response.Port);
				this.outgoingConnection = TCPConnection.GetConnection(new(endpoint));
				this.ConnectionType = ConnectionTypes.Internet;
			}

			this.outgoingConnection.AppendShutdownHandler(this.OnOutgoingConnectionClosed);

			// Send who packet to identify ourselves.
			this.CurrentStatus = Status.Handshake;

			int attempts = 0;
			while (this.CurrentStatus == Status.Handshake && attempts < 10)
			{
				attempts++;
				this.outgoingConnection.SendObject("iam", Plugin.LocalCharacterId);
				await Task.Delay(1000);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error connecting to character sync");
		}
	}

	private void OnOutgoingConnectionClosed(Connection connection)
	{
		this.Reconnect();
	}

	private void OnIncomingConnectionClosed(Connection connection)
	{
		this.Reconnect();
	}

	private void OnIncomingData(PacketHeader packetHeader, Connection connection, byte[] incomingObject)
	{
		// Sanity check
		if (connection != this.incomingConnection)
			return;
	}
}