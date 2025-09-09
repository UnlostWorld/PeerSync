// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using Dalamud.Bindings.ImGui;
using PeerSync.UI;

namespace PeerSync.SyncProviders.Penumbra;

public class PenumbraProgress(PenumbraSync provider)
	: SyncProgressBase(provider)
{
	public long totalUpload = 0;
	public long totalDownload = 0;
	public long currentUpload = 0;
	public long currentDownload = 0;

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
