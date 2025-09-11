// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System;
using System.Collections.Generic;
using System.Net;
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
	public readonly Configuration.Pair Pair;

	public Status CurrentStatus { get; private set; } = Status.None;
	private readonly CancellationTokenSource tokenSource = new();
	private CharacterData? lastData;
	private bool isApplyingData = false;
	private Connection? connection;
	private readonly ConnectionManager network;
	private readonly ushort objectIndex;

	public delegate void CharacterSyncDelegate(CharacterSync character);

	public event CharacterSyncDelegate? Connected;
	public event CharacterSyncDelegate? Disconnected;

	public CharacterSync(ConnectionManager network, Configuration.Pair pair, ushort objectIndex)
	{
		this.objectIndex = objectIndex;
		this.network = network;
		this.Pair = pair;

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

		// They've established a connection back.
		Connected,

		// The connection was terminated.
		Disconnected,
	}

	public Connection? Connection => this.connection;
	public bool IsConnected => this.CurrentStatus == Status.Connected;

	public void SendIAm()
	{
		string? fingerprint = Plugin.Instance?.LocalCharacter?.GetFingerprint();
		if (fingerprint == null)
			return;

		byte[] data = Encoding.UTF8.GetBytes(fingerprint);
		this.Send(Objects.IAm, data);
	}

	public void Send(byte objectType, byte[] data)
	{
		if (this.connection == null || !this.connection.IsConnected)
			return;

		this.connection.Send(objectType, data);
	}

	public void Reconnect()
	{
		if (this.CurrentStatus == Status.Searching
			|| this.CurrentStatus == Status.Connecting
			|| this.CurrentStatus == Status.Handshake)
			return;

		if (this.tokenSource.IsCancellationRequested)
			return;

		if (this.connection != null)
		{
			this.connection.Received -= this.OnReceived;
			this.connection.Disconnected -= this.OnDisconnected;
			this.connection.Dispose();
		}


		this.CurrentStatus = Status.None;
		Task.Run(this.Connect);
	}

	public bool SetConnection(Connection connection)
	{
		if (this.CurrentStatus != Status.Listening)
			return false;

		this.connection = connection;

		this.SetupConnection();
		this.CurrentStatus = Status.Connected;
		this.Connected?.Invoke(this);

		this.SendIAm();
		return true;
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
			string fingerprint = Encoding.UTF8.GetString(data);
			this.OnIAm(connection, fingerprint);
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
			this.connection.Dispose();
		}

		if (!tokenSource.IsCancellationRequested)
			tokenSource.Cancel();

		tokenSource.Dispose();

		this.Disconnected?.Invoke(this);
	}

	public bool Update()
	{
		if (this.Pair.IsTestPair)
			return true;

		IGameObject? obj = Plugin.ObjectTable[this.objectIndex];
		if (obj == null)
			return false;

		if (obj is not IPlayerCharacter character)
			return false;

		if (character.Name.ToString() != this.Pair.CharacterName
			|| character.HomeWorld.Value.Name != this.Pair.World)
			return false;

		return true;
	}

	public void SendData(CharacterData data)
	{
		if (this.connection == null)
			return;

		if (!this.connection.IsConnected)
			return;

		string json = JsonConvert.SerializeObject(data);
		byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
		this.connection.Send(Objects.CharacterData, jsonBytes);
	}

	public void OnCharacterData(Connection? connection, CharacterData characterData)
	{
		// Sanity check
		if (connection != this.connection)
			return;

		if (this.isApplyingData)
			return;

		Task.Run(() => ApplyCharacterData(characterData));
	}

	private async Task Connect()
	{
		try
		{
			if (Plugin.Instance == null || Plugin.Instance.LocalCharacter == null)
				return;

			if (this.Pair.IsTestPair)
			{
				this.CurrentStatus = Status.Connected;
				return;
			}

			// We're the client.
			this.CurrentStatus = Status.Searching;
			SyncStatus request = new();
			request.Fingerprint = this.Pair.GetFingerprint();

			SyncStatus? response = null;
			foreach (string indexServer in Configuration.Current.IndexServers)
			{
				try
				{
					response = await request.Send(indexServer);
				}
				catch (Exception ex)
				{
					Plugin.Log.Warning(ex, $"Error requesting peer from index server: {indexServer}");
				}

				if (this.tokenSource.IsCancellationRequested)
					return;
			}

			if (this.tokenSource.IsCancellationRequested)
				return;

			IPAddress? address = null;
			if (response == null
				|| response.Address == null
				|| !IPAddress.TryParse(response.Address, out address)
				|| address == null)
			{
				this.CurrentStatus = Status.Offline;
				await Task.Delay(30000, this.tokenSource.Token);
				this.Reconnect();
				return;
			}

			int sort = Plugin.Instance.LocalCharacter.CompareTo(this.Pair);
			if (sort >= 0)
			{
				// We're the host.
				this.CurrentStatus = Status.Listening;
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
			}

			if (this.connection == null)
			{
				this.CurrentStatus = Status.ConnectionFailed;
				await Task.Delay(10000);
				this.Reconnect();
				return;
			}

			// Send who packet to identify ourselves.
			this.CurrentStatus = Status.Handshake;

			this.SetupConnection();

			if (this.tokenSource.IsCancellationRequested)
				return;

			while (this.CurrentStatus == Status.Handshake)
			{
				if (this.tokenSource.IsCancellationRequested
					|| this.connection == null
					|| !this.connection.IsConnected)
					return;

				this.SendIAm();
				await Task.Delay(5000);
			}
		}
		catch (TaskCanceledException)
		{
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error connecting to character sync");
		}
	}

	private void OnDisconnected(Connection connection)
	{
		Plugin.Log.Information($"Connection to {this.Pair} was closed.");
		this.CurrentStatus = Status.Disconnected;
		this.Disconnected?.Invoke(this);

		this.Reconnect();
	}

	private void OnIAm(Connection connection, string fingerprint)
	{
		// Sanity check
		if (connection != this.connection || fingerprint != this.Pair.GetFingerprint())
			return;

		if (this.CurrentStatus == Status.Handshake)
		{

		}
		else if (this.CurrentStatus == Status.Listening)
		{
			Task.Run(this.SendIAm);
		}

		Plugin.Log.Info($"Connected to {this.Pair} at {connection.EndPoint}");
		this.CurrentStatus = Status.Connected;
		this.Connected?.Invoke(this);
	}

	public async Task ApplyCharacterData(CharacterData characterData)
	{
		if (this.isApplyingData)
			return;

		this.isApplyingData = true;

		await ApplySyncData(characterData.Syncs, this.objectIndex);
		await ApplySyncData(characterData.MountOrMinionSyncs, (ushort)(this.objectIndex + 1));

		this.isApplyingData = false;
		this.lastData = characterData;
	}

	private async Task ApplySyncData(Dictionary<string, string?> sync, ushort objectIndex)
	{
		foreach ((string key, string? content) in sync)
		{
			try
			{
				if (this.tokenSource.IsCancellationRequested)
					return;

				if (Plugin.Instance == null)
					return;

				SyncProviderBase? provider = Plugin.Instance?.GetSyncProvider(key);
				if (provider == null)
					continue;

				string? lastContent = null;
				this.lastData?.Syncs.TryGetValue(key, out lastContent);
				await provider.Deserialize(lastContent, content, this, objectIndex);
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, $"Error applying sync data: {key}");
			}
		}
	}

	internal void Flush()
	{
		this.lastData = null;
	}
}
