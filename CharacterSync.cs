// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Newtonsoft.Json;
using PeerSync.Network;
using PeerSync.Online;
using PeerSync.SyncProviders;

public class CharacterSync : IDisposable
{
	public readonly Configuration.Peer Peer;

	private readonly CancellationTokenSource tokenSource = new();
	private readonly ConnectionManager network;
	private readonly ushort objectIndex;

	private bool isApplyingData = false;
	private Connection? connection;

	public CharacterSync(ConnectionManager network, Configuration.Peer peer, ushort objectIndex)
	{
		this.objectIndex = objectIndex;
		this.network = network;
		this.Peer = peer;

		Task.Run(this.Connect, this.tokenSource.Token);
	}

	public delegate void CharacterSyncDelegate(CharacterSync character);

	public event CharacterSyncDelegate? Connected;
	public event CharacterSyncDelegate? Disconnected;

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

	public Status CurrentStatus { get; private set; } = Status.None;
	public Exception? LastException { get; private set; }
	public Connection? Connection => this.connection;
	public bool IsConnected => this.CurrentStatus == Status.Connected;
	public CharacterData? LastData { get; private set; }

	public void SendIAm()
	{
		string? fingerprint = Plugin.Instance?.LocalCharacter?.GetFingerprint();
		if (fingerprint == null)
			return;

		byte[] data = Encoding.UTF8.GetBytes(fingerprint);
		this.Send(PacketTypes.IAm, data);
	}

	public void Send(PacketTypes type, byte[] data)
	{
		if (this.connection == null || !this.connection.IsConnected)
			return;

		this.connection.Send(type, data);
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
			this.connection = null;
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

	public void Reset()
	{
		this.LastData = null;

		if (Plugin.Instance == null)
			return;

		foreach (SyncProviderBase provider in Plugin.Instance.SyncProviders)
		{
			provider.Reset(this, this.objectIndex);
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

		if (!this.tokenSource.IsCancellationRequested)
			this.tokenSource.Cancel();

		this.tokenSource.Dispose();

		this.Disconnected?.Invoke(this);
	}

	public bool Update()
	{
		IGameObject? obj = Plugin.ObjectTable[this.objectIndex];
		if (obj == null)
			return false;

		if (obj is not IPlayerCharacter character)
			return false;

		if (character.Name.ToString() != this.Peer.CharacterName
			|| character.HomeWorld.Value.Name != this.Peer.World)
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
		this.connection.Send(PacketTypes.CharacterData, jsonBytes);
	}

	public void OnCharacterData(Connection? connection, CharacterData characterData)
	{
		// Sanity check
		if (connection != this.connection)
			return;

		if (!this.IsConnected)
			return;

		// Do not sync characters if the local player is in combat
		// or is loading areas.
		if (Plugin.Condition[ConditionFlag.InCombat]
			|| Plugin.Condition[ConditionFlag.BetweenAreas]
			|| Plugin.Condition[ConditionFlag.BetweenAreas51])
			return;

		if (this.isApplyingData)
			return;

		Task.Run(() => this.ApplyCharacterData(characterData));
	}

	private async Task Connect()
	{
		try
		{
			if (Plugin.Instance == null || Plugin.Instance.LocalCharacter == null)
				return;

			this.LastException = null;

			this.CurrentStatus = Status.Searching;
			GetPeer request = new();
			request.Fingerprint = this.Peer.GetFingerprint();

			GetPeer? response = null;
			foreach (string indexServer in Configuration.Current.IndexServers)
			{
				try
				{
					response = await request.Send(indexServer);
				}
				catch (Exception ex)
				{
					Plugin.Log.Error(ex, $"Error requesting peer from index server: {indexServer}");
				}

				if (this.tokenSource.IsCancellationRequested)
					return;
			}

			if (this.tokenSource.IsCancellationRequested)
				return;

#if DEBUG
			Plugin.Log.Info($"connecting to peer: {this.Peer.CharacterName}:  {response?.LocalAddress} / {response?.Address} : {response?.Port}");
#endif

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

			int sort = Plugin.Instance.LocalCharacter.CompareTo(this.Peer);
			if (sort >= 0)
			{
				// We're the host.
				this.CurrentStatus = Status.Listening;
				return;
			}

			// We're the client.
			this.CurrentStatus = Status.Connecting;

			CancellationTokenSource localCancel = new();
			CancellationTokenSource wideCancel = new();

			Task<(Connection?, Exception?)>? localConnectTask = null;
			IPAddress.TryParse(response.LocalAddress, out IPAddress? localAddress);
			if (localAddress != null)
			{
				localConnectTask = this.network.Connect(new(localAddress, response.Port), localCancel.Token);
			}

			Task<(Connection?, Exception?)> wideConnectTask = this.network.Connect(new(address, response.Port), wideCancel.Token);

			(Connection? Success, Exception? Failure) localConnection;
			try
			{
				if (localConnectTask != null)
				{
					localConnection = await localConnectTask;

					if (localConnection.Success != null)
					{
						this.connection = localConnection.Success;
						wideCancel.Cancel();
					}
					else
					{
						this.LastException = localConnection.Failure;
					}
				}
			}
			catch (Exception)
			{
			}

			(Connection? Success, Exception? Failure) wideConnection;
			try
			{
				wideConnection = await wideConnectTask;

				if (wideConnection.Success != null)
				{
					this.connection = wideConnection.Success;
					localCancel.Cancel();
				}
				else
				{
					this.LastException = wideConnection.Failure;
				}
			}
			catch (Exception)
			{
			}

			if (this.connection == null)
			{
				this.CurrentStatus = Status.ConnectionFailed;

				if (this.LastException != null)
					Plugin.Log.Warning(this.LastException, "Failed to connect to peer");

				await Task.Delay(60000, this.tokenSource.Token);
				this.Reconnect();
				return;
			}

			this.LastException = null;

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

	private void SetupConnection()
	{
		if (this.connection == null)
			throw new Exception();

		this.connection.Received += this.OnReceived;
		this.connection.Disconnected += this.OnDisconnected;
	}

	private void OnReceived(Connection connection, PacketTypes type, byte[] data)
	{
		if (type == PacketTypes.IAm)
		{
			string fingerprint = Encoding.UTF8.GetString(data);
			this.OnIAm(connection, fingerprint);
		}
		else if (type == PacketTypes.CharacterData)
		{
			string json = Encoding.UTF8.GetString(data);
			CharacterData? characterData = JsonConvert.DeserializeObject<CharacterData>(json);
			if (characterData == null)
				throw new Exception();

			this.OnCharacterData(connection, characterData);
		}
	}

	private void OnDisconnected(Connection connection)
	{
		Plugin.Log.Information($"Connection to {this.Peer} was closed.");
		this.CurrentStatus = Status.Disconnected;

		this.Reset();

		this.Disconnected?.Invoke(this);

		this.Reconnect();
	}

	private void OnIAm(Connection connection, string fingerprint)
	{
		// Sanity check
		if (connection != this.connection || fingerprint != this.Peer.GetFingerprint())
			return;

		if (this.CurrentStatus == Status.Listening)
		{
			this.SendIAm();
		}

		Plugin.Log.Info($"Connected to {this.Peer} at {connection.EndPoint}");
		this.CurrentStatus = Status.Connected;
		this.Connected?.Invoke(this);
	}

	private async Task ApplyCharacterData(CharacterData characterData)
	{
		if (this.isApplyingData)
			return;

		this.isApplyingData = true;

		await this.ApplySyncData(
			characterData.Character,
			this.LastData?.Character,
			this.objectIndex);

		await this.ApplySyncData(
			characterData.MountOrMinion,
			this.LastData?.MountOrMinion,
			(ushort)(this.objectIndex + 1));

		await Plugin.Framework.RunOnUpdate();
		IGameObject? pet = null;
		unsafe
		{
			IGameObject? character = Plugin.ObjectTable[this.objectIndex];
			if (character != null)
			{
				BattleChara* pPet = CharacterManager.Instance()->LookupPetByOwnerObject((BattleChara*)character.Address);
				if (pPet != null)
				{
					pet = Plugin.ObjectTable[pPet->ObjectIndex];
				}
			}
		}

		await Plugin.Framework.RunOutsideUpdate();
		if (pet != null)
		{
			await this.ApplySyncData(
				characterData.Pet,
				this.LastData?.Pet,
				pet.ObjectIndex);
		}

		this.isApplyingData = false;
		this.LastData = characterData;
	}

	private async Task ApplySyncData(
		Dictionary<string, string?> sync,
		Dictionary<string, string?>? lastSync,
		ushort objectIndex)
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
				lastSync?.TryGetValue(key, out lastContent);

				await provider.Deserialize(lastContent, content, this, objectIndex);
			}
			catch (TaskCanceledException)
			{
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, $"Error applying sync data: {key}");
			}
		}
	}
}
