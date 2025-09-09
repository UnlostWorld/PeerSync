// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

public static class ImGuiEx
{
	public static void Icon(FontAwesomeIcon icon)
	{
		ImGuiEx.Icon(0xFFFFFFFF, icon);
	}

	public static void Icon(uint color, FontAwesomeIcon icon, float size = 0.75f)
	{
		if (icon == FontAwesomeIcon.None)
		{
			ImGui.Text("   ");
		}
		else
		{
			ImGui.PushFont(UiBuilder.IconFont);
			ImGui.SetWindowFontScale(size);
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3);

			ImGui.SetNextItemWidth(ImGui.GetTextLineHeight());
			ImGui.TextColored(color, icon.ToIconString());

			ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 3);
			ImGui.SetWindowFontScale(1.0f);
			ImGui.PopFont();
		}
	}

	public static void ThinProgressBar(float p, float width = 72)
	{
		Vector2 start = ImGui.GetCursorPos();
		float height = 5.0f;
		float top = (ImGui.GetTextLineHeight() / 2) - (height / 2) + 1;
		ImGui.SetCursorPosY(start.Y + top);
		ImGui.ProgressBar(p, new(width, 5), string.Empty);
		ImGui.SetCursorPos(start + new Vector2(width, 0));
		ImGui.Text(string.Empty);
	}
}