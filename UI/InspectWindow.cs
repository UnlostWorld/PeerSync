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
using PeerSync.Connections;
using PeerSync.SyncProviders;
using System;
using System.Numerics;

public class InspectWindow : Window, IDisposable
{
	private CharacterConnection? character;

	public InspectWindow()
		: base("Inspect##InspectWindow")
	{
		this.SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(800, 600),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
		};
	}

	public void Show(CharacterConnection? character = null)
	{
		this.character = character;
		this.IsOpen = true;
	}

	public void Dispose()
	{
	}

	public override void Draw()
	{
		if (this.character != null)
		{
			ImGui.Text($"{this.character.CharacterName} @ {this.character.CharacterWorld}");

			this.character.LastData?.DrawInspect();
		}
		else if (Plugin.Characters.Current != null)
		{
			ImGui.Text($"{Plugin.Characters.Current.CharacterName} @ {Plugin.Characters.Current.World}");

			ImGuiEx.Icon(FontAwesomeIcon.Fingerprint);
			ImGui.SameLine();
			ImGui.Text($"{Plugin.Characters.Current.GetFingerprint()}");

			Plugin.Sync.LocalCharacterData.DrawInspect();
		}
	}
}