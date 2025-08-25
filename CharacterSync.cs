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

	private bool disposed = false;
	private Connection? connection;
	private CharacterData? lastData;
	private bool isApplyingData = false;

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

		// Waiting for the peer to connect to us.
		Listening,

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

		// The connection was terminated.
		Disconnected,
	}

	public ushort ObjectTableIndex { get; set; }

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
		if (this.CurrentStatus == Status.Searching
			|| this.CurrentStatus == Status.Connecting
			|| this.CurrentStatus == Status.Handshake)
			return;

		Plugin.Log?.Info($"Reconnect Sync: {this.CharacterName}@{this.World} ({this.Identifier})");

		this.connection?.Dispose();
		this.connection = null;

		this.connection?.Dispose();
		this.connection = null;

		this.CurrentStatus = Status.None;

		Task.Run(this.Connect);
	}

	public void SetConnection(Connection connection)
	{
		if (this.CurrentStatus != Status.Listening)
			return;

		this.connection = connection;

		this.connection.AppendShutdownHandler(this.OnConnectionClosed);
		this.connection.AppendIncomingPacketHandler<string>("iam", this.OnIAmPacket);
		this.connection.AppendIncomingPacketHandler<CharacterData>("CharacterData", this.OnCharacterDataPacket);

		this.CurrentStatus = Status.Connected;
	}

	public void Dispose()
	{
		this.disposed = true;
		this.connection?.CloseConnection(false);
		this.connection?.Dispose();
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
		if (this.connection == null)
			return;

		this.connection.SendObject("iam", Plugin.Instance?.LocalCharacterIdentifier);
		this.connection.SendObject("CharacterData", data);
	}

	private async Task Connect()
	{
		try
		{
			if (Plugin.Instance == null || Plugin.Instance.LocalCharacterIdentifier == null)
				return;

			int sort = Plugin.Instance.LocalCharacterIdentifier.CompareTo(this.Identifier);
			if (sort >= 0)
			{
				// We're the host.
				this.CurrentStatus = Status.Listening;
				return;
			}

			// We're the client.
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
					this.connection = TCPConnection.GetConnection(new(endpoint));
				}
				catch (Exception)
				{
					this.connection = null;
				}
			}

			try
			{
				if (this.connection == null)
				{
					IPEndPoint endpoint = new(address, response.Port);
					this.connection = TCPConnection.GetConnection(new(endpoint));
				}
			}
			catch (Exception)
			{
				this.connection = null;
				this.CurrentStatus = Status.ConnectionFailed;
				return;
			}

			// Send who packet to identify ourselves.
			this.CurrentStatus = Status.Handshake;

			if (this.connection == null)
				return;

			this.connection.AppendShutdownHandler(this.OnConnectionClosed);
			this.connection.AppendIncomingPacketHandler<string>("iam", this.OnIAmPacket);
			this.connection.AppendIncomingPacketHandler<CharacterData>("CharacterData", this.OnCharacterDataPacket);

			if (this.disposed)
				return;

			int attempts = 0;
			while (this.CurrentStatus == Status.Handshake && attempts < 10)
			{
				if (this.disposed)
					return;

				attempts++;
				this.connection.SendObject("iam", Plugin.Instance?.LocalCharacterIdentifier);
				await Task.Delay(3000);
			}

			if (this.disposed)
				return;

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

	private void OnConnectionClosed(Connection connection)
	{
		Plugin.Log.Information($"Connection to {this.CharacterName}@{this.World} was closed.");
		this.CurrentStatus = Status.Disconnected;

		this.Reconnect();
	}

	private void OnIAmPacket(PacketHeader packetHeader, Connection connection, string incomingObject)
	{
		if (connection != this.connection || incomingObject != this.Identifier)
			return;

		if (this.CurrentStatus == Status.Handshake)
		{
			this.CurrentStatus = Status.Connected;
		}
	}

	private void OnCharacterDataPacket(PacketHeader packetHeader, Connection connection, CharacterData characterData)
	{
		// Sanity check
		if (connection != this.connection)
			return;

		if (this.isApplyingData)
			return;

		Task.Run(() => ApplyCharacterData(characterData));
	}

	private async Task ApplyCharacterData(CharacterData characterData)
	{
		if (Plugin.Instance == null)
			return;

		if (this.isApplyingData)
			return;

		this.isApplyingData = true;
		Plugin plugin = Plugin.Instance;

		try
		{
			if (lastData == null || !characterData.IsPenumbraReplacementsSame(lastData))
			{
				Plugin.Log.Information($"{this.CharacterName}@{this.World} > Penumbra files");
			}

			if ((lastData == null && characterData.PenumbraManipulations != null)
				|| (lastData != null && characterData.PenumbraManipulations != lastData.PenumbraManipulations))
			{
				Plugin.Log.Information($"{this.CharacterName}@{this.World} > Penumbra meta");
			}

			if ((lastData == null && characterData.CustomizePlus != null)
				|| (lastData != null && characterData.CustomizePlus != lastData.CustomizePlus))
			{
				Plugin.Log.Information($"{this.CharacterName}@{this.World} > Customize+");
			}

			if ((lastData == null && characterData.Glamourer != null)
				|| (lastData != null && characterData.Glamourer != lastData.Glamourer))
			{
				Plugin.Log.Information($"{this.CharacterName}@{this.World} > Glamourer");

				if (characterData.Glamourer != null)
				{
					await plugin.Glamourer.SetState(this.ObjectTableIndex, characterData.Glamourer);
				}
				else
				{
					////await plugin.Glamourer.ClearState(this.ObjectTableIndex, characterData.Glamourer);
				}
			}

			if ((lastData == null && characterData.Heels != null)
				|| (lastData != null && characterData.Heels != lastData.Heels))
			{
				Plugin.Log.Information($"{this.CharacterName}@{this.World} > Heels");
			}

			if ((lastData == null && characterData.Honorific != null)
				|| (lastData != null && characterData.Honorific != lastData.Honorific))
			{
				Plugin.Log.Information($"{this.CharacterName}@{this.World} > Honorific");
			}

			if ((lastData == null && characterData.Moodles != null)
				|| (lastData != null && characterData.Moodles != lastData.Moodles))
			{
				Plugin.Log.Information($"{this.CharacterName}@{this.World} > Moodles");
			}

			if ((lastData == null && characterData.PetNames != null)
				|| (lastData != null && characterData.PetNames != lastData.PetNames))
			{
				Plugin.Log.Information($"{this.CharacterName}@{this.World} > Pet Names");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error applying character data");
		}

		this.isApplyingData = false;
		lastData = characterData;
	}
}