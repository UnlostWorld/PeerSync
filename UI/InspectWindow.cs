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
using PeerSync.SyncProviders;
using System;
using System.Numerics;

public class InspectWindow : Window, IDisposable
{
	private CharacterSync? character;

	public InspectWindow()
		: base("Inspect##InspectWindow")
	{
		this.SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(800, 600),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
		};
	}

	public void Show(CharacterSync? character = null)
	{
		this.character = character;
		this.IsOpen = true;
	}

	public void Dispose()
	{
	}

	public override void Draw()
	{
		Plugin? plugin = Plugin.Instance;
		if (plugin == null)
			return;

		Configuration.Peer? peer = plugin.LocalCharacter;
		if (this.character != null)
			peer = this.character.Peer;

		if (peer == null)
			return;

		ImGui.Text($"{peer.CharacterName} @ {peer.World}");

		ImGuiEx.Icon(FontAwesomeIcon.Fingerprint);
		ImGui.SameLine();
		ImGui.Text($"{peer.GetFingerprint()}");

		if (this.character != null)
		{
			ImGuiEx.Icon(this.character.CurrentStatus.GetIcon());
			ImGui.SameLine();
			ImGui.Text(this.character.CurrentStatus.GetMessage());

			this.character.LastData?.DrawInspect();
		}
		else
		{
			plugin.LocalCharacterData.DrawInspect();
		}
	}
}