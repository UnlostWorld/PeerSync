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
	public readonly string? GroupFingerprint;
	public readonly string MemberFingerprint;
	public readonly string Name;
	public readonly string World;
	public readonly string LocalFingerprint;
	public readonly bool IsGroup;

	private readonly CancellationTokenSource tokenSource = new();
	private readonly ConnectionManager network;
	private readonly ushort objectIndex;

	private bool isApplyingData = false;
	private Connection? connection;
	private int connectionAttempts = 0;

	public CharacterSync(ConnectionManager network, Configuration.Peer peer, ushort objectIndex)
	{
		if (Plugin.Instance == null)
			throw new InvalidOperationException("Attempt to sync with plugin in invalid state");

		if (Plugin.Instance.LocalCharacter == null)
			throw new InvalidOperationException("Attempt to sync without local character");

		if (peer.CharacterName == null || peer.World == null)
			throw new InvalidOperationException("Attempt to sync with invalid peer");

		this.objectIndex = objectIndex;
		this.network = network;
		this.MemberFingerprint = peer.GetFingerprint();
		this.Name = peer.CharacterName;
		this.World = peer.World;
		this.LocalFingerprint = Plugin.Instance.LocalCharacter.GetFingerprint();

		Task.Run(this.Connect, this.tokenSource.Token);
	}

	public CharacterSync(ConnectionManager network, Configuration.Group group, string memberFingerprint, string name, string world, ushort objectIndex)
	{
		if (Plugin.Instance == null)
			throw new InvalidOperationException("Attempt to sync with plugin in invalid state");

		if (Plugin.Instance.LocalCharacter == null)
			throw new InvalidOperationException("Attempt to sync without local character");

		string? localFingerprint = group.GetMemberFingerprint(Plugin.Instance.LocalCharacter);
		if (localFingerprint == null)
			throw new Exception("failed to get local fingerprint");

		this.objectIndex = objectIndex;
		this.network = network;
		this.GroupFingerprint = group.GetFingerprint();
		this.MemberFingerprint = memberFingerprint;
		this.Name = name;
		this.World = world;
		this.LocalFingerprint = localFingerprint;

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

		// This peer is being handled by Lightless
		Lightless,

		// This peer is on the block list
		Blocked,
	}

	public Status CurrentStatus { get; private set; } = Status.None;
	public Exception? LastException { get; private set; }
	public Connection? Connection => this.connection;
	public bool IsConnected => this.CurrentStatus == Status.Connected;
	public int ConnectionAttempts => this.connectionAttempts;
	public CharacterData? LastData { get; private set; }

	public void SendIAm()
	{
		byte[] data = Encoding.UTF8.GetBytes(this.LocalFingerprint);
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

		if (character.Name.ToString() != this.Name
			|| character.HomeWorld.Value.Name != this.World)
		{
			return false;
		}

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
			if (Plugin.Instance == null)
				return;

			if (Plugin.Instance.LocalCharacter == null)
			{
				await Task.Delay(3000, this.tokenSource.Token);
				this.Reconnect();
				return;
			}

			this.LastException = null;

			if (await Plugin.Lightless.GetIsGameObjectHandled(this.objectIndex))
			{
				this.CurrentStatus = Status.Lightless;
				return;
			}

			if (Configuration.Current.GetIsBlocked(this.Name, this.World))
			{
				this.CurrentStatus = Status.Blocked;
				return;
			}

			this.CurrentStatus = Status.Searching;
			GetPeer request = new();
			request.GroupFingerprint = this.GroupFingerprint;
			request.MemberFingerprint = this.MemberFingerprint;

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

			IPAddress? address = null;
			if (response == null
				|| response.Address == null
				|| !IPAddress.TryParse(response.Address, out address)
				|| address == null)
			{
				this.CurrentStatus = Status.Offline;
				await Task.Delay(10000, this.tokenSource.Token);
				this.Reconnect();
				return;
			}

			// invert connection direction on each reattempt.
			this.connectionAttempts++;
			bool invertDirection = this.connectionAttempts % 2 == 0;

			bool host = this.LocalFingerprint.CompareTo(this.MemberFingerprint) >= 0;

			if (invertDirection)
				host = !host;

			if (host)
			{
				// We're the host.
				this.CurrentStatus = Status.Listening;

				// If we haven't gotten a connection within 20 seconds, reset and try again.
				await Task.Delay(20000, this.tokenSource.Token);
				if (this.CurrentStatus == Status.Listening)
				{
					this.Reconnect();
				}

				return;
			}

			// We're the client.
			this.CurrentStatus = Status.Connecting;

			CancellationTokenSource localCancel = new();
			CancellationTokenSource wideCancel = new();

			wideCancel.CancelAfter(5000);
			localCancel.CancelAfter(5000);

			Task wideConnectTask = Task.Run(
				async () =>
				{
					(Connection? connection, Exception? exception) =
						await this.network.Connect(new(address, response.Port), wideCancel.Token);

					if (connection != null)
					{
						this.connection = connection;
						wideCancel.Cancel();
					}
					else if (exception is not TaskCanceledException)
					{
						this.LastException = exception;
					}
				},
				wideCancel.Token);

			IPAddress.TryParse(response.LocalAddress, out IPAddress? localAddress);
			Task? localConnectTask = null;
			if (localAddress != null)
			{
				localConnectTask = Task.Run(
					async () =>
					{
						(Connection? connection, Exception? exception) =
							await this.network.Connect(new(localAddress, response.Port), localCancel.Token);

						if (connection != null)
						{
							this.connection = connection;
							wideCancel.Cancel();
						}
						else if (exception is not TaskCanceledException)
						{
							this.LastException = exception;
						}
					},
					localCancel.Token);
			}

			if (localConnectTask != null)
				await localConnectTask;

			await wideConnectTask;

			if (this.connection == null)
			{
				this.CurrentStatus = Status.ConnectionFailed;

				if (this.LastException != null)
					Plugin.Log.Warning(this.LastException, "Failed to connect to peer");
				await Task.Delay(1000, this.tokenSource.Token);
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
				await Task.Delay(1000);
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
#if DEBUG
		Plugin.Log.Information($"Connection to {this.Name} @ {this.World}  was closed.");
#endif

		this.CurrentStatus = Status.Disconnected;

		this.Reset();

		this.Disconnected?.Invoke(this);

		this.Reconnect();
	}

	private void OnIAm(Connection connection, string fingerprint)
	{
		// Sanity check
		if (connection != this.connection || fingerprint != this.MemberFingerprint)
			return;

		if (this.CurrentStatus == Status.Listening)
		{
			this.SendIAm();
		}

#if DEBUG
		Plugin.Log.Information($"Connected to {this.Name} @ {this.World} at {connection.EndPoint}");
#endif

		this.CurrentStatus = Status.Connected;
		this.Connected?.Invoke(this);
	}

	private async Task ApplyCharacterData(CharacterData characterData)
	{
		if (this.isApplyingData)
			return;

		if (await Plugin.Lightless.GetIsGameObjectHandled(this.objectIndex))
		{
			this.CurrentStatus = Status.Lightless;
			this.Reconnect();
			return;
		}

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
