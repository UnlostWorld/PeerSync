// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

public class PairWindow : Window, IDisposable
{
	private string createPairCharacterName = string.Empty;
	private string createPairWorld = string.Empty;
	private string createPairPassword = string.Empty;

	public PairWindow() : base(
		"Create a pair##PairWindow",
		ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize)
	{
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(450, -1),
			MaximumSize = new Vector2(450, -1),
		};
	}

	public void Show(string characterName = "", string world = "")
	{
		this.createPairCharacterName = characterName;
		this.createPairWorld = world;
		this.IsOpen = true;
	}

	public void Dispose() { }

	public override void Draw()
	{
		ImGui.TextWrapped("You will be unable to connect to this peer if they have not added your current character to their pair list.");

		ImGui.InputText("Name", ref this.createPairCharacterName);
		ImGui.InputText("World", ref this.createPairWorld);
		ImGui.InputText("Password", ref this.createPairPassword);

		ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationTriangle, 1);
		ImGui.SameLine();
		ImGui.TextColoredWrapped(0xFF0080FF, "You should only pair with people you trust.");
		ImGui.TextColoredWrapped(0xFF0080FF, "Malicious users could sync inappropriate or unstable mods with you, causing distress or crashes.");


		bool valid = !string.IsNullOrEmpty(this.createPairCharacterName)
			&& !string.IsNullOrEmpty(this.createPairWorld)
			&& !string.IsNullOrEmpty(this.createPairPassword);

		if (!valid)
			ImGui.BeginDisabled();

		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - (200 + ImGui.GetStyle().ItemSpacing.X * 2)));

		if (ImGui.Button("Pair", new(100, 0)))
		{
			Configuration.Pair? pair = Configuration.Current.GetPair(this.createPairCharacterName, this.createPairWorld);

			if (pair == null)
			{
				pair = new Configuration.Pair();
				Configuration.Current.Pairs.Add(pair);
			}

			pair.CharacterName = this.createPairCharacterName;
			pair.World = this.createPairWorld;
			pair.Password = this.createPairPassword;

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