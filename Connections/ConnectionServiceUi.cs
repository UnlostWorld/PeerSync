// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Connections;

using Dalamud.Bindings.ImGui;
using PeerSync.UI;

public partial class ConnectionService
{
	private bool expandedConnections = false;

	public void DrawStatus()
	{
		ImGuiEx.Header(ref this.expandedConnections, $"Connections");

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

				if (!this.expandedConnections && !connection.IsConnected)
					continue;

				connection.DrawStatus();

				ImGui.TableNextRow();
			}

			ImGui.EndTable();
		}
	}
}