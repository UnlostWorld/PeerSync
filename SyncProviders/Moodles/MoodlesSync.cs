// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Moodles;

using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

public class MoodlesSync : SyncProviderBase
{
	private readonly MoodlesCommunicator moodles = new();

	public override string DisplayName => "Moodles";
	public override string Key => "m";

	public override async Task Deserialize(
		string? lastContent,
		string? content,
		CharacterSync character,
		ushort objectIndex)
	{
		if (!this.moodles.GetIsAvailable())
		{
			if (!string.IsNullOrEmpty(content))
				this.SetStatus(character, SyncProgressStatus.NotApplied);

			return;
		}

		if (lastContent == content)
			return;

		await Plugin.Framework.RunOnUpdate();

		IGameObject? gameObject = Plugin.ObjectTable[objectIndex];
		if (gameObject is not IPlayerCharacter playerCharacter)
			return;

		if (content == null)
		{
			this.moodles.ClearStatusManager(playerCharacter);
			this.SetStatus(character, SyncProgressStatus.Empty);
		}
		else
		{
			this.moodles.SetStatusManagerByPC(playerCharacter, content);
			this.SetStatus(character, SyncProgressStatus.Applied);
		}
	}

	public override async Task<string?> Serialize(ushort objectIndex)
	{
		if (!this.moodles.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		IGameObject? gameObject = Plugin.ObjectTable[objectIndex];
		if (gameObject is not IPlayerCharacter playerCharacter)
			return null;

		return this.moodles.GetStatusManagerByPC(playerCharacter);
	}

	public override async Task Reset(CharacterSync character, ushort? objectIndex)
	{
		await base.Reset(character, objectIndex);

		if (objectIndex != null)
		{
			await Plugin.Framework.RunOnUpdate();

			IGameObject? gameObject = Plugin.ObjectTable[objectIndex.Value];
			if (gameObject is not IPlayerCharacter playerCharacter)
				return;

			this.moodles.ClearStatusManager(playerCharacter);
		}

		this.SetStatus(character, SyncProgressStatus.Empty);
	}
}