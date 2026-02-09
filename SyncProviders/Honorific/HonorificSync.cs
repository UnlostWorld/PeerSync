// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Honorific;

using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using PeerSync.UI;

public class HonorificSync : SyncProviderBase
{
	private readonly HonorificCommunicator honorific = new();

	public override string DisplayName => "Honorific";
	public override string Key => "h";

	public override async Task Deserialize(
		string? lastContent,
		string? content,
		CharacterSync character,
		ushort objectIndex)
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
			this.honorific.ClearCharacterTitle(objectIndex);
			this.SetStatus(character, SyncProgressStatus.Empty);
		}
		else
		{
			this.honorific.SetCharacterTitle(objectIndex, content);
			this.SetStatus(character, SyncProgressStatus.Applied);
		}
	}

	public override async Task<string?> Serialize(Configuration.Character character, ushort objectIndex)
	{
		if (!this.honorific.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();
		return this.honorific.GetCharacterTitle(objectIndex);
	}

	public override async Task Reset(CharacterSync character, ushort? objectIndex)
	{
		await base.Reset(character, objectIndex);

		await Plugin.Framework.RunOnUpdate();

		if (objectIndex != null)
			this.honorific.ClearCharacterTitle(objectIndex.Value);

		this.SetStatus(character, SyncProgressStatus.Empty);
	}

	public override void DrawInspect(CharacterSync? character, string content)
	{
		if (ImGui.CollapsingHeader(this.DisplayName))
		{
			ImGuiEx.JsonViewer("honorificInspector", content);
		}
	}
}