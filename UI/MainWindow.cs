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
using System.Collections.Generic;
using System.Numerics;

public class MainWindow : Window, IDisposable
{
	private Configuration.Character? editingCharacterPassword = null;

	private bool expandedCharacters = false;
	private bool expandedConnections = true;
	private bool expandedFriends = false;

	public MainWindow()
#if DEBUG
		: base($"Peer Sync - Debug##PeerSyncMainWindow")
#else
		: base($"Peer Sync - v{Plugin.PluginInterface.Manifest.AssemblyVersion}##PeerSyncMainWindow")
#endif
	{
		this.SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(350, 450),
			MaximumSize = new Vector2(350, float.MaxValue),
		};
	}

	public void Dispose()
	{
	}

	public override void Draw()
	{
		Plugin.Index.DrawStatus();

		Plugin? plugin = Plugin.Instance;
		if (plugin == null)
			return;

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
				if (plugin.LocalCharacter != character)
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
					if (plugin.LocalCharacter == character)
					{
						continue;
					}

					this.DrawCharacterEntry(character);
				}
			}
		}

		ImGui.EndTable();

		ImGuiEx.Header(ref this.expandedConnections, $"Connections");
		Plugin.Connections.DrawStatus(this.expandedConnections);

		bool addFriend;
		ImGuiEx.Header(ref this.expandedFriends, $"Friends", out addFriend);
		if (addFriend)
		{
			Plugin.Instance?.AddPeerWindow.Show();
		}

		if (this.expandedFriends)
		{
			if (Configuration.Current.Pairs.Count <= 0)
			{
				ImGui.Indent();
				ImGuiEx.Icon(FontAwesomeIcon.SadCry, 1.0f);
				ImGui.Unindent();
			}
			else
			{
				if (ImGui.BeginTable("PeersTable", 2))
				{
					ImGui.TableSetupColumn("Hover", ImGuiTableColumnFlags.WidthFixed);
					ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);

					List<string> peerNames = new();
					Dictionary<string, Configuration.Peer> peerLookup = new();
					foreach (Configuration.Peer peer in Configuration.Current.Pairs)
					{
						string compoundName = $"{peer.CharacterName} @ {peer.World}";

						if (peerLookup.ContainsKey(compoundName))
							continue;

						peerNames.Add(compoundName);
						peerLookup.Add(compoundName, peer);
					}

					peerNames.Sort();

					foreach (string peerName in peerNames)
					{
						if (!peerLookup.TryGetValue(peerName, out Configuration.Peer? peer) || peer == null)
							continue;

						this.DrawPeerEntry(peer);
						ImGui.TableNextRow();
					}

					ImGui.EndTable();
				}
			}
		}

		foreach (SyncProviderBase syncProvider in plugin.SyncProviders)
		{
			syncProvider.DrawStatus();
		}

		ImGui.Spacing();
		ImGui.Spacing();

		if (ImGui.CollapsingHeader($"Settings"))
		{
			int port = Configuration.Current.Port;
			if (ImGui.InputInt("Custom Port", ref port))
			{
				Configuration.Current.Port = (ushort)port;
				Configuration.Current.Save();
			}

			ImGui.LabelText("Current Port", Configuration.Current.LastPort.ToString());

			foreach (SyncProviderBase syncProvider in plugin.SyncProviders)
			{
				syncProvider.DrawSettings();
			}
		}
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

			if (Plugin.Instance?.LocalCharacter == character)
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
		if (Plugin.Instance?.LocalCharacter == character)
		{
			ImGuiEx.Icon(FontAwesomeIcon.Wifi);
		}

		ImGui.TableNextRow();
	}

	private void DrawPeerEntry(Configuration.Peer peer)
	{
		string pId = peer.GetFingerprint();

		// Tooltip
		ImGui.TableNextColumn();
		ImGui.Selectable(
			$"##RowSelector{pId}",
			false,
			ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

		if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
			&& ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.OpenPopup($"peer_{pId}_contextMenu");
		}

		if (ImGui.BeginPopup(
			$"peer_{pId}_contextMenu",
			ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings))
		{
			ImGui.PushID($"peer_{pId}_contextMenu");

			if (ImGui.MenuItem("Remove"))
			{
				DialogBox.Show(
					"Confirm",
					$"Are you sure you want to remove the peer\n{peer.CharacterName} @ {peer.World} ?",
					FontAwesomeIcon.ExclamationTriangle,
					0xFF0080FF,
					"Remove",
					"Cancel",
					() =>
					{
						Configuration.Current.Pairs.Remove(peer);
						Configuration.Current.Save();
					});
			}

			ImGui.PopID();
			ImGui.EndPopup();
		}

		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.SetNextWindowSizeConstraints(new Vector2(256, 0), new Vector2(256, 400));
			ImGui.BeginTooltip();

			ImGui.Text($"{peer.CharacterName} @ {peer.World}");
			ImGui.Separator();

			ImGuiEx.Icon(0xFFFFFFFF, FontAwesomeIcon.Fingerprint, 1.15f);
			ImGui.SameLine();
			ImGui.SetWindowFontScale(0.75f);
			ImGui.TextColoredWrapped(0x80FFFFFF, $"{peer.GetFingerprint()}");
			ImGui.SetWindowFontScale(1.0f);
			ImGui.Separator();

			ImGui.Spacing();

			ImGui.TextDisabled("Right-click for more options");
			ImGui.EndTooltip();
		}

		// Name
		ImGui.TableNextColumn();
		ImGui.Text($"{peer.CharacterName} @ {peer.World}");
	}
}
