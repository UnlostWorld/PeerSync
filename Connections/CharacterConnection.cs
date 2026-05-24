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
using PeerSync.SyncProviders;
using PeerSync.UI;

using CharacterData = PeerSync.CharacterData;

public partial class CharacterConnection : IDisposable
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan ReIndex = TimeSpan.FromSeconds(10);

	private readonly ushort objectIndex;
	private readonly CancellationTokenSource cancellationTokenSource = new();
	private DateTime lastSeen;
	private DateTime lastIndexAttempt;
	private Connection? outgoingConnection;
	private Connection? incomingConnection;
	private bool isApplyingData = false;

	public CharacterConnection(IPlayerCharacter character)
	{
		this.CharacterId = character.GetId();
		this.CharacterName = character.GetName();
		this.CharacterWorld = character.GetHomeWorld();
		this.objectIndex = character.ObjectIndex;

		this.lastSeen = DateTime.Now;
		this.lastIndexAttempt = DateTime.MinValue;

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
	public CharacterConnectionStatus CurrentStatus { get; private set; }
	public CharacterData? LastData { get; private set; }

	public TimeSpan TimeSinceLastSeen => DateTime.Now - this.lastSeen;
	public TimeSpan TimeSinceLastIndexAttempt => DateTime.Now - this.lastIndexAttempt;

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

	public void OnCharacterData(CharacterData characterData)
	{
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
			if (this.CurrentStatus == CharacterConnectionStatus.Offline
				&& this.TimeSinceLastIndexAttempt > ReIndex
				&& Plugin.Index.HasInitialIndexingCompleted
				&& Plugin.Characters.Current != null)
			{
				Task.Run(this.FingerprintIndexConnect);
			}

			return States.Found;
		}

		if (this.TimeSinceLastSeen > Timeout)
		{
			return States.TimedOut;
		}

		return States.NotFound;
	}

	public void Dispose()
	{
		if (this.CurrentStatus == CharacterConnectionStatus.Connected)
		{
			this.OnDisconnected();
		}

		this.cancellationTokenSource.Cancel();
	}

	public void SetOutgoingNetworkConnection(Connection connection)
	{
		this.outgoingConnection = connection;
		this.outgoingConnection.Received += this.OnReceived;
		this.outgoingConnection.Disconnected += this.OnOutgoingDisconnected;
		this.CurrentStatus = CharacterConnectionStatus.HandShaking;
		this.SendIAm();
	}

	public void SetIncomingNetworkConnection(Connection connection)
	{
		this.incomingConnection = connection;
		this.incomingConnection.Received += this.OnReceived;
		this.incomingConnection.Disconnected += this.OnIncomingDisconnected;
		this.CurrentStatus = CharacterConnectionStatus.HandShaking;
		this.SendIAm();
	}

	public void SendIAm()
	{
		string? localCharacterId = Plugin.Characters.GetCurrentCharacterId();
		if (localCharacterId == null)
			throw new Exception("Attempt to send IAm without a current character");

		byte[] data = Encoding.UTF8.GetBytes(localCharacterId);
		this.Send(PacketTypes.IAm, data);
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

	private async Task<bool> FingerprintIndexConnect()
	{
		this.CurrentStatus = CharacterConnectionStatus.Indexing;
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
		this.CurrentStatus = CharacterConnectionStatus.Offline;
		return false;
	}

	private async Task<bool> IndexConnect(GetPeer request)
	{
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

		return false;
	}

	private async Task<bool> Connect(IPAddress address, IPAddress? localAddress, ushort port)
	{
		if (Plugin.Instance == null)
			return false;

		this.CurrentStatus = CharacterConnectionStatus.Connecting;
		Connection? outgoingConnection = await Plugin.Connections.Connect(address, localAddress, port);

		if (outgoingConnection != null)
		{
			if (Plugin.Characters.Current == null)
			{
				outgoingConnection.Dispose();
			}
			else
			{
				this.SetOutgoingNetworkConnection(outgoingConnection);
			}

			return true;
		}

		return false;
	}

	private void OnIncomingDisconnected(Connection connection)
	{
		connection.Received -= this.OnReceived;
		connection.Disconnected -= this.OnOutgoingDisconnected;
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
		this.CurrentStatus = CharacterConnectionStatus.Connected;

		if (Plugin.Instance == null)
			return;

		foreach (SyncProviderBase sync in Plugin.Instance.SyncProviders)
		{
			sync.OnCharacterConnected(this);
		}

		// send the most recent version of our character data to this new connection.
		if (Plugin.Instance.LocalCharacterData != null)
		{
			string json = JsonConvert.SerializeObject(Plugin.Instance.LocalCharacterData);
			byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
			this.Send(PacketTypes.CharacterData, jsonBytes);
		}
	}

	private void OnDisconnected()
	{
		Plugin.Log.Info($"Disconnected: {this.CharacterId}");
		this.CurrentStatus = CharacterConnectionStatus.Offline;

		if (Plugin.Instance == null)
			return;

		foreach (SyncProviderBase sync in Plugin.Instance.SyncProviders)
		{
			sync.OnCharacterDisconnected(this);
		}
	}

	private void OnReceived(Connection connection, PacketTypes type, byte[] data)
	{
		if (type == PacketTypes.IAm)
		{
			string characterId = Encoding.UTF8.GetString(data);
			if (characterId != this.CharacterId)
			{
				Plugin.Log.Warning($"Unrecognized IAm: {characterId} from {connection.EndPoint}, expected {this.CharacterId}");
			}
			else
			{
				this.OnConnected();
			}
		}
		else if (type == PacketTypes.CharacterData)
		{
			string json = Encoding.UTF8.GetString(data);
			CharacterData? characterData = JsonConvert.DeserializeObject<CharacterData>(json);
			if (characterData == null)
				throw new Exception();

			this.OnCharacterData(characterData);
		}
		else
		{
			this.Received?.Invoke(this, type, data);
		}
	}

	private async Task ApplyCharacterData(CharacterData characterData)
	{
		if (this.isApplyingData)
			return;

		if (await Plugin.Lightless.GetIsGameObjectHandled(this.objectIndex))
		{
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