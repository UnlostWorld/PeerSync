// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

public class PairWindow : Window, IDisposable
{
	private Configuration.Pair? pair = null;
	private string newPassword = string.Empty;

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
		this.pair = Configuration.Current.GetPair(characterName, world);
		this.IsOpen = true;

		if (this.pair == null)
		{
			this.pair = new();
			this.pair.CharacterName = characterName;
			this.pair.World = world;
		}
	}

	public void Dispose() { }

	public override void Draw()
	{
		if (this.pair == null
			|| this.pair.CharacterName == null
			|| this.pair.World == null)
			return;

		ImGui.LabelText("Character", $"{this.pair.CharacterName} @ {this.pair.World}");

		ImGui.InputText("Password", ref this.newPassword);

		ImGui.Separator();
		if (this.newPassword.Length < 6)
			ImGui.BeginDisabled();

		if (ImGui.Button("Pair"))
		{
			this.pair.Password = this.newPassword;

			if (Configuration.Current.GetPair(this.pair.CharacterName, this.pair.World) == null)
				Configuration.Current.Pairs.Add(this.pair);

			Configuration.Current.Save();
			this.IsOpen = false;
		}

		ImGui.EndDisabled();
	}
}