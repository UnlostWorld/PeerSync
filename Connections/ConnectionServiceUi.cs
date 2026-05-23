// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Connections;

using Dalamud.Bindings.ImGui;

public partial class ConnectionService
{
	public void DrawStatus(bool collapse)
	{
		if (ImGui.BeginTable("SyncTable", 4))
		{
			ImGui.TableSetupColumn("Hover", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 15);

			foreach (string id in this.alphabeticalIds)
			{
				if (!this.connectionLookup.TryGetValue(id, out CharacterConnection? connection) || connection == null)
					continue;

				if (collapse && connection.CurrentStatus == CharacterConnectionStatus.IndexingFailed)
					continue;

				connection.DrawStatus();

				ImGui.TableNextRow();
			}

			ImGui.EndTable();
		}
	}
}