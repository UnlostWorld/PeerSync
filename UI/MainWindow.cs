// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

public class MainWindow : Window, IDisposable
{
	// We give this window a hidden ID using ##.
	// The user will see "My Amazing Window" as window title,
	// but for ImGui the ID is "My Amazing Window##With a hidden ID"
	public MainWindow() : base(
		"Peer Sync##MainWindow",
		ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(375, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};
	}

	public void Dispose() { }

	public override void Draw()
	{
		Plugin plugin = Plugin.Instance;
		ImGui.Text(plugin.Status);

		if (ImGui.BeginTabBar("##tabs"))
		{
			if (ImGui.BeginTabItem("Current Character"))
			{
				if (plugin.CharacterName != null && plugin.World != null)
				{
					StUi.TextBlock("Name", $"{plugin.CharacterName} @ {plugin.World}");

					string password = Configuration.Current.GetPassword(plugin.CharacterName, plugin.World) ?? string.Empty;
					if (StUi.TextBox("Password", ref password))
					{
						Configuration.Current.SetPassword(plugin.CharacterName, plugin.World, password);
					}
				}

				ImGui.EndTabItem();
			}

			if (ImGui.BeginTabItem("All Pairs"))
			{
				ImGui.BeginTable("#pairsTable", 4);
				foreach (Configuration.Pair pair in Configuration.Current.Pairs)
				{
					CharacterSync? sync = null;
					if (pair.CharacterName != null && pair.World != null)
						sync = Plugin.Instance.GetCharacterSync(pair.CharacterName, pair.World);

					ImGui.TableNextColumn();
					ImGui.Text(pair.CharacterName);
					ImGui.TableNextColumn();
					ImGui.Text(pair.World);
					ImGui.TableNextColumn();
					ImGui.Text(sync?.CurrentStatus.ToString() ?? "");

					ImGui.TableNextColumn();
					using (ImRaii.PushFont(UiBuilder.IconFont))
					{
						if (sync?.ConnectionType == CharacterSync.ConnectionTypes.Internet)
						{
							ImGui.Text(FontAwesomeIcon.Globe.ToIconString());
						}
						else if (sync?.ConnectionType == CharacterSync.ConnectionTypes.Local)
						{
							ImGui.Text(FontAwesomeIcon.NetworkWired.ToIconString());
						}
					}

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
				ImGui.EndTabItem();
			}

			if (ImGui.BeginTabItem("Settings"))
			{
				int port = Configuration.Current.Port;
				if (ImGui.InputInt("Custom Port", ref port))
				{
					Configuration.Current.Port = (ushort)port;
					Configuration.Current.Save();
				}


				ImGui.EndTabItem();
			}

			ImGui.EndTabBar();
		}
	}
}