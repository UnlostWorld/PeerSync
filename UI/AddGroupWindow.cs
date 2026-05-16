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

public class AddGroupWindow : Window, IDisposable
{
	private string name = string.Empty;
	private string password = string.Empty;

	public AddGroupWindow()
		: base(
		"Add group##GroupWindow",
		ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)
	{
		this.SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(350, -1),
			MaximumSize = new Vector2(350, -1),
		};
	}

	public void Show(string? groupName = null)
	{
		this.name = string.Empty;
		this.password = string.Empty;

		if (groupName != null)
			this.name = groupName;

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
		ImGui.Text("Create or join a Group");
		ImGui.Spacing();

		ImGui.SetNextItemWidth(-1);
		ImGui.InputTextWithHint("###GroupName", "The Scions of the Seventh Dawn", ref this.name);

		ImGui.SetNextItemWidth(-1);
		ImGui.InputTextWithHint("###Password", "Password", ref this.password);

		ImGui.SetWindowFontScale(0.9f);
		ImGui.TextColoredWrapped(0x80FFFFFF, "Anybody in this group will be able to connect with you.");
		ImGui.SetWindowFontScale(1.0f);

		ImGui.Spacing();

		ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationTriangle, 1);
		ImGui.SameLine();
		ImGui.TextColoredWrapped(0xFF0080FF, "You should only join groups you trust.");

		ImGui.TextColoredWrapped(0xFF0080FF, "Malicious users could sync inappropriate or unstable mods with you, causing distress or crashes.");

		ImGui.Spacing();

		bool valid = !string.IsNullOrEmpty(this.name) && !string.IsNullOrEmpty(this.password);

		if (!valid)
			ImGui.BeginDisabled();

		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - (200 + (ImGui.GetStyle().ItemSpacing.X * 2))));

		if (ImGui.Button($"Add Group", new(100, 0)))
		{
			Configuration.Group? group = Configuration.Current.GetGroup(this.name);

			if (group == null)
			{
				group = new Configuration.Group();
				Configuration.Current.Groups.Add(group);
			}

			group.Name = this.name;
			group.Password = this.password.Trim();

			Configuration.Current.Save();
			this.IsOpen = false;
		}

		if (!valid)
			ImGui.EndDisabled();

		ImGui.SameLine();

		if (ImGui.Button("Cancel", new(100, 0)))
		{
			this.IsOpen = false;
		}
	}
}