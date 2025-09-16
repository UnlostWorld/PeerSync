// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

public static class DialogBox
{
	public static void Show(
		string title,
		string message,
		FontAwesomeIcon icon = FontAwesomeIcon.None,
		uint? color = null,
		string? leftButton = null,
		string rightButton = "OK",
		Action? leftCallback = null,
		Action? rightCallback = null)
	{
		if (Plugin.Instance == null)
			return;

		Plugin.Instance.DialogBox.Show(
			title,
			message,
			icon,
			color,
			leftButton,
			rightButton,
			leftCallback,
			rightCallback);
	}
}

public class DialogBoxWindow : Window, IDisposable
{
	private string title = string.Empty;
	private string message = string.Empty;
	private FontAwesomeIcon icon = FontAwesomeIcon.None;
	private uint? color;
	private string? leftButtonLabel;
	private string rightButtonLabel = "OK";
	private Action? leftCallback = null;
	private Action? rightCallback = null;

	public DialogBoxWindow()
		: base(
		"DialogBox##DialogBox",
		ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)
	{
		this.SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(350, -1),
			MaximumSize = new Vector2(350, -1),
		};
	}

	public void Show(
		string title,
		string message,
		FontAwesomeIcon icon = FontAwesomeIcon.None,
		uint? color = null,
		string? leftButton = null,
		string rightButton = "OK",
		Action? leftCallback = null,
		Action? rightCallback = null)
	{
		this.title = title;
		this.message = message;
		this.icon = icon;
		this.color = color;
		this.leftButtonLabel = leftButton;
		this.rightButtonLabel = rightButton;
		this.leftCallback = leftCallback;
		this.rightCallback = rightCallback;

		this.IsOpen = true;
	}

	public void Dispose()
	{
	}

	public override void PreDraw()
	{
		base.PreDraw();

		Vector2 center = ImGui.GetMainViewport().GetCenter();
		ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
	}

	public override void Draw()
	{
		if (this.icon != FontAwesomeIcon.None)
		{
			if (this.color != null)
			{
				ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationTriangle, 1);
				ImGui.SameLine();
			}
			else
			{
				ImGuiEx.Icon(FontAwesomeIcon.ExclamationTriangle, 1);
				ImGui.SameLine();
			}
		}

		ImGui.Text(this.title);
		ImGui.Spacing();
		ImGui.Spacing();

		ImGui.TextWrapped(this.message);

		ImGui.Spacing();
		ImGui.Spacing();

		if (this.leftButtonLabel != null)
		{
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - (200 + (ImGui.GetStyle().ItemSpacing.X * 2))));
			if (ImGui.Button($"{this.leftButtonLabel}###DialogLeft", new(100, 0)))
			{
				this.leftCallback?.Invoke();
				this.IsOpen = false;
			}
		}
		else
		{
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - (100 + (ImGui.GetStyle().ItemSpacing.X * 2))));
		}

		ImGui.SameLine();

		if (ImGui.Button($"{this.rightButtonLabel}###DialogRight", new(100, 0)))
		{
			this.rightCallback?.Invoke();
			this.IsOpen = false;
		}
	}
}