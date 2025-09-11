// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeerSync.SyncProviders.CustomizePlus;

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
			if (this.appliedProfiles.TryGetValue(character.Pair.GetFingerprint(), out Guid guid))
			{
				this.customizePlus.DeleteTemporaryProfileByUniqueId(guid);
				this.appliedProfiles.Remove(character.Pair.GetFingerprint());
			}

			this.SetStatus(character, SyncProgressStatus.Empty);
		}
		else
		{
			Guid? guid = this.customizePlus.SetTemporaryProfileOnCharacter(objectIndex, content);
			if (guid == null)
				return;

			this.appliedProfiles[character.Pair.GetFingerprint()] = guid.Value;
			this.SetStatus(character, SyncProgressStatus.Applied);
		}
	}

	public override async Task<string?> Serialize(ushort objectIndex)
	{
		if (!this.customizePlus.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		Guid? guid = this.customizePlus.GetActiveProfileIdOnCharacter(objectIndex);
		if (guid == null)
			return null;

		return this.customizePlus.GetProfileByUniqueId(guid.Value);
	}
}