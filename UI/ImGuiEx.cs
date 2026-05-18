// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Newtonsoft.Json;

public static class ImGuiEx
{
	public static bool Header(string label, bool button = false)
	{
		Vector2 startPos = ImGui.GetCursorPos();
		ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
		bool clicked = false;

		ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0x00000000);
		ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0x00000000);
		ImGui.Button(label);
		ImGui.PopStyleColor();
		ImGui.PopStyleColor();

		float height = ImGui.GetCursorPosY() - startPos.Y;

		ImGui.SameLine();
		Vector2 lineStartPos = ImGui.GetCursorPos();
		float width = ImGui.GetContentRegionAvail().X;

		if (button)
			width -= 25;

		ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (height / 2) - 1);
		ImGui.BeginChild(label, new Vector2(width, 1), true);
		ImGui.EndChild();

		if (button)
		{
			ImGui.SameLine();
			ImGui.SetCursorPosX(lineStartPos.X + width);
			ImGui.SetCursorPosY(lineStartPos.Y);
			clicked = ImGui.Button($"+###{label}", new Vector2(25, 0));
		}

		ImGui.PopStyleColor();
		return clicked;
	}

	public static void Icon(FontAwesomeIcon icon, float size = 0.75f)
	{
		if (icon == FontAwesomeIcon.None)
		{
			ImGui.Text("   ");
		}
		else
		{
			ImGui.PushFont(UiBuilder.IconFont);
			ImGui.SetWindowFontScale(size);
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);

			ImGui.SetNextItemWidth(ImGui.GetTextLineHeight());
			ImGui.Text(icon.ToIconString());

			ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2);
			ImGui.SetWindowFontScale(1.0f);
			ImGui.PopFont();
		}
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
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);

			ImGui.SetNextItemWidth(ImGui.GetTextLineHeight());
			ImGui.TextColored(color, icon.ToIconString());

			ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2);
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

	public static void BeginCenter(string strId)
	{
		ImGui.BeginTable(strId, 3);
		ImGui.TableSetupColumn("#L", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("#C", ImGuiTableColumnFlags.WidthFixed);
		ImGui.TableSetupColumn("#R", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableNextRow();
		ImGui.TableNextColumn();
		ImGui.TableNextColumn();
	}

	public static void EndCenter()
	{
		ImGui.TableNextColumn();
		ImGui.EndTable();
	}

	public static void JsonViewer(string id, string json)
	{
		object? obj = JsonConvert.DeserializeObject(json);
		json = JsonConvert.SerializeObject(obj, Formatting.Indented);

		ImGui.PushFont(UiBuilder.MonoFont);
		ImGui.InputTextMultiline(
			$"###JsonInspector{id}",
			ref json,
			10000,
			new Vector2(-1, 400),
			ImGuiInputTextFlags.ReadOnly);
		ImGui.PopFont();
	}

	public static void Size(long bytes)
	{
		if (bytes < 1024)
		{
			ImGui.Text($"{bytes} b");
			return;
		}

		float kiloBytes = bytes / 1024;
		if (kiloBytes < 1024)
		{
			ImGui.Text($"{kiloBytes.ToString("F1")} kb");
			return;
		}

		float megaBytes = kiloBytes / 1024;
		if (megaBytes < 1024)
		{
			ImGui.Text($"{megaBytes.ToString("F1")} mb");
			return;
		}

		float gigaBytes = megaBytes / 1024;
		if (gigaBytes < 1024)
		{
			ImGui.Text($"{gigaBytes.ToString("F1")} gb");
			return;
		}
	}
}