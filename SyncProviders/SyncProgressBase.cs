// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders;

using PeerSync.UI;

public class SyncProgressBase(SyncProviderBase provider)
{
	public SyncProviderBase Provider = provider;

	public SyncProgressStatus Status { get; set; }
	public long Current { get; set; }
	public long Total { get; set; }

	public virtual void DrawInfo()
	{
		if (this.Current < this.Total && this.Total > 0)
		{
			float p = (float)this.Current / (float)this.Total;
			ImGuiEx.ThinProgressBar(p, -1);
		}
	}

	public virtual void DrawStatus()
	{
		ImGuiEx.Icon(this.Status.GetIcon());
	}
}