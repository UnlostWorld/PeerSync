// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Threading.Tasks;

namespace PeerSync.SyncProviders.Honorific;

public class HonorificSync : SyncProviderBase
{
	public override string DisplayName => "Honorific";
	public override string Key => "h";

	private readonly HonorificCommunicator honorific = new();

	public override async Task Deserialize(string? lastContent, string? content, CharacterSync character)
	{
		if (!this.honorific.GetIsAvailable())
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
			this.honorific.ClearCharacterTitle(character.ObjectTableIndex);
			this.SetStatus(character, SyncProgressStatus.Empty);
		}
		else
		{
			this.honorific.SetCharacterTitle(character.ObjectTableIndex, content);
			this.SetStatus(character, SyncProgressStatus.Applied);
		}
	}

	public override async Task<string?> Serialize(ushort objectIndex)
	{
		if (!this.honorific.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();
		return this.honorific.GetCharacterTitle(objectIndex);
	}
}