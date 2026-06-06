// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Connections;

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Newtonsoft.Json;
using PeerSync;
using PeerSync.Index;
using PeerSync.Network;
using PeerSync.Online;
using PeerSync.Overlays;
using PeerSync.SyncProviders;
using PeerSync.UI;

using CharacterData = PeerSync.CharacterData;

public partial class CharacterConnection : IDisposable
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
	private static readonly TimeSpan ReIndex = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan SearchDelay = TimeSpan.FromSeconds(1);

	private readonly CancellationTokenSource cancellationTokenSource = new();
	private readonly Dictionary<SyncProviderBase, SyncProgressBase> characterProgress = new();
	private readonly Dictionary<SyncProviderBase, SyncProgressBase> mountProgress = new();
	private readonly Dictionary<SyncProviderBase, SyncProgressBase> petProgress = new();
	private DateTime lastSeen;
	private DateTime lastIndexAttempt;
	private DateTime lastSearch;
	private Connection? outgoingConnection;
	private Connection? incomingConnection;
	private bool isPreparingData = false;
	private bool isApplyingData = false;
	private Exception? lastConnectionException;
	private TransferOverlay? overlay;
	private ushort objectIndex;
	private States lastState;
	private CharacterData? characterData;

	public CharacterConnection(IPlayerCharacter character)
	{
		this.CharacterId = character.GetId();
		this.CharacterName = character.GetName();
		this.CharacterWorld = character.GetHomeWorld();
		this.objectIndex = character.ObjectIndex;

		this.lastSeen = DateTime.Now;
		this.lastIndexAttempt = DateTime.MinValue;
		this.lastSearch = DateTime.Now;

		this.IsPeer = false;

		Task.Run(this.FingerprintIndexConnect);
	}

	public delegate void DataDelegate(CharacterConnection character, PacketTypes type, byte[] data);

	public event DataDelegate? Received;

	public enum States
	{
		Found,
		NotFound,
		TimedOut,
	}

	public string CharacterId { get; init; }
	public string CharacterName { get; init; }
	public string CharacterWorld { get; init; }

	public CharacterData? LastData { get; private set; }
	public bool IsConnected { get; private set; }
	public bool IsPeer { get; private set; }
	public bool IsBlocked => Configuration.Current.GetIsBlocked(this.CharacterName, this.CharacterWorld);
	public bool IsOffline { get; private set; }
	public bool IsWaitingForData { get; private set; }

	public TimeSpan TimeSinceLastSeen => DateTime.Now - this.lastSeen;
	public TimeSpan TimeSinceLastSearch => DateTime.Now - this.lastSearch;
	public TimeSpan TimeSinceLastIndexAttempt => DateTime.Now - this.lastIndexAttempt;

	public void Reset()
	{
		this.LastData = null;
		this.characterData = null;

		foreach (SyncProviderBase provider in Plugin.Sync.Providers)
		{
			try
			{
				provider.Reset(this, this.objectIndex);
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, $"Error resetting character with provider: {provider}");
			}
		}

		foreach (SyncProviderBase provider in Plugin.Sync.Providers)
		{
			try
			{
				provider.Reset(this, (ushort)(this.objectIndex + 1));
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, $"Error resetting mount / minion with provider: {provider}");
			}
		}

		IGameObject? pet = this.GetPet();
		if (pet != null)
		{
			foreach (SyncProviderBase provider in Plugin.Sync.Providers)
			{
				try
				{
					provider.Reset(this, pet.ObjectIndex);
				}
				catch (Exception ex)
				{
					Plugin.Log.Error(ex, $"Error resetting mount / minion with provider: {provider}");
				}
			}
		}

		this.characterProgress.Clear();
		this.mountProgress.Clear();
		this.petProgress.Clear();
	}

	public IPlayerCharacter? GetCharacter()
	{
		IGameObject? obj = Plugin.ObjectTable[this.objectIndex];
		if (obj is IPlayerCharacter character
			&& character.GetName() == this.CharacterName
			&& character.GetHomeWorld() == this.CharacterWorld)
		{
			return character;
		}

		return null;
	}

	public States Update()
	{
		IGameObject? obj = Plugin.ObjectTable[this.objectIndex];
		if (obj is IPlayerCharacter character
			&& character.GetName() == this.CharacterName
			&& character.GetHomeWorld() == this.CharacterWorld)
		{
			this.lastSeen = DateTime.Now;

			// Begin connecting if this character is offline, enough time has passed,
			// and the index servers are connected.
			if (this.outgoingConnection == null
				&& this.TimeSinceLastIndexAttempt > ReIndex
				&& Plugin.Index.HasInitialIndexingCompleted
				&& Plugin.Characters.Current != null)
			{
				Task.Run(this.FingerprintIndexConnect);
			}

			this.lastState = States.Found;

			this.UpdateOverlay();
			this.ApplySyncData();

			return States.Found;
		}
		else if (this.TimeSinceLastSearch > SearchDelay)
		{
			this.lastSearch = DateTime.Now;

			foreach (IPlayerCharacter tCharacter in Plugin.ObjectTable.PlayerObjects)
			{
				if (tCharacter.GetName() == this.CharacterName
					&& tCharacter.GetHomeWorld() == this.CharacterWorld)
				{
					this.objectIndex = tCharacter.ObjectIndex;
					this.lastState = States.Found;
					return States.Found;
				}
			}
		}

		if (this.TimeSinceLastSeen > Timeout)
		{
			this.lastState = States.TimedOut;
			return States.TimedOut;
		}

		this.lastState = States.NotFound;
		return States.NotFound;
	}

	public void Dispose()
	{
		if (this.IsConnected)
		{
			this.OnDisconnected();
		}

		this.cancellationTokenSource.Cancel();

		this.Reset();
	}

	public void SetOutgoingNetworkConnection(Connection connection)
	{
		if (this.outgoingConnection != null)
		{
			this.outgoingConnection.Received -= this.OnReceived;
			this.outgoingConnection.Disconnected -= this.OnOutgoingDisconnected;
			this.outgoingConnection.Dispose();
		}

		if (connection == this.incomingConnection)
			throw new Exception($"Attempt to set current incoming connection ({connection.Name}) as outgoing");

		this.outgoingConnection = connection;
		this.outgoingConnection.Received += this.OnReceived;
		this.outgoingConnection.Disconnected += this.OnOutgoingDisconnected;
		this.IsWaitingForData = true;
		this.SendLatestCharacterData();
	}

	public void SetIncomingNetworkConnection(Connection connection)
	{
		if (this.incomingConnection != null)
		{
			this.incomingConnection.Received -= this.OnReceived;
			this.incomingConnection.Disconnected -= this.OnIncomingDisconnected;
			this.incomingConnection.Dispose();
		}

		if (connection == this.outgoingConnection)
			throw new Exception($"Attempt to set current outgoing connection ({connection.Name}) as incoming");

		this.incomingConnection = connection;
		this.incomingConnection.Received += this.OnReceived;
		this.incomingConnection.Disconnected += this.OnIncomingDisconnected;
		this.IsWaitingForData = true;
		this.SendLatestCharacterData();
	}

	public void Send(PacketTypes type, byte[] data)
	{
		if (this.outgoingConnection?.IsConnected == true)
		{
			this.outgoingConnection.Send(type, data);
			return;
		}

		if (this.incomingConnection?.IsConnected == true)
		{
			this.incomingConnection.Send(type, data);
			return;
		}
	}

	private void UpdateOverlay()
	{
		if (this.IsConnected && this.overlay == null)
		{
			this.overlay = new(this);
		}
		else if (!this.IsConnected && this.overlay != null)
		{
			this.overlay.Dispose();
			this.overlay = null;
		}
	}

	private async Task<bool> FingerprintIndexConnect()
	{
		try
		{
			this.lastConnectionException = null;
			this.lastIndexAttempt = DateTime.Now;
			GetPeer request = new();

			// Check if this character is a friend
			Configuration.Peer? friend = Configuration.Current.GetFriend(this.CharacterName, this.CharacterWorld);
			if (friend != null)
			{
				request.MemberFingerprint = friend.GetFingerprint();
				if (await this.IndexConnect(request))
				{
					return true;
				}
			}

			// If that character is not a friend, or we could not get their details, check if they're a member of
			// any groups.
			foreach (GroupServer group in Plugin.Index.Groups)
			{
				string memberFingerprint = group.GetMemberFingerprint(this.CharacterName, this.CharacterWorld);

				if (group.IsMember(memberFingerprint))
				{
					request.GroupFingerprint = group.GetFingerprint();
					request.MemberFingerprint = memberFingerprint;
					if (await this.IndexConnect(request))
					{
						return true;
					}
				}
			}

			// We didn't find a valid peer.
			return false;
		}
		catch (Exception ex)
		{
			this.lastConnectionException = ex;
			Plugin.Log.Error(ex, "Error fingerprinting character");
			return false;
		}
	}

	private async Task<bool> IndexConnect(GetPeer request)
	{
		this.IsPeer = true;
		this.IsOffline = false;
		GetPeer? response = null;

		// Check each index sever we have configured for this peer request
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

			if (response != null && response.Address != null)
			{
				// We got a peer, connect to them
				IPAddress.TryParse(response.Address, out IPAddress? address);
				IPAddress.TryParse(response.LocalAddress, out IPAddress? localAddress);

				if (address == null)
					continue;

				if (await this.Connect(address, localAddress, response.Port))
				{
					return true;
				}
			}
		}

		this.IsOffline = true;
		return false;
	}

	private async Task<bool> Connect(IPAddress address, IPAddress? localAddress, ushort port)
	{
		try
		{
			this.lastConnectionException = null;
			Connection outgoingConnection = await Plugin.Connections.Connect(address, localAddress, port);

			if (Plugin.Characters.Current == null)
			{
				outgoingConnection.Dispose();
				return false;
			}
			else
			{
				this.SetOutgoingNetworkConnection(outgoingConnection);
				return true;
			}
		}
		catch (Exception ex)
		{
			this.lastConnectionException = ex;
			return false;
		}
	}

	private void OnIncomingDisconnected(Connection connection)
	{
		connection.Received -= this.OnReceived;
		connection.Disconnected -= this.OnIncomingDisconnected;
		connection.Dispose();
		this.incomingConnection = null;

		if (this.incomingConnection == null && this.outgoingConnection == null)
		{
			this.OnDisconnected();
		}
	}

	private void OnOutgoingDisconnected(Connection connection)
	{
		connection.Received -= this.OnReceived;
		connection.Disconnected -= this.OnOutgoingDisconnected;
		connection.Dispose();
		this.outgoingConnection = null;

		if (this.incomingConnection == null && this.outgoingConnection == null)
		{
			this.OnDisconnected();
		}
	}

	private void OnConnected()
	{
		if (this.IsConnected)
			return;

		Plugin.Log.Debug($"Connected to {this.CharacterId}");

		this.IsConnected = true;
		this.IsPeer = true;

		foreach (SyncProviderBase sync in Plugin.Sync.Providers)
		{
			sync.OnCharacterConnected(this);
		}

		this.SendLatestCharacterData();
	}

	private void SendLatestCharacterData()
	{
		// send the most recent version of our character data to this new connection.
		if (Plugin.Sync.LocalCharacterData != null)
		{
			string json = JsonConvert.SerializeObject(Plugin.Sync.LocalCharacterData);
			byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
			this.Send(PacketTypes.CharacterData, jsonBytes);
		}
	}

	private void OnDisconnected()
	{
		if (!this.IsConnected)
			return;

		Plugin.Log.Debug($"Disconnected from {this.CharacterId}");

		this.IsConnected = false;

		foreach (SyncProviderBase sync in Plugin.Sync.Providers)
		{
			sync.OnCharacterDisconnected(this);
		}
	}

	private void OnReceived(Connection connection, PacketTypes type, byte[] data)
	{
		if (type == PacketTypes.CharacterData)
		{
			string json = Encoding.UTF8.GetString(data);
			CharacterData? characterData = JsonConvert.DeserializeObject<CharacterData>(json);
			if (characterData == null)
				throw new Exception();

			this.OnCharacterData(characterData);
		}
		else if (this.IsConnected)
		{
			this.Received?.Invoke(this, type, data);
		}
	}

	private void OnCharacterData(CharacterData characterData)
	{
		if (this.IsBlocked)
			return;

		if (characterData.CharacterId != this.CharacterId)
		{
			Plugin.Log.Warning($"Unrecognized Character: {characterData.CharacterId}, expected {this.CharacterId}");
			return;
		}

		this.IsWaitingForData = false;

		if (this.lastState != States.Found)
			return;

		this.OnConnected();

		if (Plugin.Sync.Lightless.GetIsGameObjectHandled(this.objectIndex))
			return;

		this.characterData = characterData;

		// Do not sync characters if the local player is in combat
		// or is loading areas.
		if (Plugin.Condition[ConditionFlag.InCombat]
			|| Plugin.Condition[ConditionFlag.BetweenAreas]
			|| Plugin.Condition[ConditionFlag.BetweenAreas51])
			return;

		if (this.isPreparingData)
			return;

		Task.Run(() => this.PrepareCharacterData(characterData));
	}

	private async Task PrepareCharacterData(CharacterData characterData)
	{
		if (this.isPreparingData)
			return;

		this.isPreparingData = true;

		while (this.isApplyingData)
			await Task.Delay(100);

		await this.PrepareSyncData(
			characterData.Character,
			this.LastData?.Character,
			this.objectIndex,
			this.characterProgress);

		await this.PrepareSyncData(
			characterData.MountOrMinion,
			this.LastData?.MountOrMinion,
			(ushort)(this.objectIndex + 1),
			this.mountProgress);

		await Plugin.Framework.RunOnUpdate();
		IGameObject? pet = this.GetPet();
		await Plugin.Framework.RunOutsideUpdate();
		if (pet != null)
		{
			await this.PrepareSyncData(
				characterData.Pet,
				this.LastData?.Pet,
				pet.ObjectIndex,
				this.petProgress);
		}

		this.isPreparingData = false;
	}

	private async Task PrepareSyncData(
		Dictionary<string, string?> sync,
		Dictionary<string, string?>? lastSync,
		ushort objectIndex,
		Dictionary<SyncProviderBase, SyncProgressBase> progresses)
	{
		if (this.lastState != States.Found)
			return;

		foreach ((string key, string? content) in sync)
		{
			try
			{
				if (string.IsNullOrEmpty(content))
					continue;

				SyncProviderBase? provider = Plugin.Sync.GetProvider(key);
				if (provider == null)
					continue;

				string? lastContent = null;
				lastSync?.TryGetValue(key, out lastContent);

				SyncProgressBase? progress = null;
				progresses.TryGetValue(provider, out progress);

				if (progress == null)
				{
					progress = provider.CreateProgress(this);
					progresses.Add(provider, progress);
				}

				progress.Status = SyncProgressStatus.Syncing;
				await provider.Prepare(lastContent, content, this, objectIndex, progress);

				if (progress.Status == SyncProgressStatus.Syncing)
					progress.Status = SyncProgressStatus.None;

				if (this.lastState != States.Found)
				{
					return;
				}
			}
			catch (TaskCanceledException)
			{
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, $"Error preparing sync data: {key}");
			}
		}
	}

	private void ApplySyncData()
	{
		if (this.isPreparingData)
			return;

		if (this.characterData == null)
			return;

		this.isApplyingData = true;

		this.ApplySyncData(
			this.characterData.Character,
			this.LastData?.Character,
			this.objectIndex,
			this.characterProgress);

		this.ApplySyncData(
			this.characterData.MountOrMinion,
			this.LastData?.MountOrMinion,
			(ushort)(this.objectIndex + 1),
			this.mountProgress);

		IGameObject? pet = this.GetPet();
		if (pet != null)
		{
			this.ApplySyncData(
				this.characterData.Pet,
				this.LastData?.Pet,
				pet.ObjectIndex,
				this.petProgress);
		}

		this.LastData = this.characterData;
		this.isApplyingData = false;
	}

	private void ApplySyncData(
		Dictionary<string, string?> sync,
		Dictionary<string, string?>? lastSync,
		ushort objectIndex,
		Dictionary<SyncProviderBase, SyncProgressBase> progresses)
	{
		if (this.lastState != States.Found)
			return;

		foreach ((string key, string? content) in sync)
		{
			try
			{
				SyncProviderBase? provider = Plugin.Sync.GetProvider(key);
				if (provider == null)
					continue;

				string? lastContent = null;
				lastSync?.TryGetValue(key, out lastContent);

				SyncProgressBase? progress = null;
				progresses.TryGetValue(provider, out progress);

				if (progress == null)
				{
					progress = provider.CreateProgress(this);
					progresses.Add(provider, progress);
				}

				if (content == lastContent)
				{
					if (content != null)
						progress.Status = SyncProgressStatus.Applied;

					continue;
				}

				progress.Status = provider.Apply(lastContent, content, this, objectIndex);

				if (this.lastState != States.Found)
				{
					return;
				}
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

	private IGameObject? GetPet()
	{
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

		return pet;
	}
}