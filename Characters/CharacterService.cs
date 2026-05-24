// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Characters;

using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using PeerSync.UI;

public class CharacterService : IDisposable
{
	private bool expandedCharacters = false;
	private Configuration.Character? editingCharacterPassword = null;

	public Configuration.Character? Current { get; private set; }

	public string? GetCurrentCharacterId()
	{
		if (this.Current == null)
			return null;

		return $"{this.Current.CharacterName}@{this.Current.World}";
	}

	public void Dispose()
	{
		this.Current = null;
	}

	public void FrameworkUpdate()
	{
		Configuration.Character? localCharacter = this.UpdateLocalCharacter();
		if (this.Current != localCharacter)
		{
			this.Current = localCharacter;
		}
	}

	public void DrawStatus()
	{
		ImGuiEx.Header(ref this.expandedCharacters, $"Characters");
		if (ImGui.BeginTable("CharactersTable", 4))
		{
			ImGui.TableSetupColumn("Hover", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Password", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 15);
			ImGui.TableNextRow();

			// Draw current character first
			foreach (Configuration.Character character in Configuration.Current.Characters.AsReadOnly())
			{
				if (this.Current != character)
				{
					continue;
				}

				this.DrawCharacterEntry(character);
			}

			if (this.expandedCharacters)
			{
				// Draw everyone else
				foreach (Configuration.Character character in Configuration.Current.Characters.AsReadOnly())
				{
					if (this.Current == character)
					{
						continue;
					}

					this.DrawCharacterEntry(character);
				}
			}
		}

		ImGui.EndTable();
	}

	private void DrawCharacterEntry(Configuration.Character character)
	{
		string cId = character.GetFingerprint();

		// Tooltip
		ImGui.TableNextColumn();
		ImGui.Selectable(
			$"##RowSelector{cId}",
			false,
			ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

		if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
			&& ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.OpenPopup($"character_{cId}_contextMenu");
		}

		if (ImGui.BeginPopup(
			$"character_{cId}_contextMenu",
			ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings))
		{
			ImGui.PushID($"character_{cId}_contextMenu");

			if (this.Current == character)
			{
				if (ImGui.MenuItem("Inspect"))
				{
					Plugin.Instance?.InspectWindow.Show();
				}

				ImGui.Separator();
			}

			if (ImGui.MenuItem("Remove"))
			{
				DialogBox.Show(
					"Confirm",
					$"Are you sure you want to remove the character\n{character.CharacterName} @ {character.World} ?",
					FontAwesomeIcon.ExclamationTriangle,
					0xFF0080FF,
					"Remove",
					"Cancel",
					() =>
					{
						Configuration.Current.Characters.Remove(character);
						Configuration.Current.Save();
					});
			}

			if (ImGui.MenuItem("Copy Password"))
			{
				ImGui.SetClipboardText(character.Password ?? string.Empty);
			}

			if (ImGui.MenuItem("Edit Password"))
			{
				this.editingCharacterPassword = character;
			}

			if (ImGui.MenuItem("Randomize Password"))
			{
				character.GeneratePassword();
				Configuration.Current.Save();
			}

			ImGui.PopID();
			ImGui.EndPopup();
		}

		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.SetNextWindowSizeConstraints(new Vector2(256, 0), new Vector2(256, 400));
			ImGui.BeginTooltip();

			ImGui.Text($"{character.CharacterName} @ {character.World}");
			ImGui.Separator();

			ImGuiEx.Icon(0xFFFFFFFF, FontAwesomeIcon.Fingerprint, 1.15f);
			ImGui.SameLine();
			ImGui.SetWindowFontScale(0.75f);
			ImGui.TextColoredWrapped(0x80FFFFFF, $"{character.GetFingerprint()}");
			ImGui.SetWindowFontScale(1.0f);
			ImGui.Separator();

			ImGui.TextWrapped("Peers can only connect to this character if they have this password. It is safe to give this password to people you trust and want to connect with.");

			ImGui.Spacing();

			ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationTriangle);
			ImGui.SameLine();
			ImGui.TextColoredWrapped(0xFF0080FF, "Changing this password will break any connections to this character, Peers will be unable to sync with this character until they receive the updated password.");

			ImGui.Spacing();

			ImGui.TextDisabled("Right-click for more options");
			ImGui.EndTooltip();
		}

		ImGui.TableNextColumn();
		ImGui.Text($"{character.CharacterName} @ {character.World}");

		ImGui.TableNextColumn();
		string password = character.Password ?? string.Empty;

		if (this.editingCharacterPassword == character)
		{
			ImGui.PushItemWidth(-1);
			ImGui.SetKeyboardFocusHere();
			if (ImGui.InputText($"###Password{cId}", ref password, 256, ImGuiInputTextFlags.EnterReturnsTrue))
			{
				character.Password = password;
				character.ClearFingerprint();
				Configuration.Current.Save();
				this.editingCharacterPassword = null;
			}

			ImGui.PopItemWidth();
		}
		else
		{
			ImGui.BeginDisabled();
			ImGui.PushItemWidth(-1);
			ImGui.InputText("###Password{character}", ref password);
			ImGui.PopItemWidth();
			ImGui.EndDisabled();
		}

		ImGui.TableNextColumn();
		if (this.Current == character)
		{
			ImGuiEx.Icon(FontAwesomeIcon.Wifi);
		}

		ImGui.TableNextRow();
	}

	private Configuration.Character? UpdateLocalCharacter()
	{
		if (Plugin.Condition[ConditionFlag.BetweenAreas]
			|| Plugin.Condition[ConditionFlag.BetweenAreas51]
			|| Plugin.Condition[ConditionFlag.LoggingOut])
		{
			return null;
		}

		if (!Plugin.ClientState.IsLoggedIn)
		{
			return null;
		}

		IPlayerCharacter? player = Plugin.ObjectTable.LocalPlayer;
		if (player == null)
		{
			return null;
		}

		string characterName = player.Name.ToString();
		string world = player.HomeWorld.Value.Name.ToString();

		if (this.Current != null)
		{
			if (this.Current.CharacterName == characterName
				&& this.Current.World == world)
			{
				return this.Current;
			}
		}

		foreach (Configuration.Character character in Configuration.Current.Characters)
		{
			if (character.CharacterName == characterName && character.World == world)
			{
				return character;
			}
		}

		Configuration.Character newCharacter = new();
		newCharacter.CharacterName = characterName;
		newCharacter.World = world;
		newCharacter.GeneratePassword();
		Configuration.Current.Characters.Add(newCharacter);
		Configuration.Current.Save();
		return newCharacter;
	}
}