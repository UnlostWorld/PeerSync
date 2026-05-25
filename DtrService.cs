// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using PeerSync.SyncProviders;

public class DtrService : IDisposable
{
	public static readonly SeString GlyphSolid = SeStringUtils.ToSeString("\uE0BD");
	public static readonly SeString GlyphOutline = SeStringUtils.ToSeString("\uE0BC");

	private readonly IDtrBarEntry dtrBarEntry;

	public DtrService()
	{
		this.dtrBarEntry = Plugin.DtrBar.Get("Peer Sync");
		this.dtrBarEntry.Text = GlyphOutline;
		this.dtrBarEntry.Tooltip = SeStringUtils.ToSeString($"Peer Sync");
		this.dtrBarEntry.OnClick = this.OnDtrClicked;
	}

	public void Dispose()
	{
		this.dtrBarEntry.Text = GlyphOutline;
		Plugin.DtrBar.Remove("Peer Sync");
	}

	public void FrameworkUpdate()
	{
		SeStringBuilder dtrEntryBuilder = new();
		dtrEntryBuilder.AddText($"\uE0BD");

		SeStringBuilder dtrTooltipBuilder = new();
		dtrTooltipBuilder.AddText($"Peer Sync");

		if (Plugin.Connections.Count > 0)
			dtrEntryBuilder.AddText($"{Plugin.Connections.Count}");

		if (Plugin.Instance != null)
		{
			lock (Plugin.Instance.SyncProviders)
			{
				foreach (SyncProviderBase sync in Plugin.Instance.SyncProviders)
				{
					sync.GetDtrStatus(ref dtrEntryBuilder, ref dtrTooltipBuilder);
				}
			}
		}

		this.dtrBarEntry.Text = dtrEntryBuilder.ToString();
		this.dtrBarEntry.Tooltip = dtrTooltipBuilder.ToString();
	}

	private void OnDtrClicked(DtrInteractionEvent @evt)
	{
		Plugin.Instance?.MainWindow.Toggle();
	}
}