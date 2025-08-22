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
		if (Plugin.CharacterName == null || Plugin.World == null)
			return;

		ImGui.LabelText("Character", $"{Plugin.CharacterName} @ {Plugin.World}");

		string password = Configuration.Current.GetPassword(Plugin.CharacterName, Plugin.World) ?? string.Empty;
		if (ImGui.InputText("Password", ref password))
			Configuration.Current.SetPassword(Plugin.CharacterName, Plugin.World, password);
	}
}