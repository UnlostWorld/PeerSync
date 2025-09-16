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

public class AddPeerWindow : Window, IDisposable
{
	private string character = string.Empty;
	private string password = string.Empty;
	private bool showNameInvalid = false;

	public AddPeerWindow()
		: base(
		"Add peer##PeerWindow",
		ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)
	{
		this.SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(350, -1),
			MaximumSize = new Vector2(350, -1),
		};
	}

	public void Show(string? characterName = null, string? world = null)
	{
		this.character = string.Empty;
		this.password = string.Empty;

		if (characterName != null && world != null)
			this.character = $"{characterName} @ {world}";

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
		ImGui.Text("Add a new peer");
		ImGui.Spacing();

		if (this.showNameInvalid)
		{
			ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
			ImGui.PushStyleColor(ImGuiCol.Border, 0xFF0080FF);
		}

		ImGui.SetNextItemWidth(-1);
		ImGui.InputTextWithHint("###Character", "Meteor Survivor @ Etheirys", ref this.character);

		if (this.showNameInvalid)
		{
			ImGui.PopStyleColor();
			ImGui.PopStyleVar();
		}

		ImGui.SetNextItemWidth(-1);
		ImGui.InputTextWithHint("###Password", "Password", ref this.password);

		ImGui.SetWindowFontScale(0.9f);
		ImGui.TextColoredWrapped(0x80FFFFFF, "You will only be unable to connect to this peer if they have also added your current character as a peer.");
		ImGui.SetWindowFontScale(1.0f);

		ImGui.Spacing();

		ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationTriangle, 1);
		ImGui.SameLine();
		ImGui.TextColoredWrapped(0xFF0080FF, "You should only add people you trust as peers.");

		ImGui.TextColoredWrapped(0xFF0080FF, "Malicious users could sync inappropriate or unstable mods with you, causing distress or crashes.");

		ImGui.Spacing();

		string name = string.Empty;
		string world = string.Empty;

		bool valid = !string.IsNullOrEmpty(this.character)
			&& !string.IsNullOrEmpty(this.password);

		if (valid)
		{
			this.showNameInvalid = false;
			string[] parts = this.character.Split('@', StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length == 2)
			{
				name = parts[0].Trim();
				world = parts[1].Trim();

				if (string.IsNullOrEmpty(name)
					|| string.IsNullOrEmpty(world)
					|| !name.Contains(' '))
				{
					valid = false;
					this.showNameInvalid = true;
				}
			}
			else
			{
				valid = false;
				this.showNameInvalid = true;
			}
		}
		else
		{
			this.showNameInvalid = false;
		}

		if (!valid)
			ImGui.BeginDisabled();

		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - (200 + (ImGui.GetStyle().ItemSpacing.X * 2))));

		if (ImGui.Button($"Add Peer", new(100, 0)))
		{
			Configuration.Peer? peer = Configuration.Current.GetPeer(name, world);

			if (peer == null)
			{
				peer = new Configuration.Peer();
				Configuration.Current.Pairs.Add(peer);
			}

			peer.CharacterName = name;
			peer.World = world;
			peer.Password = this.password;

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