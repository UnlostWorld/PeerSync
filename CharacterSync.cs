// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Newtonsoft.Json;
using PeerSync.Network;
using PeerSync.Online;

public class CharacterSync : IDisposable
{
	public readonly string CharacterName;
	public readonly string World;
	public readonly string Identifier;

	public Status CurrentStatus { get; private set; } = Status.None;
	private readonly CancellationTokenSource tokenSource = new();
	private CharacterData? lastData;
	private bool isApplyingData = false;
	private Connection? connection;
	private readonly ConnectionManager network;

	public delegate void CharacterSyncDelegate(CharacterSync character);

	public event CharacterSyncDelegate? Connected;
	public event CharacterSyncDelegate? Disconnected;

	public CharacterSync(ConnectionManager network, string characterName, string world, string password)
	{
		this.network = network;
		this.CharacterName = characterName;
		this.World = world;
		this.Identifier = GetIdentifier(characterName, world, password);

		Task.Run(this.Connect, tokenSource.Token);
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
	public Connection? Connection => this.connection;

	public static string GetIdentifier(string characterName, string world, string password, int iterations = 1000)
	{
		string pluginVersion = Plugin.PluginInterface.Manifest.AssemblyVersion.ToString();
		// The Identifier is sent to the server, and it contains the character name and world, so
		// ensure its cryptographically secure in case of bad actors controlling servers.
		string input = $"{characterName}{world}";
		for (int i = 0; i < iterations; i++)
		{
			HashAlgorithm algorithm = SHA256.Create();
			byte[] bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes($"{input}{password}{pluginVersion}"));
			input = BitConverter.ToString(bytes);
			input = input.Replace("-", string.Empty, StringComparison.Ordinal);
		}

		return input;
	}

	public async Task SendAsync(byte objectType, byte[] data)
	{
		if (this.connection == null || !this.connection.IsConnected)
			return;

		await this.connection.SendAsync(objectType, data);
	}

	public void Reconnect()
	{
		if (this.CurrentStatus == Status.Searching
			|| this.CurrentStatus == Status.Connecting
			|| this.CurrentStatus == Status.Handshake)
			return;

		Plugin.Log?.Info($"Reconnecting...");

		this.CurrentStatus = Status.None;

		////Task.Run(this.Connect);
	}

	public void SetConnection(Connection connection)
	{
		if (this.CurrentStatus != Status.Listening)
			return;

		Plugin.Log.Info($"{this.CharacterName} @ {this.World} Connected from {connection.EndPoint}");

		this.connection = connection;

		this.SetupConnection();
		this.CurrentStatus = Status.Connected;
		this.Connected?.Invoke(this);
	}

	private void SetupConnection()
	{
		if (this.connection == null)
			throw new Exception();

		this.connection.Received += this.OnReceived;
		this.connection.Disconnected += this.OnDisconnected;
	}

	private void OnReceived(Connection connection, byte typeId, byte[] data)
	{
		if (typeId == Objects.IAm)
		{
			string identifier = Encoding.UTF8.GetString(data);
			this.OnIAm(connection, identifier);
		}
		else if (typeId == Objects.CharacterData)
		{
			string json = Encoding.UTF8.GetString(data);
			CharacterData? characterData = JsonConvert.DeserializeObject<CharacterData>(json);
			if (characterData == null)
				throw new Exception();

			this.OnCharacterData(connection, characterData);
		}
	}

	public void Dispose()
	{
		if (this.connection != null)
		{
			this.connection.Received -= this.OnReceived;
			this.connection.Disconnected -= this.OnDisconnected;
		}

		if (!tokenSource.IsCancellationRequested)
			tokenSource.Cancel();

		tokenSource.Dispose();

		this.Disconnected?.Invoke(this);
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

	public async Task SendData(CharacterData data)
	{
		if (this.connection == null || Plugin.Instance?.LocalCharacterIdentifier == null)
			return;

		if (!this.connection.IsConnected)
			return;

		string identifier = Plugin.Instance.LocalCharacterIdentifier;
		byte[] identifierBytes = Encoding.UTF8.GetBytes(identifier);
		await this.connection.SendAsync(Objects.IAm, identifierBytes);

		string json = JsonConvert.SerializeObject(data);
		byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
		await this.connection.SendAsync(Objects.CharacterData, jsonBytes);
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

			if (this.tokenSource.IsCancellationRequested)
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
					IPEndPoint endPoint = new(localAddress, response.Port);
					this.connection = await this.network.Connect(endPoint, tokenSource.Token);
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
					IPEndPoint endPoint = new(address, response.Port);
					this.connection = await this.network.Connect(endPoint, tokenSource.Token);
				}
			}
			catch (Exception)
			{
				this.connection = null;
				this.CurrentStatus = Status.ConnectionFailed;
				return;
			}

			if (this.connection == null)
			{
				this.CurrentStatus = Status.ConnectionFailed;
				return;
			}

			// Send who packet to identify ourselves.
			this.CurrentStatus = Status.Handshake;

			this.SetupConnection();

			if (this.tokenSource.IsCancellationRequested)
				return;

			string? identifier = Plugin.Instance?.LocalCharacterIdentifier;
			if (identifier == null)
				throw new Exception("No identifier");

			byte[] identifierBytes = Encoding.UTF8.GetBytes(identifier);

			int attempts = 0;
			while (this.CurrentStatus == Status.Handshake && attempts < 10)
			{
				if (this.tokenSource.IsCancellationRequested)
					return;

				attempts++;
				await this.connection.SendAsync(Objects.IAm, identifierBytes);
				await Task.Delay(3000);
			}

			if (this.tokenSource.IsCancellationRequested)
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

	private void OnDisconnected(Connection connection)
	{
		Plugin.Log.Information($"Connection to {this.CharacterName} @ {this.World} was closed.");
		this.CurrentStatus = Status.Disconnected;
		this.Disconnected?.Invoke(this);

		////this.Reconnect();
	}

	private void OnIAm(Connection connection, string identifier)
	{
		// Sanity check
		if (connection != this.connection || identifier != this.Identifier)
			return;

		if (this.CurrentStatus == Status.Handshake)
		{
			Plugin.Log.Info($"Connected to {this.CharacterName} @ {this.World} at {connection.EndPoint}");
			this.CurrentStatus = Status.Connected;
			this.Connected?.Invoke(this);
		}
	}

	private void OnCharacterData(Connection connection, CharacterData characterData)
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
		if (this.isApplyingData)
			return;

		this.isApplyingData = true;

		foreach ((string key, string? content) in characterData.Syncs)
		{
			try
			{
				if (Plugin.Instance == null)
					return;

				SyncProviderBase? provider = Plugin.Instance?.GetSyncProvider(key);
				if (provider == null)
					continue;

				////Plugin.Log.Information($"{this.CharacterName}@{this.World} > {key}");
				await provider.Deserialize(content, this);
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, $"Error applying sync data: {key}");
			}
		}

		this.isApplyingData = false;
		lastData = characterData;
	}
}