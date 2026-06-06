// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.CustomizePlus;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using PeerSync.Connections;
using PeerSync.UI;

public class CustomizePlusSync : SyncProviderBase
{
	private readonly CustomizePlusCommunicator customizePlus = new();
	private readonly Dictionary<string, Guid> appliedProfiles = new();

	public override string DisplayName => "Customize+";
	public override string Key => "c";

	public override SyncProgressStatus Apply(
		string? lastContent,
		string? content,
		CharacterConnection character,
		ushort objectIndex)
	{
		if (!this.customizePlus.GetIsAvailable())
		{
			return SyncProgressStatus.NotApplied;
		}

		if (content == null)
		{
			if (this.appliedProfiles.TryGetValue(character.CharacterId, out Guid guid))
			{
				this.customizePlus.DeleteTemporaryProfileByUniqueId(guid);
				this.appliedProfiles.Remove(character.CharacterId);
			}

			return SyncProgressStatus.Empty;
		}
		else
		{
			Guid? guid = this.customizePlus.SetTemporaryProfileOnCharacter(objectIndex, content);
			if (guid == null)
				return SyncProgressStatus.Error;

			this.appliedProfiles[character.CharacterId] = guid.Value;
			return SyncProgressStatus.Applied;
		}
	}

	public override async Task<string?> Serialize(Configuration.Character character, ushort objectIndex)
	{
		if (!this.customizePlus.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		Guid? guid = this.customizePlus.GetActiveProfileIdOnCharacter(objectIndex);
		if (guid == null)
			return null;

		return this.customizePlus.GetProfileByUniqueId(guid.Value);
	}

	public override void Reset(CharacterConnection character, ushort? objectIndex)
	{
		if (this.appliedProfiles.TryGetValue(character.CharacterId, out Guid guid))
		{
			try
			{
				this.customizePlus.DeleteTemporaryProfileByUniqueId(guid);
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Failed to reset peer");
			}

			this.appliedProfiles.Remove(character.CharacterId);
		}
	}

	public override void DrawInspect(CharacterConnection? character, string content)
	{
		if (ImGui.CollapsingHeader(this.DisplayName))
		{
			ImGuiEx.JsonViewer("c+Inspector", content);
		}
	}
}