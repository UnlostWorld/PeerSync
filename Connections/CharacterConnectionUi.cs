// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Connections;

using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using PeerSync.Overlays;
using PeerSync.SyncProviders;
using PeerSync.UI;

public partial class CharacterConnection
{
	public void DrawStatus()
	{
		string sId = this.CharacterId;

		// Tooltip
		ImGui.TableNextColumn();
		ImGui.Selectable(
			$"##RowSelector{sId}",
			false,
			ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

		if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
			&& ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.OpenPopup($"peer_{sId}_contextMenu");
		}

		if (ImGui.BeginPopup(
			$"peer_{sId}_contextMenu",
			ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings))
		{
			ImGui.PushID($"peer_{sId}_contextMenu");

			if (ImGui.MenuItem("Inspect"))
			{
				Plugin.Ui.InspectWindow.Show(this);
			}

			ImGui.Separator();

			if (Configuration.Current.GetIsBlocked(this.CharacterName, this.CharacterWorld))
			{
				if (ImGui.MenuItem("Unblock"))
				{
					Configuration.Current.SetIsBlocked(this.CharacterName, this.CharacterWorld, false);
					this.Reset();
				}
			}
			else
			{
				if (ImGui.MenuItem("Block"))
				{
					Configuration.Current.SetIsBlocked(this.CharacterName, this.CharacterWorld, true);
					this.Reset();
				}
			}

			ImGui.PopID();
			ImGui.EndPopup();
		}

		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.SetNextWindowSizeConstraints(new Vector2(256, 0), new Vector2(256, 400));
			ImGui.BeginTooltip();

			ImGui.Text($"{this.CharacterName} @ {this.CharacterWorld}");
			ImGui.Separator();

			if (this.lastState == States.NotFound)
			{
				TimeSpan p = Timeout - this.TimeSinceLastSeen;

				ImGuiEx.Icon(FontAwesomeIcon.Binoculars);
				ImGui.SameLine();
				ImGui.Text("Looking for character");

				ImGui.Text($"Disconnect in {(int)p.TotalSeconds} seconds");
			}

			if (this.IsConnected)
			{
				ImGuiEx.Icon(FontAwesomeIcon.Wifi);
				ImGui.SameLine();
				ImGui.Text("Connected");

				// Direction
				if (this.outgoingConnection?.IsConnected == true && this.incomingConnection?.IsConnected == true)
				{
					ImGui.SameLine();
					ImGui.Text("(Duplex)");
				}
				else if (this.outgoingConnection?.IsConnected == true)
				{
					ImGui.SameLine();
					ImGui.Text("(Client)");
				}
				else if (this.incomingConnection?.IsConnected == true)
				{
					ImGui.SameLine();
					ImGui.Text("(Host)");
				}
			}

			if (this.IsOffline)
			{
				ImGuiEx.Icon(FontAwesomeIcon.Bed);
				ImGui.SameLine();
				ImGui.Text("Offline");
			}

			if (this.IsWaitingForData)
			{
				ImGuiEx.Icon(FontAwesomeIcon.Hourglass);
				ImGui.SameLine();
				ImGui.Text("Waiting for data");
			}

			ImGui.Separator();

			if (this.lastConnectionException != null)
			{
				ImGui.TextColoredWrapped(0xFF0080FF, this.lastConnectionException.Message);
				ImGui.Separator();
			}

			if (this.IsBlocked)
			{
				ImGuiEx.Icon(FontAwesomeIcon.Stop);
				ImGui.SameLine();
				ImGui.Text("Blocked");
				ImGui.Separator();
			}

			this.DrawProgressGroup("Character", this.characterProgress);
			this.DrawProgressGroup("Mount / Minion", this.mountProgress);
			this.DrawProgressGroup("Pet", this.petProgress);

			ImGui.Spacing();

			ImGui.TextDisabled("Right-click for more options");
			ImGui.EndTooltip();
		}

		// Name
		ImGui.TableNextColumn();
		ImGui.Text($"{this.CharacterName} @ {this.CharacterWorld}");

		// Progress
		ImGui.TableNextColumn();
		float totalProgress = this.GetTotalProgress();
		if (totalProgress < 1)
		{
			ImGuiEx.ThinProgressBar(totalProgress);
		}

		// Status
		ImGui.TableNextColumn();

		if (this.lastState == States.NotFound)
		{
			ImGuiEx.Icon(FontAwesomeIcon.Binoculars);
		}
		else
		{
			if (this.IsBlocked)
			{
				ImGuiEx.Icon(FontAwesomeIcon.Stop);
			}
			else if (this.IsConnected)
			{
				ImGuiEx.Icon(FontAwesomeIcon.Wifi);
			}
			else if (this.IsOffline)
			{
				ImGuiEx.Icon(FontAwesomeIcon.Bed);
			}
			else if (this.IsWaitingForData)
			{
				ImGuiEx.Icon(FontAwesomeIcon.Hourglass);
			}
		}
	}

	public float GetTotalProgress()
	{
		long total = 0;
		long current = 0;

		foreach (SyncContext progress in this.characterProgress.Values)
		{
			if (progress.Status == SyncProgressStatus.Syncing)
			{
				progress.Combine(ref current, ref total);
			}
		}

		foreach (SyncContext progress in this.mountProgress.Values)
		{
			if (progress.Status == SyncProgressStatus.Syncing)
			{
				progress.Combine(ref current, ref total);
			}
		}

		foreach (SyncContext progress in this.petProgress.Values)
		{
			if (progress.Status == SyncProgressStatus.Syncing)
			{
				progress.Combine(ref current, ref total);
			}
		}

		float p = (float)current / (float)total;

		if (total <= 0)
			p = 1;

		return p;
	}

	private void DrawProgressGroup(string label, Dictionary<SyncProviderBase, SyncContext> progresses)
	{
		if (progresses.Count <= 0)
			return;

		ImGui.Text(label);
		if (ImGui.BeginTable($"{label}PeerProgressInfoTable", 3))
		{
			ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Info", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableNextRow();

			foreach (SyncProviderBase provider in Plugin.Sync.Providers)
			{
				SyncContext? progress = null;
				if (!progresses.TryGetValue(provider, out progress) || progress == null)
					continue;

				// The remote peer sent no data for this provider
				if (progress.Status == SyncProgressStatus.Empty)
					continue;

				// The local peer does not support this provider
				if (progress.Status == SyncProgressStatus.None)
					continue;

				ImGui.TableNextColumn();
				progress.DrawStatus();

				ImGui.TableNextColumn();
				ImGui.Text(progress.Provider.DisplayName);

				ImGui.TableNextColumn();
				progress.DrawInfo();

				ImGui.TableNextRow();
			}

			ImGui.EndTable();
		}
	}
}