// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Moodles;

using System;
using System.Buffers.Text;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;

public class SimpleHeelsSync : SyncProviderBase
{
	private readonly SimpleHeelsCommunicator simpleHeels = new();

	public override string DisplayName => "Simple Heels";
	public override string Key => "s";

	public override async Task<string?> Serialize(Configuration.Character character, ushort objectIndex)
	{
		if (!this.simpleHeels.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		if (objectIndex != Plugin.ObjectTable.LocalPlayer?.ObjectIndex)
			return null;

		return this.simpleHeels.GetPlayerData();
	}

	public override async Task Deserialize(
		string? lastContent,
		string? content,
		CharacterSync character,
		ushort objectIndex)
	{
		if (!this.simpleHeels.GetIsAvailable())
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
			this.simpleHeels.ClearPlayerData(objectIndex);
			this.SetStatus(character, SyncProgressStatus.Empty);
		}
		else
		{
			this.simpleHeels.SetPlayerData(objectIndex, content);
			this.SetStatus(character, SyncProgressStatus.Applied);
		}
	}

	public override async Task Reset(CharacterSync character, ushort? objectIndex)
	{
		await base.Reset(character, objectIndex);

		if (objectIndex != null)
		{
			await Plugin.Framework.RunOnUpdate();
			this.simpleHeels.ClearPlayerData(objectIndex.Value);
		}

		this.SetStatus(character, SyncProgressStatus.Empty);
	}
}