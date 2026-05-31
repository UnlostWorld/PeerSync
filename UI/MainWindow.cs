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
	private bool expandedFriends = false;

	public MainWindow()
		: base($"Peer Sync - v{Plugin.PluginInterface.Manifest.AssemblyVersion}##PeerSyncMainWindow")
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
		Plugin.Characters.DrawStatus();
		Plugin.Connections.DrawStatus();

		bool addFriend;
		ImGuiEx.Header(ref this.expandedFriends, $"Friends", out addFriend);
		if (addFriend)
		{
			Plugin.Ui.AddPeerWindow.Show();
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

		foreach (SyncProviderBase syncProvider in Plugin.Sync.Providers)
		{
			syncProvider.DrawStatus();
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

			ImGui.LabelText("Current Port", Configuration.Current.LastPort.ToString());

			foreach (SyncProviderBase syncProvider in Plugin.Sync.Providers)
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
}
