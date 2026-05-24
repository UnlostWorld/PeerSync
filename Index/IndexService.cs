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
using PeerSync.UI;

public partial class IndexService : IDisposable
{
	public readonly List<IndexServer> Servers = new();
	public readonly List<GroupServer> Groups = new();

	private static readonly TimeSpan IndexDelay = TimeSpan.FromSeconds(10);

	private DateTime lastIndex;
	private bool isUpdatingIndexes = false;
	private bool expandedIndex = false;
	private bool expandedGroups = false;

	public IndexService()
	{
		this.lastIndex = DateTime.MinValue;

		foreach (string address in Configuration.Current.IndexServers)
		{
			this.Servers.Add(new(address));
		}

		foreach (Configuration.Group group in Configuration.Current.Groups)
		{
			this.Groups.Add(new(group));
		}
	}

	private TimeSpan TimeSinceLastIndex => DateTime.Now - this.lastIndex;

	public void Dispose()
	{
		this.Servers.Clear();
		this.Groups.Clear();
	}

	public void FrameworkUpdate()
	{
		if (this.isUpdatingIndexes)
			return;

		if (Plugin.Instance?.LocalCharacter == null)
			return;

		if (this.TimeSinceLastIndex < IndexDelay)
			return;

		if (Plugin.Instance.LocalIpAddress == null)
			return;

		Task.Run(async () => await this.UpdateIndex(Plugin.Instance.LocalPort, Plugin.Instance.LocalIpAddress));
	}

	public void DrawStatus()
	{
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

		bool addIndex;
		ImGuiEx.Header(ref this.expandedIndex, $"Index Servers", out addIndex);
		if (addIndex)
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

				foreach (IndexServer server in this.Servers)
				{
					server.DrawStatus();
					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}
		}

		// Groups
		bool addGroup;
		ImGuiEx.Header(ref this.expandedGroups, $"Groups", out addGroup);
		if (addGroup)
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

			foreach (GroupServer groupServer in this.Groups)
			{
				groupServer.DrawStatus();
				ImGui.TableNextRow();
			}
		}

		ImGui.EndTable();
	}

	private async Task UpdateIndex(ushort port, IPAddress? localIp)
	{
		this.isUpdatingIndexes = true;
		this.lastIndex = DateTime.Now;

		Configuration.Character? localCharacter = Plugin.Instance?.LocalCharacter;
		if (localCharacter == null)
			return;

		try
		{
			foreach (IndexServer indexServer in this.Servers)
			{
				await indexServer.UpdatePeer(localCharacter, localIp, port);
			}

			foreach (GroupServer groupServer in this.Groups)
			{
				await groupServer.UpdatePeer(localCharacter, localIp, port);
			}
		}
		catch (TaskCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, $"Error updating index server status");
			return;
		}
		finally
		{
			this.isUpdatingIndexes = false;
		}
	}
}