// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

namespace PeerSync.SyncProviders.Moodles;

public class MoodlesSync : SyncProviderBase
{
	public override string DisplayName => "Moodles";
	public override string Key => "m";

	private readonly MoodlesCommunicator moodles = new();

	public override async Task Deserialize(string? lastContent, string? content, CharacterSync character)
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

		IGameObject? gameObject = Plugin.ObjectTable[character.ObjectTableIndex];
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
}