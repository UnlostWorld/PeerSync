// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Overlays;

using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using PeerSync.Connections;
using PeerSync.SyncProviders;
using PeerSync.UI;

public class TransferOverlay : OverlayBase
{
	private readonly CharacterConnection connection;

	public TransferOverlay(CharacterConnection connection)
	{
		this.connection = connection;
	}

	public override Vector3 GetWorldPosition()
	{
		IPlayerCharacter? character = this.connection.GetCharacter();
		if (character == null)
			return Vector3.Zero;

		return character.Position;
	}

	public override void Draw()
	{
		float totalProgress = this.connection.GetTotalProgress();
		if (totalProgress < 1)
		{
			float width = 75;
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() - (width / 2));
			ImGuiEx.ThinProgressBar(totalProgress, width);
		}
	}
}