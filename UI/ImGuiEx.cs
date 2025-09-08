// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

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