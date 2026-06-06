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
using PeerSync.Connections;
using PeerSync.UI;

public class PenumbraSyncContext(PenumbraSync provider, CharacterConnection character)
	: SyncContext(provider, character)
{
	public override void DrawInfo()
	{
		if (this.Status == SyncProgressStatus.Syncing)
		{
			provider.DownloadGroup.GetProgress(
				this,
				out long downloadCurrent,
				out long downloadTotal);

			if (downloadTotal > 0)
			{
				float p = (float)downloadCurrent / (float)downloadTotal;
				ImGuiEx.ThinProgressBar(p, -1);
			}
		}

		ImGui.SameLine();
	}

	public override void Combine(ref long current, ref long total)
	{
		provider.DownloadGroup.GetProgress(
			this.Connection,
			out long downloadCurrent,
			out long downloadTotal);

		current += downloadCurrent;
		total += downloadTotal;
	}
}
