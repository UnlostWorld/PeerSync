// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioSync.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

public class MainWindow : Window, IDisposable
{
	// We give this window a hidden ID using ##.
	// The user will see "My Amazing Window" as window title,
	// but for ImGui the ID is "My Amazing Window##With a hidden ID"
	public MainWindow() : base(
		"Studio Sync##MainWindow",
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
		ImGui.Text(Plugin.Status);

		if (ImGui.BeginTabBar("##tabs"))
		{
			if (ImGui.BeginTabItem("Current Character"))
			{
				if (Plugin.CharacterName != null && Plugin.World != null)
				{
					StUi.TextBlock("Name", $"{Plugin.CharacterName} @ {Plugin.World}");

					string password = Configuration.Current.GetPassword(Plugin.CharacterName, Plugin.World) ?? string.Empty;
					if (StUi.TextBox("Password", ref password))
						Configuration.Current.SetPassword(Plugin.CharacterName, Plugin.World, password);

					StUi.TextBlockLarge("Identifier", Plugin.LocalCharacterId ?? "");
				}

				ImGui.EndTabItem();
			}

			if (ImGui.BeginTabItem("All Pairs"))
			{
				ImGui.BeginTable("#pairstable", 3);
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
					ImGui.Text(sync?.Status ?? "");

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}

			ImGui.EndTabBar();
		}
	}
}