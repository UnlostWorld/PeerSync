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
	private Configuration.Peer? peerToRemove = null;

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
		Plugin? plugin = Plugin.Instance;
		if (plugin == null)
			return;

		if (ImGui.BeginTable("StatusTable", 3))
		{
			ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 20);
			ImGui.TableSetupColumn("Text", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("Button", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableNextRow();

			ImGui.TableNextColumn();
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 6);
			ImGuiEx.Icon(plugin.Status.GetIcon());
			ImGui.TableNextColumn();
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 7);
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 3);
			ImGui.Text(plugin.Status.GetMessage());
			ImGui.TableNextColumn();

			if (plugin.Status == PluginStatus.Online)
			{
				ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
				ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
				ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 6));
				ImGui.PushStyleColor(ImGuiCol.Border, 0xFF000080);
				ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF000080);
				if (ImGui.Button("Stop"))
				{
					plugin.Stop();
				}

				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
				ImGui.PopStyleVar();
				ImGui.PopStyleVar();
				ImGui.PopStyleColor();
			}
			else if (plugin.Status == PluginStatus.Shutdown)
			{
				ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
				ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
				ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 6));
				ImGui.PushStyleColor(ImGuiCol.Border, 0xFF004000);
				ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF004000);
				if (ImGui.Button("Start"))
				{
					plugin.Start();
				}

				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
				ImGui.PopStyleVar();
				ImGui.PopStyleVar();
				ImGui.PopStyleColor();
			}
			else
			{
				ImGui.BeginDisabled();
				ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
				ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
				ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 6));
				ImGui.PushStyleColor(ImGuiCol.Border, 0xFF808080);
				ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF808080);
				ImGui.Button("Wait...");
				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
				ImGui.PopStyleVar();
				ImGui.PopStyleVar();
				ImGui.PopStyleColor();
				ImGui.EndDisabled();
			}

			ImGui.EndTable();
		}

		ImGui.Spacing();

		if (ImGui.CollapsingHeader($"Settings"))
		{
			int port = Configuration.Current.Port;
			if (ImGui.InputInt("Custom Port", ref port))
			{
				Configuration.Current.Port = (ushort)port;
				Configuration.Current.Save();
			}
		}

		if (ImGui.BeginPopup("AddIndexPopup"))
		{
			string newIndex = string.Empty;
			if (ImGui.InputText("Address", ref newIndex, 512, ImGuiInputTextFlags.EnterReturnsTrue))
			{
				newIndex = newIndex.TrimEnd('/', '\\');
				Configuration.Current.IndexServers.Add(newIndex);
				Configuration.Current.Save();
				ImGui.CloseCurrentPopup();
			}

			ImGui.EndPopup();
		}

		Vector2 startPos = ImGui.GetCursorPos();
		ImGui.SetCursorPosX(startPos.X + (ImGui.GetContentRegionAvail().X - 25));
		ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
		if (ImGui.Button("+###AddIndexButton", new Vector2(25, 0)))
		{
			ImGui.OpenPopup("AddIndexPopup");
		}

		ImGui.PopStyleColor();
		ImGui.SetCursorPos(startPos);

		bool indexServersSectionOpen = ImGui.CollapsingHeader("###IndexServersSection");
		ImGui.SameLine();

		if (Configuration.Current.IndexServers.Count <= 0)
		{
			ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationTriangle);
			ImGui.SameLine();
		}

		ImGui.Text($"Index Servers ({Configuration.Current.IndexServers.Count})");

		if (indexServersSectionOpen)
		{
			if (ImGui.BeginTable("IndexServersTable", 3))
			{
				if (Configuration.Current.IndexServers.Count <= 0)
				{
					ImGuiEx.BeginCenter("IndexServerWarningBox");
					ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationTriangle);
					ImGui.SameLine();
					ImGui.TextColored(0xFF0080FF, $"No index server");
					ImGuiEx.EndCenter();
				}
				else
				{
					ImGui.TableSetupColumn("Hover", ImGuiTableColumnFlags.WidthFixed);
					ImGui.TableSetupColumn("Url", ImGuiTableColumnFlags.WidthStretch);
					ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed);
					ImGui.TableNextRow();

					foreach (string indexServer in Configuration.Current.IndexServers.AsReadOnly())
					{
						int peerCount = 0;
						Plugin.Instance?.IndexServersStatus.TryGetValue(indexServer, out peerCount);

						// Tooltip
						ImGui.TableNextColumn();
						ImGui.Selectable(
							$"##RowSelector{indexServer}",
							false,
							ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

						if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
							&& ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
						{
							ImGui.OpenPopup($"index_{indexServer}_contextMenu");
						}

						if (ImGui.BeginPopup(
							$"index_{indexServer}_contextMenu",
							ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings))
						{
							ImGui.PushID($"index_{indexServer}_contextMenu");
							if (ImGui.MenuItem("Remove"))
							{
								Configuration.Current.IndexServers.Remove(indexServer);
								Configuration.Current.Save();
							}

							ImGui.PopID();
							ImGui.EndPopup();
						}

						if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
						{
							ImGui.SetNextWindowSizeConstraints(new Vector2(256, 0), new Vector2(256, 400));
							ImGui.BeginTooltip();

							ImGui.TextWrapped($"{indexServer}");
							ImGui.Separator();

							if (peerCount <= 0)
							{
								ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationCircle);
								ImGui.SameLine();
								ImGui.Text("Offline");
							}
							else
							{
								ImGuiEx.Icon(FontAwesomeIcon.Wifi);
								ImGui.SameLine();
								ImGui.Text($"{peerCount} online peers");
							}

							ImGui.TextDisabled("Right-click for more options");
							ImGui.EndTooltip();
						}

						// Url
						ImGui.TableNextColumn();
						string indexServerName = indexServer;
						indexServerName = indexServerName.Replace("http://", string.Empty);
						indexServerName = indexServerName.Replace("https://", string.Empty);
						indexServerName = indexServerName.Replace("www.", string.Empty);
						indexServerName = indexServerName.Replace(".ondigitalocean.app", string.Empty);
						ImGui.Text(indexServerName);

						// Status
						ImGui.TableNextColumn();

						if (peerCount > 0)
						{
							ImGuiEx.Icon(FontAwesomeIcon.Wifi);
						}
						else
						{
							ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationCircle);
						}

						ImGui.TableNextRow();
					}

					ImGui.EndTable();
				}
			}
		}

		if (ImGui.CollapsingHeader($"Characters ({Configuration.Current.Characters.Count})###CharactersSection", ImGuiTreeNodeFlags.Framed))
		{
			if (ImGui.BeginTable("CharactersTable", 3))
			{
				ImGui.TableSetupColumn("Hover", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Password", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableNextRow();

				foreach (Configuration.Character character in Configuration.Current.Characters.AsReadOnly())
				{
					// Tooltip
					ImGui.TableNextColumn();
					ImGui.Selectable(
						$"##RowSelector{character}",
						false,
						ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

					if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
						&& ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
					{
						ImGui.OpenPopup($"character_{character}_contextMenu");
					}

					if (ImGui.BeginPopup(
						$"character_{character}_contextMenu",
						ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings))
					{
						ImGui.PushID($"character_{character}_contextMenu");
						if (ImGui.MenuItem("Remove"))
						{
							// ??
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

						ImGuiEx.Icon(0xFFFFFFFF, FontAwesomeIcon.Fingerprint, 1.25f);
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
						if (ImGui.InputText($"###Password{character}", ref password, 256, ImGuiInputTextFlags.EnterReturnsTrue))
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

					ImGui.TableNextRow();
				}
			}

			ImGui.EndTable();
		}

		startPos = ImGui.GetCursorPos();
		ImGui.SetCursorPosX(startPos.X + (ImGui.GetContentRegionAvail().X - 25));
		ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
		if (ImGui.Button("+###AddPeerButton", new Vector2(25, 0)))
		{
			Plugin.Instance?.AddPeerWindow.Show();
		}

		ImGui.PopStyleColor();

		ImGui.SetCursorPos(startPos);

		if (ImGui.CollapsingHeader($"Peers ({plugin.CharacterSyncCount()} / {Configuration.Current.Pairs.Count})###PeersSection"))
		{
			if (ImGui.BeginTable("PeersTable", 4))
			{
				ImGui.TableSetupColumn("Hover", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed);

				HashSet<Configuration.Peer> syncedPeers = new();

				// Draw synced peers first
				foreach (Configuration.Peer peer in Configuration.Current.Pairs)
				{
					CharacterSync? sync = null;
					if (peer.CharacterName != null && peer.World != null)
						sync = Plugin.Instance?.GetCharacterSync(peer.CharacterName, peer.World);

					if (sync == null)
						continue;

					syncedPeers.Add(peer);
					List<SyncProgressBase>? progresses = null;
					if (sync != null)
						progresses = plugin.GetSyncProgress(sync);

					this.DrawPeerEntry(peer, sync, progresses);

					ImGui.TableNextRow();
				}

				// Draw unsynced peers last
				foreach (Configuration.Peer peer in Configuration.Current.Pairs)
				{
					if (syncedPeers.Contains(peer))
						continue;

					this.DrawPeerEntry(peer, null, null);

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}

			if (this.peerToRemove != null)
			{
				Configuration.Current.Pairs.Remove(this.peerToRemove);
				Configuration.Current.Save();
			}
		}

		foreach (SyncProviderBase syncProvider in plugin.SyncProviders)
		{
			syncProvider.DrawStatus();
		}
	}

	private void DrawPeerEntry(Configuration.Peer peer, CharacterSync? sync, List<SyncProgressBase>? progresses)
	{
		// Tooltip
		ImGui.TableNextColumn();
		ImGui.Selectable(
			$"##RowSelector{peer}",
			false,
			ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

		if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
			&& ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.OpenPopup($"peer_{peer}_contextMenu");
		}

		if (ImGui.BeginPopup(
			$"peer_{peer}_contextMenu",
			ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings))
		{
			ImGui.PushID($"peer_{peer}_contextMenu");
			if (ImGui.MenuItem("Remove"))
				this.peerToRemove = peer;

			ImGui.PopID();
			ImGui.EndPopup();
		}

		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.SetNextWindowSizeConstraints(new Vector2(256, 0), new Vector2(256, 400));
			ImGui.BeginTooltip();

			ImGui.Text($"{peer.CharacterName} @ {peer.World}");
			ImGui.Separator();

			ImGuiEx.Icon(0xFFFFFFFF, FontAwesomeIcon.Fingerprint, 1.25f);
			ImGui.SameLine();
			ImGui.SetWindowFontScale(0.75f);
			ImGui.TextColoredWrapped(0x80FFFFFF, $"{peer.GetFingerprint()}");
			ImGui.SetWindowFontScale(1.0f);
			ImGui.Separator();

			if (sync != null)
			{
				ImGuiEx.Icon(sync.CurrentStatus.GetIcon());
				ImGui.SameLine();
				ImGui.Text(sync.CurrentStatus.GetMessage());
				ImGui.Separator();
			}

			if (progresses != null)
			{
				if (ImGui.BeginTable("PeerProgressInfoTable", 3))
				{
					ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed);
					ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
					ImGui.TableSetupColumn("Info", ImGuiTableColumnFlags.WidthStretch);
					ImGui.TableNextRow();

					foreach (SyncProgressBase progress in progresses)
					{
						ImGui.TableNextColumn();
						progress.DrawStatus();

						ImGui.TableNextColumn();
						ImGui.Text(progress.Provider.DisplayName);

						ImGui.TableNextColumn();
						progress.DrawInfo();

						ImGui.TableNextRow();
					}

					ImGui.EndTable();
				}

				ImGui.Spacing();
			}

			ImGui.Spacing();

			ImGui.TextDisabled("Right-click for more options");
			ImGui.EndTooltip();
		}

		// Name
		ImGui.TableNextColumn();
		if (sync != null)
		{
			ImGui.Text($"{peer.CharacterName} @ {peer.World}");
		}
		else
		{
			ImGui.TextDisabled($"{peer.CharacterName} @ {peer.World}");
		}

		// Progress
		ImGui.TableNextColumn();
		if (progresses != null)
		{
			long total = 0;
			long current = 0;

			foreach (SyncProgressBase progress in progresses)
			{
				total += progress.Total;
				current += progress.Current;
			}

			float p = (float)current / (float)total;

			if (total <= 0)
				p = 1;

			if (p < 1)
			{
				ImGuiEx.ThinProgressBar(p);
			}
		}

		// Status
		ImGui.TableNextColumn();
		if (sync != null)
		{
			ImGuiEx.Icon(sync.CurrentStatus.GetIcon());
		}
	}
}
