// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Glamourer;

using System.Threading.Tasks;

public class GlamourerSync : SyncProviderBase
{
	private readonly GlamourerCommunicator glamourer = new();

	public override string DisplayName => "Glamourer";
	public override string Key => "g";

	public override async Task<string?> Serialize(ushort objectIndex)
	{
		if (!this.glamourer.GetIsAvailable())
			return null;

		return await this.glamourer.GetState(objectIndex);
	}

	public override async Task Deserialize(
		string? lastContent,
		string? content,
		CharacterSync character,
		ushort objectIndex)
	{
		if (!this.glamourer.GetIsAvailable())
		{
			if (!string.IsNullOrEmpty(content))
				this.SetStatus(character, SyncProgressStatus.NotApplied);

			return;
		}

		if (lastContent == content)
			return;

		if (content == null)
		{
			await this.glamourer.RevertState(objectIndex);
			this.SetStatus(character, SyncProgressStatus.Empty);
		}
		else
		{
			await this.glamourer.SetState(objectIndex, content);
			this.SetStatus(character, SyncProgressStatus.Applied);
		}
	}
}