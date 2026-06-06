// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.PetNames;

using System;
using System.Buffers.Text;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using PeerSync.Connections;

public class PetNamesSync : SyncProviderBase
{
	private readonly PetNamesCommunicator petNames = new();

	public override string DisplayName => "Pet Nicknames";
	public override string Key => "n";

	public override async Task<string?> Serialize(Configuration.Character character, ushort objectIndex)
	{
		if (!this.petNames.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		if (objectIndex != Plugin.ObjectTable.LocalPlayer?.ObjectIndex)
			return null;

		return this.petNames.GetPlayerData();
	}

	public override SyncProgressStatus Apply(
		string? lastContent,
		string? content,
		CharacterConnection character,
		ushort objectIndex)
	{
		if (!this.petNames.GetIsAvailable())
			return SyncProgressStatus.NotApplied;

		IGameObject? gameObject = Plugin.ObjectTable[objectIndex];
		if (gameObject is not IPlayerCharacter playerCharacter)
			return SyncProgressStatus.Error;

		if (content == null)
		{
			this.petNames.ClearPlayerData(objectIndex);
			return SyncProgressStatus.Empty;
		}
		else
		{
			this.petNames.SetPlayerData(content);
			return SyncProgressStatus.Applied;
		}
	}

	public override void Reset(CharacterConnection character, ushort? objectIndex)
	{
		if (objectIndex != null)
		{
			this.petNames.ClearPlayerData(objectIndex.Value);
		}
	}

	public override void DrawInspect(CharacterConnection? character, string content)
	{
		if (ImGui.CollapsingHeader(this.DisplayName))
		{
			ImGui.PushFont(UiBuilder.MonoFont);

			byte[] data = Convert.FromBase64String(content);
			content = Encoding.Unicode.GetString(data);

			ImGui.TextWrapped(content);
			ImGui.PopFont();
		}
	}
}