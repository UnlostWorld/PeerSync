// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using PeerSync;
using PeerSync.Online;

public class CharacterConnection : IDisposable
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan ReIndex = TimeSpan.FromSeconds(10);

	private readonly int objectIndex;
	private readonly CancellationTokenSource cancellationTokenSource = new();
	private DateTime lastSeen;
	private DateTime lastIndexAttempt;

	private string? address;
	private string? localAddress;
	private ushort port;

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
				this.address = response.Address;
				this.localAddress = response.LocalAddress;
				this.port = response.Port;
				this.Connect();
				return true;
			}
		}

		return false;
	}

	private void Connect()
	{
		this.CurrentStatus = Status.Connecting;
		Plugin.Log.Information($"Connect to {this.CharacterId} at {this.address}:{this.port}");
	}
}