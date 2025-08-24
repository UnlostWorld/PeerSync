// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

public class PairWindow : Window, IDisposable
{
	private string? characterName;
	private string? world;
	private string password = string.Empty;

	public PairWindow() : base(
		"Peer Sync Pair##PairWindow",
		ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse)
	{
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(375, 175),
			MaximumSize = new Vector2(375, 175),
		};
	}

	public void Show(string characterName, string world)
	{
		this.characterName = characterName;
		this.world = world;
		this.password = Configuration.Current.GetPassword(this.characterName, this.world) ?? string.Empty;
		this.IsOpen = true;
	}

	public void Dispose() { }

	public override void Draw()
	{
		if (this.characterName == null || this.world == null)
			return;

		StUi.TextBlock("Character", $"{this.characterName} @ {this.world}");
		StUi.TextBox("Password", ref this.password);

		ImGui.Separator();
		if (password.Length < 6)
			ImGui.BeginDisabled();

		if (ImGui.Button("Pair"))
		{
			Configuration.Current.SetPassword(this.characterName, this.world, this.password);
			this.IsOpen = false;
		}

		ImGui.EndDisabled();
	}
}