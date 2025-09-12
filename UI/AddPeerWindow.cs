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
	private string characterName = string.Empty;
	private string world = string.Empty;
	private string password = string.Empty;

	public AddPeerWindow()
		: base(
		"Add peer##PeerWindow",
		ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize)
	{
		this.SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(450, -1),
			MaximumSize = new Vector2(450, -1),
		};
	}

	public void Show(string characterName = "", string world = "")
	{
		this.characterName = characterName;
		this.world = world;
		this.IsOpen = true;
	}

	public void Dispose()
	{
	}

	public override void Draw()
	{
		ImGui.TextWrapped("You will only be unable to connect to this peer if they have added your current character as a peer.");

		ImGui.InputText("Name", ref this.characterName);
		ImGui.InputText("World", ref this.world);
		ImGui.InputText("Password", ref this.password);

		ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationTriangle, 1);
		ImGui.SameLine();
		ImGui.TextColoredWrapped(0xFF0080FF, "You should add people you trust as peers.");
		ImGui.TextColoredWrapped(0xFF0080FF, "Malicious users could sync inappropriate or unstable mods with you, causing distress or crashes.");

		bool valid = !string.IsNullOrEmpty(this.characterName)
			&& !string.IsNullOrEmpty(this.world)
			&& !string.IsNullOrEmpty(this.password);

		if (!valid)
			ImGui.BeginDisabled();

		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - (200 + (ImGui.GetStyle().ItemSpacing.X * 2))));

		if (ImGui.Button("Add Peer", new(100, 0)))
		{
			Configuration.Peer? peer = Configuration.Current.GetPeer(this.characterName, this.world);

			if (peer == null)
			{
				peer = new Configuration.Peer();
				Configuration.Current.Pairs.Add(peer);
			}

			peer.CharacterName = this.characterName;
			peer.World = this.world;
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