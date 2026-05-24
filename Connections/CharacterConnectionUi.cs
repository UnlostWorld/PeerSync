// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Connections;

using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using PeerSync.SyncProviders;
using PeerSync.UI;

public partial class CharacterConnection
{
	public void DrawStatus()
	{
		List<SyncProgressBase>? progresses = Plugin.Instance?.GetSyncProgress(this);

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
				Plugin.Instance?.InspectWindow.Show(this);
			}

			ImGui.Separator();

			if (Configuration.Current.GetIsBlocked(this.CharacterName, this.CharacterWorld))
			{
				if (ImGui.MenuItem("Unblock"))
				{
					Configuration.Current.SetIsBlocked(this.CharacterName, this.CharacterWorld, false);
				}
			}
			else
			{
				if (ImGui.MenuItem("Block"))
				{
					Configuration.Current.SetIsBlocked(this.CharacterName, this.CharacterWorld, true);
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

			ImGuiEx.Icon(this.CurrentStatus.GetIcon());
			ImGui.SameLine();
			ImGui.Text(this.CurrentStatus.GetMessage());

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

			ImGui.Separator();

			if (progresses != null)
			{
				if (ImGui.BeginTable("PeerProgressInfoTable", 3))
				{
					ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed);
					ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
					ImGui.TableSetupColumn("Info", ImGuiTableColumnFlags.WidthStretch);
					ImGui.TableNextRow();

					foreach (SyncProgressBase progress in progresses)
					{
						// This user sent no date for this sync progress
						if (progress.Status == SyncProgressStatus.Empty
						|| progress.Status == SyncProgressStatus.None
						|| progress.Status == SyncProgressStatus.NotApplied)
							continue;

						ImGui.TableNextColumn();
						progress.DrawStatus();

						ImGui.TableNextColumn();
						ImGui.Text(progress.Provider.DisplayName);

						ImGui.TableNextColumn();
						progress.DrawInfo();

						ImGui.TableNextRow();
					}

					foreach (SyncProgressBase progress in progresses)
					{
						// This user sent no date for this sync progress
						if (progress.Status != SyncProgressStatus.NotApplied)
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

				ImGui.Spacing();
			}

			ImGui.Spacing();

			ImGui.TextDisabled("Right-click for more options");
			ImGui.EndTooltip();
		}

		// Name
		ImGui.TableNextColumn();
		ImGui.Text($"{this.CharacterName} @ {this.CharacterWorld}");

		// Progress
		ImGui.TableNextColumn();
		if (progresses != null)
		{
			long total = 0;
			long current = 0;

			foreach (SyncProgressBase progress in progresses)
			{
				if (progress.Status == SyncProgressStatus.Syncing)
				{
					progress.Combine(ref current, ref total);
				}
			}

			float p = (float)current / (float)total;

			if (total <= 0)
				p = 1;

			if (p < 1)
			{
				ImGuiEx.ThinProgressBar(p);
			}
		}

		// Status
		ImGui.TableNextColumn();
		ImGuiEx.Icon(this.CurrentStatus.GetIcon());
	}
}