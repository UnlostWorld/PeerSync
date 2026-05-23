// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using PeerSync;
using PeerSync.Network;
using PeerSync.Online;

public class CharacterConnection : IDisposable
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan ReIndex = TimeSpan.FromSeconds(10);

	private readonly int objectIndex;
	private readonly CancellationTokenSource cancellationTokenSource = new();
	private DateTime lastSeen;
	private DateTime lastIndexAttempt;

	private Connection? outgoingConnection;
	private Connection? incomingConnection;

	public CharacterConnection(IPlayerCharacter character)
	{
		this.CharacterId = character.GetId();
		this.CharacterName = character.GetName();
		this.CharacterWorld = character.GetHomeWorld();
		this.objectIndex = character.ObjectIndex;

		this.lastSeen = DateTime.Now;

		Task.Run(this.FingerprintIndexConnect);
	}

	public enum States
	{
		Found,
		NotFound,
		TimedOut,
	}

	public enum Status
	{
		Initializing,
		Indexing,
		IndexingFailed,
		Connecting,
		Connected,
	}

	public string CharacterId { get; init; }
	public string CharacterName { get; init; }
	public string CharacterWorld { get; init; }
	public Status CurrentStatus { get; private set; }

	public TimeSpan TimeSinceLastSeen => DateTime.Now - this.lastSeen;
	public TimeSpan TimeSinceLastIndexAttempt => DateTime.Now - this.lastIndexAttempt;

	public States Update()
	{
		IGameObject? obj = Plugin.ObjectTable[this.objectIndex];
		if (obj is IPlayerCharacter character
			&& character.GetName() == this.CharacterName
			&& character.GetHomeWorld() == this.CharacterWorld)
		{
			this.lastSeen = DateTime.Now;

			if (this.CurrentStatus == Status.IndexingFailed && this.TimeSinceLastIndexAttempt > ReIndex)
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
		this.cancellationTokenSource.Cancel();
	}

	public void DrawStatus()
	{
		ImGui.Text($"{this.CharacterName} @ {this.CharacterWorld} - {this.CurrentStatus}");
	}

	public void SetOutgoingNetworkConnection(Connection connection)
	{
		Plugin.Log.Information($"Connected to {this.CharacterId} (outgoing)");
		this.outgoingConnection = connection;
		this.CurrentStatus = Status.Connected;
		this.SendIAm();
	}

	public void SetIncomingNetworkConnection(Connection connection)
	{
		Plugin.Log.Information($"Connected to {this.CharacterId} (incoming)");
		this.incomingConnection = connection;
		this.CurrentStatus = Status.Connected;
		this.SendIAm();
	}

	public void SendIAm()
	{
		// TODO: move this into a local character service or something.
		string localCharacterId = $"{Plugin.Instance?.LocalCharacter?.CharacterName}@{Plugin.Instance?.LocalCharacter?.World}";
		byte[] data = Encoding.UTF8.GetBytes(localCharacterId);
		this.Send(PacketTypes.IAm, data);
	}

	public void Send(PacketTypes type, byte[] data)
	{
		if (this.outgoingConnection?.IsConnected == true)
		{
			this.outgoingConnection.Send(type, data);
		}

		if (this.incomingConnection?.IsConnected == true)
		{
			this.incomingConnection.Send(type, data);
		}
	}

	private async Task<bool> FingerprintIndexConnect()
	{
		this.CurrentStatus = Status.Indexing;
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
		if (Plugin.Instance != null)
		{
			foreach (GroupSync group in Plugin.Instance.GroupSyncs.Values)
			{
				string memberFingerprint = group.Group.GetMemberFingerprint(this.CharacterName, this.CharacterWorld);

				if (group.MemberFingerprints.Contains(memberFingerprint))
				{
					request.GroupFingerprint = group.Group.GetFingerprint();
					request.MemberFingerprint = memberFingerprint;
					if (await this.IndexConnect(request))
					{
						return true;
					}
				}
			}
		}

		// We didn't find a valid peer.
		this.CurrentStatus = Status.IndexingFailed;
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

		this.CurrentStatus = Status.Connecting;
		Connection? outgoingConnection = await Plugin.Instance.Connections.Connect(address, localAddress, port);

		if (outgoingConnection != null)
		{
			this.SetOutgoingNetworkConnection(outgoingConnection);
			return true;
		}

		return false;
	}
}