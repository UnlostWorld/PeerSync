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
	private CharacterData? lastData;

	public CharacterSync(string characterName, string world, string password)
	{
		this.CharacterName = characterName;
		this.World = world;
		this.Identifier = GetIdentifier(characterName, world, password);

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

		// The connection failed.
		ConnectionFailed,

		// We've established a connection and are now identifying ourselves.
		Handshake,

		// We've failed to establish two way connection.
		HandshakeFailed,

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

	public static string GetIdentifier(string characterName, string world, string password, int iterations = 1000)
	{
		// The Identifier is sent to the server, and it contains the character name and world, so
		// ensure its cryptographically secure in case of bad actors controlling servers.
		string input = $"{characterName}{world}";
		for (int i = 0; i < iterations; i++)
		{
			HashAlgorithm algorithm = SHA256.Create();
			byte[] bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes($"{input}{password}"));
			input = BitConverter.ToString(bytes);
			input = input.Replace("-", string.Empty, StringComparison.Ordinal);
		}

		return input;
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
		if (this.CurrentStatus == Status.HandshakeFailed || this.CurrentStatus == Status.ConnectionFailed)
		{
			this.Reconnect();
			return;
		}

		if (this.CurrentStatus != Status.Handshake)
			return;

		Plugin.Log.Information($"Got IAm packet from {this.CharacterName}, Status: {this.CurrentStatus}");

		this.incomingConnection = connection;
		this.incomingConnection.AppendShutdownHandler(this.OnIncomingConnectionClosed);
		this.incomingConnection.AppendIncomingPacketHandler<CharacterData>("CharacterData", this.OnCharacterDataPacket);

		this.CurrentStatus = Status.Connected;
	}

	public void Dispose()
	{
		this.disposed = true;
		this.outgoingConnection?.Dispose();
		this.incomingConnection?.Dispose();
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

	public void SendData(CharacterData data)
	{
		this.outgoingConnection?.SendObject("iam", Plugin.LocalCharacterIdentifier);
		this.outgoingConnection?.SendObject("CharacterData", data);
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

			try
			{
				if (this.outgoingConnection == null)
				{
					IPEndPoint endpoint = new(address, response.Port);
					this.outgoingConnection = TCPConnection.GetConnection(new(endpoint));
					this.ConnectionType = ConnectionTypes.Internet;
				}
			}
			catch (Exception)
			{
				this.outgoingConnection = null;
				this.CurrentStatus = Status.ConnectionFailed;
				return;
			}

			this.outgoingConnection.AppendShutdownHandler(this.OnOutgoingConnectionClosed);

			// Send who packet to identify ourselves.
			this.CurrentStatus = Status.Handshake;

			int attempts = 0;
			while (this.CurrentStatus == Status.Handshake && attempts < 10)
			{
				Plugin.Log.Information($"Sending IAm packet to {this.CharacterName}");
				attempts++;
				this.outgoingConnection.SendObject("iam", Plugin.LocalCharacterIdentifier);
				await Task.Delay(3000);
			}

			if (attempts >= 10)
			{
				this.CurrentStatus = Status.HandshakeFailed;
				throw new Exception("Handshake failed");
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

	private void OnCharacterDataPacket(PacketHeader packetHeader, Connection connection, CharacterData characterData)
	{
		// Sanity check
		if (connection != this.incomingConnection)
			return;

		if (lastData == null || !characterData.IsPenumbraReplacementsSame(lastData))
		{
			Plugin.Log.Information($"{this.CharacterName}@{this.World} > Penumbra files");
		}

		/*if (lastData == null || characterData.PenumbraManipulations != lastData.PenumbraManipulations)
		{
			Plugin.Log.Information($"> Penumbra meta");
		}

		if (lastData == null || characterData.CustomizePlus != lastData.CustomizePlus)
		{
			Plugin.Log.Information($"> Customize+");
		}

		if (lastData == null || characterData.Glamourer != lastData.Glamourer)
		{
			Plugin.Log.Information($"> Glamourer");
		}

		if (lastData == null || characterData.Heels != lastData.Heels)
		{
			Plugin.Log.Information($"> Heels");
		}

		if (lastData == null || characterData.Honorific != lastData.Honorific)
		{
			Plugin.Log.Information($"> Honorific");
		}

		if (lastData == null || characterData.Moodles != lastData.Moodles)
		{
			Plugin.Log.Information($"> Moodls");
		}

		if (lastData == null || characterData.PetNames != lastData.PetNames)
		{
			Plugin.Log.Information($"> Pet Names");
		}*/

		lastData = characterData;
	}
}