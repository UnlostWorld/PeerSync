// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Newtonsoft.Json;
using PeerSync.Connections;
using PeerSync.SyncBlockers;
using PeerSync.SyncProviders.CustomizePlus;
using PeerSync.SyncProviders.Glamourer;
using PeerSync.SyncProviders.Honorific;
using PeerSync.SyncProviders.Moodles;
using PeerSync.SyncProviders.Penumbra;
using PeerSync.SyncProviders.PetNames;
using PeerSync.SyncProviders.SimpleHeels;

using CharacterData = PeerSync.CharacterData;

public class SyncService : IDisposable
{
	public readonly LightlessCommunicator Lightless = new();

	public readonly CharacterData LocalCharacterData = new();
	public readonly List<SyncProviderBase> Providers = new();

	private static readonly TimeSpan CheckTime = TimeSpan.FromSeconds(1);
	private static readonly TimeSpan ForceSendTime = TimeSpan.FromSeconds(30);

	private readonly Dictionary<string, SyncProviderBase> providerLookup = new();
	private bool isSending = false;
	private DateTime lastSend;
	private DateTime lastCheck;

	public SyncService()
	{
		this.lastSend = DateTime.MinValue;

		lock (this.Providers)
		{
			this.Providers.Clear();
			this.Providers.Add(new CustomizePlusSync());
			this.Providers.Add(new MoodlesSync());
			this.Providers.Add(new HonorificSync());
			this.Providers.Add(new GlamourerSync());
			this.Providers.Add(new PenumbraSync());
			this.Providers.Add(new PetNamesSync());
			this.Providers.Add(new SimpleHeelsSync());
		}

		foreach (SyncProviderBase provider in this.Providers)
		{
			this.providerLookup.Add(provider.Key, provider);
		}
	}

	public TimeSpan TimeSinceLastSend => DateTime.Now - this.lastSend;
	public TimeSpan TimeSinceLastCheck => DateTime.Now - this.lastCheck;

	public SyncProviderBase? GetProvider(string key)
	{
		if (this.providerLookup.TryGetValue(key, out var provider))
			return provider;

		return null;
	}

	public void Dispose()
	{
		lock (this.Providers)
		{
			foreach (SyncProviderBase sync in this.Providers)
			{
				sync.Dispose();
			}

			this.Providers.Clear();
		}

		this.providerLookup.Clear();
	}

	public void FrameworkUpdate()
	{
		if (this.isSending)
			return;

		if (Plugin.Characters.Current == null)
			return;

		// Do not sync character if we are in combat is loading
		if (Plugin.Condition[ConditionFlag.InCombat]
			|| Plugin.Condition[ConditionFlag.BetweenAreas]
			|| Plugin.Condition[ConditionFlag.BetweenAreas51]
			|| Plugin.Condition[ConditionFlag.LoggingOut])
		{
			return;
		}

		if (this.TimeSinceLastCheck < CheckTime)
			return;

		IPlayerCharacter? player = Plugin.ObjectTable.LocalPlayer;
		if (player == null)
			return;

		IGameObject? mountOrMinion = Plugin.ObjectTable[player.ObjectIndex + 1];

		IGameObject? pet = null;
		unsafe
		{
			BattleChara* pPet = CharacterManager.Instance()->LookupPetByOwnerObject((BattleChara*)player.Address);
			if (pPet != null)
			{
				pet = Plugin.ObjectTable[pPet->ObjectIndex];
			}
		}

		Task.Run(async () => await this.SerializeAndSend(player.ObjectIndex, mountOrMinion?.ObjectIndex, pet?.ObjectIndex));
	}

	private async Task SerializeAndSend(ushort playerIndex, ushort? mountIndex, ushort? petIndex)
	{
		if (Plugin.Characters.Current == null)
			return;

		if (this.isSending)
			return;

		this.isSending = true;
		this.lastCheck = DateTime.Now;

		try
		{
			CharacterData data = new();
			data.CharacterId = Plugin.Characters.GetCurrentCharacterId();

			foreach (SyncProviderBase sync in Plugin.Sync.Providers.ToArray())
			{
				try
				{
					string? content = await sync.Serialize(Plugin.Characters.Current, playerIndex);
					data.Character.Add(sync.Key, content);

					if (mountIndex != null)
					{
						content = await sync.Serialize(Plugin.Characters.Current, (ushort)mountIndex);
						data.MountOrMinion.Add(sync.Key, content);
					}

					if (petIndex != null)
					{
						content = await sync.Serialize(Plugin.Characters.Current, (ushort)petIndex);
						data.Pet.Add(sync.Key, content);
					}
				}
				catch (Exception ex)
				{
					Plugin.Log.Error(ex, "Error collecting character data");
				}
			}

			if (this.LocalCharacterData.IsSame(data) && this.TimeSinceLastSend < ForceSendTime)
				return;

			this.lastSend = DateTime.Now;
			data.CopyTo(this.LocalCharacterData);

			string json = JsonConvert.SerializeObject(data);
			byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
			Plugin.Connections.Send(PacketTypes.CharacterData, jsonBytes);
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error sending character data");
		}
		finally
		{
			this.isSending = false;
		}
	}
}