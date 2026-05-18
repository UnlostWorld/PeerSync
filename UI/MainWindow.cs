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
using Lumina.Excel.Sheets;
using PeerSync.Online;
using PeerSync.SyncProviders;
using System;
using System.Collections.Generic;
using System.Numerics;

public class MainWindow : Window, IDisposable
{
	private Configuration.Character? editingCharacterPassword = null;

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

		if (ImGuiEx.Header($"Index Servers", true))
		{
			ImGui.OpenPopup("AddIndexPopup");
		}

		Vector2 startPos = ImGui.GetCursorPos();

		if (ImGui.BeginTable("IndexServersTable", 4))
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
				ImGui.TableSetupColumn("Users", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 15);
				ImGui.TableNextRow();

				HashSet<string> servers = new(Configuration.Current.IndexServers);
				foreach (string indexServer in servers)
				{
					ServerStatus? serverStatus = null;
					Plugin.Instance?.IndexServersStatus.TryGetValue(indexServer, out serverStatus);

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
							DialogBox.Show(
							"Confirm",
							$"Are you sure you want to remove the index server\n{indexServer} ?",
							FontAwesomeIcon.ExclamationTriangle,
							0xFF0080FF,
							"Remove",
							"Cancel",
							() =>
							{
								Configuration.Current.IndexServers.Remove(indexServer);
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

						ImGui.TextWrapped($"{indexServer}");
						ImGui.Separator();

						if (serverStatus != null)
						{
							ImGuiEx.Icon(FontAwesomeIcon.Wifi);
							ImGui.SameLine();
							ImGui.TextWrapped(serverStatus.Motd);
						}

						ImGui.TextDisabled("Right-click for more options");
						ImGui.EndTooltip();
					}

					if (serverStatus != null)
					{
						// Url
						ImGui.TableNextColumn();
						ImGui.Text(serverStatus.ServerName);

						// Users
						ImGui.TableNextColumn();
						ImGui.Text($"{serverStatus.OnlineUsers}");

						// Status
						ImGui.TableNextColumn();
						ImGuiEx.Icon(FontAwesomeIcon.Wifi);
					}
					else
					{
						// Url
						ImGui.TableNextColumn();
						string indexServerName = indexServer;
						indexServerName = indexServerName.Replace("http://", string.Empty);
						indexServerName = indexServerName.Replace("https://", string.Empty);
						indexServerName = indexServerName.Replace("www.", string.Empty);
						indexServerName = indexServerName.Replace(".ondigitalocean.app", string.Empty);
						ImGui.Text(indexServerName);
						ImGui.TableNextColumn();
						ImGui.TableNextColumn();
					}

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}
		}

		ImGuiEx.Header($"Characters");

		if (ImGui.BeginTable("CharactersTable", 4))
		{
			ImGui.TableSetupColumn("Hover", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Password", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 15);
			ImGui.TableNextRow();

			foreach (Configuration.Character character in Configuration.Current.Characters.AsReadOnly())
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

					if (plugin.LocalCharacter == character)
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
				if (plugin.LocalCharacter == character)
				{
					ImGuiEx.Icon(FontAwesomeIcon.Wifi);
				}

				ImGui.TableNextRow();
			}
		}

		ImGui.EndTable();

		ImGuiEx.Header($"Connections");
		if (ImGui.BeginTable("SyncTable", 4))
		{
			ImGui.TableSetupColumn("Hover", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 15);

			List<string> syncNames = new();
			Dictionary<string, CharacterSync> syncLookup = new();
			foreach ((string id, CharacterSync sync) in plugin.CharacterSyncs)
			{
				string compoundName = $"{sync.Name} @ {sync.World}";
				if (syncLookup.ContainsKey(compoundName))
					continue;

				syncNames.Add(compoundName);
				syncLookup.Add(compoundName, sync);
			}

			syncNames.Sort();

			foreach (string syncName in syncNames)
			{
				if (!syncLookup.TryGetValue(syncName, out CharacterSync? sync) || sync == null)
					continue;

				List<SyncProgressBase>? progresses = plugin.GetSyncProgress(sync);
				this.DrawSyncEntry(sync, progresses);

				ImGui.TableNextRow();
			}

			ImGui.EndTable();
		}

		if (ImGuiEx.Header($"Groups", true))
		{
			Plugin.Instance?.AddGroupWindow.Show();
		}

		if (ImGui.BeginTable("GroupsTable", 4))
		{
			ImGui.TableSetupColumn("Hover", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Group", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 15);
			ImGui.TableNextRow();

			List<string> groupNames = new();
			Dictionary<string, Configuration.Group> groupLookup = new();
			foreach (Configuration.Group group in Configuration.Current.Groups)
			{
				if (group.Name == null)
					continue;

				if (groupLookup.ContainsKey(group.Name))
					continue;

				groupNames.Add(group.Name);
				groupLookup.Add(group.Name, group);
			}

			groupNames.Sort();

			foreach (string groupName in groupNames)
			{
				if (!groupLookup.TryGetValue(groupName, out Configuration.Group? group) || group == null)
					continue;

				this.DrawGroupEntry(group);

				ImGui.TableNextRow();
			}
		}

		ImGui.EndTable();

		if (ImGuiEx.Header($"Friends", true))
		{
			Plugin.Instance?.AddPeerWindow.Show();
		}

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

	private void DrawGroupEntry(Configuration.Group group)
	{
		if (group.Name == null)
			return;

		string gId = group.GetFingerprint();
		GroupSync? sync = Plugin.Instance?.GetGroupSync(group);

		// Tooltip
		ImGui.TableNextColumn();
		ImGui.Selectable(
			$"##RowSelector{gId}",
			false,
			ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

		if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
			&& ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.OpenPopup($"group_{gId}_contextMenu");
		}

		if (ImGui.BeginPopup(
			$"group_{gId}_contextMenu",
			ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings))
		{
			ImGui.PushID($"group_{gId}_contextMenu");

			if (ImGui.MenuItem("Remove"))
			{
				DialogBox.Show(
					"Confirm",
					$"Are you sure you want to remove the group\n{group.Name}?",
					FontAwesomeIcon.ExclamationTriangle,
					0xFF0080FF,
					"Remove",
					"Cancel",
					() =>
					{
						Configuration.Current.Groups.Remove(group);
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

			ImGui.Text($"{group.Name}");

			ImGui.Separator();

			ImGui.Text("Group:");
			ImGuiEx.Icon(0xFFFFFFFF, FontAwesomeIcon.Fingerprint, 1.15f);
			ImGui.SameLine();
			ImGui.SetWindowFontScale(0.75f);
			ImGui.TextColoredWrapped(0x80FFFFFF, $"{group.GetFingerprint()}");
			ImGui.SetWindowFontScale(1.0f);
			ImGui.Separator();

			if (Plugin.Instance != null && Plugin.Instance.LocalCharacter != null)
			{
				ImGui.Text("You:");
				ImGuiEx.Icon(0xFFFFFFFF, FontAwesomeIcon.Fingerprint, 1.15f);
				ImGui.SameLine();
				ImGui.SetWindowFontScale(0.75f);
				ImGui.TextColoredWrapped(0x80FFFFFF, $"{group.GetMemberFingerprint(Plugin.Instance.LocalCharacter)}");
				ImGui.SetWindowFontScale(1.0f);
				ImGui.Separator();
			}

			ImGui.Spacing();

			ImGui.TextDisabled("Right-click for more options");
			ImGui.EndTooltip();
		}

		// Name
		ImGui.TableNextColumn();
		ImGui.Text($"{group.Name}");

		// Count
		ImGui.TableNextColumn();

		if (sync != null && sync.ServerStatus != null)
		{
			int bestCount = 0;
			foreach ((string indexServer, ServerStatus? status) in sync.ServerStatus)
			{
				if (status?.OnlineUsers > bestCount)
				{
					bestCount = status.OnlineUsers;
				}
			}

			ImGui.Text($"{bestCount}");
		}

		// Status
		ImGui.TableNextColumn();

		if (sync != null && sync.ServerStatus != null)
		{
			ImGuiEx.Icon(FontAwesomeIcon.Wifi);
		}
	}

	private void DrawSyncEntry(CharacterSync sync, List<SyncProgressBase>? progresses)
	{
		string sId = sync.MemberFingerprint;

		// Tooltip
		ImGui.TableNextColumn();
		ImGui.Selectable(
			$"##RowSelector{sId}",
			false,
			ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

		if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
			&& ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.OpenPopup($"peer_{sId}_contextMenu");
		}

		if (ImGui.BeginPopup(
			$"peer_{sId}_contextMenu",
			ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings))
		{
			ImGui.PushID($"peer_{sId}_contextMenu");

			if (ImGui.MenuItem("Inspect"))
			{
				Plugin.Instance?.InspectWindow.Show(sync);
			}

			ImGui.Separator();

			if (Configuration.Current.GetIsBlocked(sync.Name, sync.World))
			{
				if (ImGui.MenuItem("Unblock"))
				{
					Configuration.Current.SetIsBlocked(sync.Name, sync.World, false);
					sync.Reconnect();
				}
			}
			else
			{
				if (ImGui.MenuItem("Block"))
				{
					Configuration.Current.SetIsBlocked(sync.Name, sync.World, true);
					sync.Reconnect();
				}
			}

			if (ImGui.MenuItem("Reconnect"))
			{
				Plugin.Instance?.ClearConnection(sync);
			}

			ImGui.PopID();
			ImGui.EndPopup();
		}

		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.SetNextWindowSizeConstraints(new Vector2(256, 0), new Vector2(256, 400));
			ImGui.BeginTooltip();

			ImGui.Text($"{sync.Name} @ {sync.World}");
			ImGui.Separator();

			ImGuiEx.Icon(0xFFFFFFFF, FontAwesomeIcon.Fingerprint, 1.15f);
			ImGui.SameLine();
			ImGui.SetWindowFontScale(0.75f);
			ImGui.TextColoredWrapped(0x80FFFFFF, $"{sync.MemberFingerprint}");
			ImGui.SetWindowFontScale(1.0f);
			ImGui.Separator();

			ImGuiEx.Icon(sync.CurrentStatus.GetColor(), sync.CurrentStatus.GetIcon());
			ImGui.SameLine();
			ImGui.TextColoredWrapped(sync.CurrentStatus.GetColor(), sync.CurrentStatus.GetMessage());

			if (sync.LastException != null)
			{
				ImGui.SetWindowFontScale(0.75f);
				ImGui.TextColoredWrapped(0xFF0080FF, sync.LastException.Message);
				ImGui.SetWindowFontScale(1.0f);
			}

			if (sync.CurrentStatus == CharacterSync.Status.Connecting ||
				sync.CurrentStatus == CharacterSync.Status.Listening ||
				sync.CurrentStatus == CharacterSync.Status.Searching ||
				sync.CurrentStatus == CharacterSync.Status.Handshake)
			{
				ImGui.SameLine();
				ImGui.Text($"({sync.ConnectionAttempts} / {Plugin.MaxConnectionAttempts})");
			}

			ImGui.Separator();

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
						// This user sent no date for this sync progress
						if (progress.Status == SyncProgressStatus.Empty
						|| progress.Status == SyncProgressStatus.None
						|| progress.Status == SyncProgressStatus.NotApplied)
							continue;

						ImGui.TableNextColumn();
						progress.DrawStatus();

						ImGui.TableNextColumn();
						ImGui.Text(progress.Provider.DisplayName);

						ImGui.TableNextColumn();
						progress.DrawInfo();

						ImGui.TableNextRow();
					}

					foreach (SyncProgressBase progress in progresses)
					{
						// This user sent no date for this sync progress
						if (progress.Status != SyncProgressStatus.NotApplied)
							continue;

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
		ImGui.Text($"{sync.Name} @ {sync.World}");

		// Progress
		ImGui.TableNextColumn();
		if (progresses != null)
		{
			long total = 0;
			long current = 0;

			foreach (SyncProgressBase progress in progresses)
			{
				if (progress.Status == SyncProgressStatus.Syncing)
				{
					progress.Combine(ref current, ref total);
				}
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
