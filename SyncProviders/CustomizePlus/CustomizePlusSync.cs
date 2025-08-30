// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeerSync.SyncProviders.CustomizePlus;

public class CustomizePlusSync : SyncProviderBase
{
	private readonly CustomizePlusCommunicator customizePlus = new();
	private readonly Dictionary<string, Guid> appliedProfiles = new();

	public override string Key => "Customize+";

	public override async Task Deserialize(string? content, CharacterSync character)
	{
		if (!this.customizePlus.GetIsAvailable())
			return;

		await Plugin.Framework.RunOnUpdate();

		if (content == null)
		{
			if (this.appliedProfiles.TryGetValue(character.Identifier, out Guid guid))
			{
				this.customizePlus.DeleteTemporaryProfileByUniqueId(guid);
				this.appliedProfiles.Remove(character.Identifier);
			}
		}
		else
		{
			Guid? guid = this.customizePlus.SetTemporaryProfileOnCharacter(character.ObjectTableIndex, content);
			if (guid == null)
				return;

			this.appliedProfiles[character.Identifier] = guid.Value;
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