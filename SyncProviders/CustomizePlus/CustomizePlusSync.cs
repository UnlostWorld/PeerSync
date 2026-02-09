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
using PeerSync.UI;

public class CustomizePlusSync : SyncProviderBase
{
	private readonly CustomizePlusCommunicator customizePlus = new();
	private readonly Dictionary<string, Guid> appliedProfiles = new();

	public override string DisplayName => "Customize+";
	public override string Key => "c";

	public override async Task Deserialize(
		string? lastContent,
		string? content,
		CharacterSync character,
		ushort objectIndex)
	{
		if (!this.customizePlus.GetIsAvailable())
		{
			if (!string.IsNullOrEmpty(content))
				this.SetStatus(character, SyncProgressStatus.NotApplied);

			return;
		}

		if (lastContent == content)
			return;

		await Plugin.Framework.RunOnUpdate();

		if (content == null)
		{
			if (this.appliedProfiles.TryGetValue(character.Peer.GetFingerprint(), out Guid guid))
			{
				this.customizePlus.DeleteTemporaryProfileByUniqueId(guid);
				this.appliedProfiles.Remove(character.Peer.GetFingerprint());
			}

			this.SetStatus(character, SyncProgressStatus.Empty);
		}
		else
		{
			Guid? guid = this.customizePlus.SetTemporaryProfileOnCharacter(objectIndex, content);
			if (guid == null)
				return;

			this.appliedProfiles[character.Peer.GetFingerprint()] = guid.Value;
			this.SetStatus(character, SyncProgressStatus.Applied);
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

	public override async Task Reset(CharacterSync character, ushort? objectIndex)
	{
		await base.Reset(character, objectIndex);
		await Plugin.Framework.RunOnUpdate();

		if (this.appliedProfiles.TryGetValue(character.Peer.GetFingerprint(), out Guid guid))
		{
			this.customizePlus.DeleteTemporaryProfileByUniqueId(guid);
			this.appliedProfiles.Remove(character.Peer.GetFingerprint());
		}

		this.SetStatus(character, SyncProgressStatus.Empty);
	}

	public override void DrawInspect(CharacterSync? character, string content)
	{
		if (ImGui.CollapsingHeader(this.DisplayName))
		{
			ImGuiEx.JsonViewer("c+Inspector", content);
		}
	}
}