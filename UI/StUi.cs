// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using Dalamud.Bindings.ImGui;
using System.Numerics;

public static class StUi
{
	public static void TextBlock(string label, string text)
	{
		ImGui.Text(label + ":");
		ImGui.SetNextItemWidth(-1);
		ImGui.BeginDisabled();
		ImGui.InputText($"##{label}", ref text);
		ImGui.EndDisabled();
	}

	public static void TextBlockLarge(string label, string text, int lines = 3)
	{
		ImGui.Text(label + ":");
		ImGui.SetNextItemWidth(-1);
		ImGui.Spacing();
		if (ImGui.BeginChildFrame(1, new Vector2(-1, ImGui.GetTextLineHeightWithSpacing() * lines)))
		{
			ImGui.TextWrapped(text);
			ImGui.EndChildFrame();
		}
	}

	public static bool TextBox(string label, ref string text)
	{
		ImGui.Text(label + ":");
		ImGui.SetNextItemWidth(-1);
		return ImGui.InputText($"##{label}", ref text);
	}
}