// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using Dalamud.Bindings.ImGui;
using PeerSync.UI;

public class PenumbraProgress(PenumbraSync provider)
	: SyncProgressBase(provider)
{
	private long totalUpload = 0;
	private long totalDownload = 0;
	private long currentUpload = 0;
	private long currentDownload = 0;

	public void AddTotalUpload(long value)
	{
		lock (this)
		{
			this.totalUpload += value;
			this.Total += value;
		}
	}

	public void AddCurrentUpload(long value)
	{
		lock (this)
		{
			this.currentUpload += value;
			this.Current += value;
		}
	}

	public void AddTotalDownload(long value)
	{
		lock (this)
		{
			this.totalDownload += value;
			this.Total += value;
		}
	}

	public void AddCurrentDownload(long value)
	{
		lock (this)
		{
			this.currentDownload += value;
			this.Current += value;
		}
	}

	public override void DrawInfo()
	{
		bool hasDownload = this.totalDownload > 0 && this.currentDownload < this.totalDownload;
		bool hasUpload = this.totalUpload > 0 && this.currentUpload < this.totalUpload;

		if (hasUpload && hasDownload)
		{
			float p = (float)this.currentUpload / (float)this.totalUpload;
			ImGui.Text("↑");
			ImGui.SameLine();
			ImGuiEx.ThinProgressBar(p, 32);
			ImGui.SameLine();

			p = (float)this.currentDownload / (float)this.totalDownload;
			ImGui.Text("↓");
			ImGui.SameLine();
			ImGuiEx.ThinProgressBar(p, 32);
		}
		else if (hasUpload)
		{
			float p = (float)this.currentUpload / (float)this.totalUpload;
			ImGui.Text("↑");
			ImGui.SameLine();
			ImGuiEx.ThinProgressBar(p, -1);
		}
		else if (hasDownload)
		{
			float p = (float)this.currentDownload / (float)this.totalDownload;
			ImGui.Text("↓");
			ImGui.SameLine();
			ImGuiEx.ThinProgressBar(p, -1);
		}

		ImGui.SameLine();
	}
}
