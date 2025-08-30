// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

namespace PeerSync.SyncProviders.Moodles;

public class MoodlesSync : SyncProviderBase
{
	public override string Key => "Moodles";

	private readonly MoodlesCommunicator moodles = new();

	public override async Task Deserialize(string? content, CharacterSync character)
	{
		await Plugin.Framework.RunOnUpdate();

		IGameObject? gameObject = Plugin.ObjectTable[character.ObjectTableIndex];
		if (gameObject is not IPlayerCharacter playerCharacter)
			return;

		if (content == null)
		{
			this.moodles.ClearStatusManager(playerCharacter);
		}
		else
		{
			this.moodles.SetStatusManagerByPC(playerCharacter, content);
		}
	}

	public override async Task<string?> Serialize(ushort objectIndex)
	{
		await Plugin.Framework.RunOnUpdate();

		IGameObject? gameObject = Plugin.ObjectTable[objectIndex];
		if (gameObject is not IPlayerCharacter playerCharacter)
			return null;

		return this.moodles.GetStatusManagerByPC(playerCharacter);
	}
}