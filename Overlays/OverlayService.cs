// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Overlays;

using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

public class OverlayService : IDisposable
{
	private readonly List<OverlayBase> overlays = new();

	public OverlayService()
	{
	}

	public void AddOverlay(OverlayBase overlay)
	{
		this.overlays.Add(overlay);
	}

	public void RemoveOverlay(OverlayBase overlay)
	{
		this.overlays.Remove(overlay);
	}

	public void Draw()
	{
		ImGui.SetNextWindowPos(Vector2.Zero);
		ImGui.SetNextWindowSize(new Vector2(-1, -1));
		if (ImGui.Begin(
			"PeerSyncOverlays",
			ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDecoration))
		{
			try
			{
				foreach (OverlayBase overlay in this.overlays)
				{
					Vector3 pos = overlay.GetWorldPosition();
					Vector2 screenPos;
					if (Plugin.GameGui.WorldToScreen(pos, out screenPos))
					{
						ImGui.SetCursorPos(screenPos);
						overlay.Draw();
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error drawing overlays");
			}

			ImGui.End();
		}
	}

	public void Dispose()
	{
		for (int i = this.overlays.Count - 1; i >= 0; i--)
		{
			this.overlays[i].Dispose();
		}

		this.overlays.Clear();
	}
}