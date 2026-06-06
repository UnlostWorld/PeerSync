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
using PeerSync.Connections;
using PeerSync.UI;

public class HonorificSync : SyncProviderBase
{
	private readonly HonorificCommunicator honorific = new();

	public override string DisplayName => "Honorific";
	public override string Key => "h";

	public override SyncProgressStatus Apply(
		string? lastContent,
		string? content,
		CharacterConnection character,
		ushort objectIndex)
	{
		if (!this.honorific.GetIsAvailable())
			return SyncProgressStatus.NotApplied;

		if (content == null)
		{
			this.honorific.ClearCharacterTitle(objectIndex);
			return SyncProgressStatus.Empty;
		}
		else
		{
			this.honorific.SetCharacterTitle(objectIndex, content);
			return SyncProgressStatus.Applied;
		}
	}

	public override async Task<string?> Serialize(Configuration.Character character, ushort objectIndex)
	{
		if (!this.honorific.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();
		return this.honorific.GetCharacterTitle(objectIndex);
	}

	public override void Reset(CharacterConnection character, ushort? objectIndex)
	{
		if (!this.honorific.GetIsAvailable())
			return;

		if (objectIndex != null)
		{
			this.honorific.ClearCharacterTitle(objectIndex.Value);
		}
	}

	public override void DrawInspect(CharacterConnection? character, string content)
	{
		if (ImGui.CollapsingHeader(this.DisplayName))
		{
			ImGuiEx.JsonViewer("honorificInspector", content);
		}
	}
}