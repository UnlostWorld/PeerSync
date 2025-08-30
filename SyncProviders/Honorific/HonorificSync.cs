// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Threading.Tasks;

namespace PeerSync.SyncProviders.Honorific;

public class HonorificSync : SyncProviderBase
{
	public override string Key => "Honorific";

	private readonly HonorificCommunicator honorific = new();

	public override async Task Deserialize(string? lastContent, string? content, CharacterSync character)
	{
		if (!this.honorific.GetIsAvailable())
			return;

		if (lastContent == content)
			return;

		await Plugin.Framework.RunOnUpdate();

		if (content == null)
		{
			this.honorific.ClearCharacterTitle(character.ObjectTableIndex);
		}
		else
		{
			this.honorific.SetCharacterTitle(character.ObjectTableIndex, content);
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