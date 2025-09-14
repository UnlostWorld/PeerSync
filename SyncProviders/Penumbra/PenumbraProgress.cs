// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using PeerSync.UI;

public class PenumbraProgress(PenumbraSync provider, CharacterSync character)
	: SyncProgressBase(provider, character)
{
	public override void DrawInfo()
	{
		provider.DownloadGroup.GetCharacterProgress(
			this.Character,
			out long downloadCurrent,
			out long downloadTotal);

		provider.UploadGroup.GetCharacterProgress(
			this.Character,
			out long uploadCurrent,
			out long uploadTotal);

		if (uploadTotal > 0 && downloadTotal > 0)
		{
			float p = (float)uploadCurrent / (float)uploadTotal;
			ImGui.Text("↑");
			ImGui.SameLine();
			ImGuiEx.ThinProgressBar(p, 32);
			ImGui.SameLine();

			p = (float)downloadCurrent / (float)downloadTotal;
			ImGui.Text("↓");
			ImGui.SameLine();
			ImGuiEx.ThinProgressBar(p, 32);
		}
		else if (uploadTotal > 0)
		{
			float p = (float)uploadCurrent / (float)uploadTotal;
			ImGui.Text("↑");
			ImGui.SameLine();
			ImGuiEx.ThinProgressBar(p, -1);
		}
		else if (downloadTotal > 0)
		{
			float p = (float)downloadCurrent / (float)downloadTotal;
			ImGui.Text("↓");
			ImGui.SameLine();
			ImGuiEx.ThinProgressBar(p, -1);
		}

		ImGui.SameLine();
	}
}
