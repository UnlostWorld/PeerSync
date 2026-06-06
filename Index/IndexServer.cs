// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Index;

using System;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using PeerSync.Online;
using PeerSync.UI;

public class IndexServer
{
	public IndexServer(string address)
	{
		this.Address = address;
	}

	public string Address { get; init; }
	public ServerStatus? Status { get; private set; }

	public async Task UpdatePeer(Configuration.Character character, IPAddress? localIp, ushort port)
	{
		SetPeer setPeerRequest = new();
		setPeerRequest.MemberFingerprint = character.GetFingerprint();
		setPeerRequest.Port = port;
		setPeerRequest.LocalAddress = localIp?.ToString();

		try
		{
			bool wasConnected = this.Status != null;
			this.Status = await setPeerRequest.Send(this.Address);

			if (!wasConnected)
			{
				this.OnConnected();
			}
		}
		catch (Exception ex)
		{
			this.Status = null;
			Plugin.Log.Warning(ex, $"Failed to connect to index server: {this.Address}");
		}
	}

	public void DrawStatus()
	{
		// Tooltip
		ImGui.TableNextColumn();
		ImGui.Selectable(
			$"##RowSelector{this.Address}",
			false,
			ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

		if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
			&& ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.OpenPopup($"index_{this.Address}_contextMenu");
		}

		if (ImGui.BeginPopup(
			$"index_{this.Address}_contextMenu",
			ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings))
		{
			ImGui.PushID($"index_{this.Address}_contextMenu");
			if (ImGui.MenuItem("Remove"))
			{
				DialogBox.Show(
				"Confirm",
				$"Are you sure you want to remove the index server\n{this.Address} ?",
				FontAwesomeIcon.ExclamationTriangle,
				0xFF0080FF,
				"Remove",
				"Cancel",
				() =>
				{
					Configuration.Current.IndexServers.Remove(this.Address);
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

			ImGui.TextWrapped($"{this.Address}");
			ImGui.Separator();

			if (this.Status != null)
			{
				ImGuiEx.Icon(FontAwesomeIcon.Wifi);
				ImGui.SameLine();
				ImGui.TextWrapped(this.Status.Motd);
			}

			ImGui.TextDisabled("Right-click for more options");
			ImGui.EndTooltip();
		}

		if (this.Status != null)
		{
			// Url
			ImGui.TableNextColumn();
			ImGui.Text(this.Status.ServerName);

			// Users
			ImGui.TableNextColumn();
			ImGui.Text($"{this.Status.OnlineUsers}");

			// Status
			ImGui.TableNextColumn();
			ImGuiEx.Icon(FontAwesomeIcon.Wifi);
		}
		else
		{
			// Url
			ImGui.TableNextColumn();
			string indexServerName = this.Address;
			indexServerName = indexServerName.Replace("http://", string.Empty);
			indexServerName = indexServerName.Replace("https://", string.Empty);
			indexServerName = indexServerName.Replace("www.", string.Empty);
			indexServerName = indexServerName.Replace(".ondigitalocean.app", string.Empty);
			ImGui.Text(indexServerName);
			ImGui.TableNextColumn();
			ImGui.TableNextColumn();
		}
	}

	private void OnConnected()
	{
		if (this.Status == null)
			return;

		SeStringBuilder str = new();
		str.AddText("\uE0BD");
		str.AddText(" Connected to ");
		str.AddText(this.Status.ServerName ?? string.Empty);

		if (this.Status.Motd != null)
		{
			str.AddText(": ");
			str.AddText(this.Status.Motd);
		}

		XivChatEntry entry = new();
		entry.Type = XivChatType.Debug;
		entry.Message = str.Build();
		Plugin.ChatGui.Print(entry);
	}
}
