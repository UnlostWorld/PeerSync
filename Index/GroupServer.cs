// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Index;

using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using PeerSync.Online;
using PeerSync.UI;

public class GroupServer
{
	private readonly Configuration.Group groupConfiguration;
	private readonly HashSet<string> memberFingerprints = new();

	public GroupServer(Configuration.Group group)
	{
		this.groupConfiguration = group;
	}

	public async Task UpdatePeer(Configuration.Character character, IPAddress? localIp, ushort port)
	{
		SetPeer setGroupPeerRequest = new();
		setGroupPeerRequest.GroupFingerprint = this.groupConfiguration.GetFingerprint();
		setGroupPeerRequest.MemberFingerprint = this.GetMemberFingerprint(character);
		setGroupPeerRequest.Port = port;
		setGroupPeerRequest.LocalAddress = localIp?.ToString();

		GetMembers getGroupMembersRequest = new();
		getGroupMembersRequest.GroupFingerprint = this.groupConfiguration.GetFingerprint();

		this.memberFingerprints.Clear();

		foreach (string indexServer in Configuration.Current.IndexServers)
		{
			try
			{
				await setGroupPeerRequest.Send(indexServer);

				GetMembers? response = await getGroupMembersRequest.Send(indexServer);
				if (response != null && response.Members != null)
				{
					foreach (string memberFingerprint in response.Members)
					{
						this.memberFingerprints.Add(memberFingerprint);
					}
				}
			}
			catch (Exception)
			{
				continue;
			}
		}
	}

	public string GetFingerprint()
	{
		return this.groupConfiguration.GetFingerprint();
	}

	public string? GetMemberFingerprint(Configuration.Character character)
	{
		if (character.CharacterName == null || character.World == null)
			return null;

		return this.GetMemberFingerprint(character.CharacterName, character.World);
	}

	public string GetMemberFingerprint(string characterName, string characterWorld)
	{
		return this.groupConfiguration.GetMemberFingerprint(characterName, characterWorld);
	}

	public bool IsMember(string fingerprint)
	{
		return this.memberFingerprints.Contains(fingerprint);
	}

	public void DrawStatus()
	{
		if (this.groupConfiguration.Name == null)
			return;

		string gId = this.GetFingerprint();

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
					$"Are you sure you want to remove the group\n{this.groupConfiguration.Name}?",
					FontAwesomeIcon.ExclamationTriangle,
					0xFF0080FF,
					"Remove",
					"Cancel",
					() =>
					{
						Configuration.Current.Groups.Remove(this.groupConfiguration);
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

			ImGui.Text($"{this.groupConfiguration.Name}");

			ImGui.Separator();

			ImGui.Text("Group:");
			ImGuiEx.Icon(0xFFFFFFFF, FontAwesomeIcon.Fingerprint, 1.15f);
			ImGui.SameLine();
			ImGui.SetWindowFontScale(0.75f);
			ImGui.TextColoredWrapped(0x80FFFFFF, $"{this.GetFingerprint()}");
			ImGui.SetWindowFontScale(1.0f);
			ImGui.Separator();

			if (Plugin.Instance != null && Plugin.Instance.LocalCharacter != null)
			{
				ImGui.Text("You:");
				ImGuiEx.Icon(0xFFFFFFFF, FontAwesomeIcon.Fingerprint, 1.15f);
				ImGui.SameLine();
				ImGui.SetWindowFontScale(0.75f);
				ImGui.TextColoredWrapped(0x80FFFFFF, $"{this.GetMemberFingerprint(Plugin.Instance.LocalCharacter)}");
				ImGui.SetWindowFontScale(1.0f);
				ImGui.Separator();
			}

			ImGui.Spacing();

			ImGui.TextDisabled("Right-click for more options");
			ImGui.EndTooltip();
		}

		// Name
		ImGui.TableNextColumn();
		ImGui.Text($"{this.groupConfiguration.Name}");

		// Count
		ImGui.TableNextColumn();
		ImGui.Text($"{this.memberFingerprints.Count}");

		// Status
		ImGui.TableNextColumn();
		if (this.memberFingerprints.Count > 0)
		{
			ImGuiEx.Icon(FontAwesomeIcon.Wifi);
		}
	}
}