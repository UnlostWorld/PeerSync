// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Threading.Tasks;

namespace PeerSync.SyncProviders.Glamourer;

public class GlamourerSync : SyncProviderBase
{
	private readonly GlamourerCommunicator glamourer = new();

	public override string DisplayName => "Glamourer";
	public override string Key => "gl";

	public override async Task<string?> Serialize(ushort objectIndex)
	{
		if (!glamourer.GetIsAvailable())
			return null;

		return await this.glamourer.GetState(objectIndex);
	}

	public override async Task Deserialize(string? lastContent, string? content, CharacterSync character)
	{
		if (!glamourer.GetIsAvailable())
		{
			if (!string.IsNullOrEmpty(content))
				this.SetStatus(character, SyncProgressStatus.NotApplied);

			return;
		}

		if (lastContent == content)
			return;

		if (content == null)
		{
			await glamourer.RevertState(character.ObjectTableIndex);
			this.SetStatus(character, SyncProgressStatus.Empty);
		}
		else
		{
			if (!character.Pair.IsTestPair)
				await glamourer.SetState(character.ObjectTableIndex, content);

			this.SetStatus(character, SyncProgressStatus.Applied);
		}
	}
}