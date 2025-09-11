// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
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
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(350, 450),
			MaximumSize = new Vector2(350, float.MaxValue)
		};
	}

	public void Dispose() { }

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

		if (ImGui.CollapsingHeader($"Index Servers ({Configuration.Current.IndexServers.Count})###IndexServersSection"))
		{
			if (ImGui.BeginTable("IndexServersTable", 2))
			{
				ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 20);
				ImGui.TableSetupColumn("Url", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableNextRow();

				foreach (string indexServer in Configuration.Current.IndexServers.AsReadOnly())
				{
					// Status
					ImGui.TableNextColumn();
					Plugin.IndexServerStatus status = Plugin.IndexServerStatus.None;
					Plugin.Instance?.IndexServersStatus.TryGetValue(indexServer, out status);

					if (status == Plugin.IndexServerStatus.Online)
					{
						ImGuiEx.Icon(FontAwesomeIcon.Wifi);

						if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
						{
							ImGui.BeginTooltip();
							ImGui.TextDisabled("Connected to index server");
							ImGui.EndTooltip();
						}
					}
					else if (status == Plugin.IndexServerStatus.Offline)
					{
						ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationCircle);

						if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
						{
							ImGui.BeginTooltip();
							ImGui.TextDisabled("Failed to connect to index server");
							ImGui.EndTooltip();
						}
					}

					// Url
					ImGui.TableNextColumn();
					ImGui.Text(indexServer);

					if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
					{
						ImGui.SetNextWindowSizeConstraints(new Vector2(400, 0), new Vector2(400, 400));
						ImGui.BeginTooltip();
						ImGui.TextWrapped("Index servers are used to track the online status of peers. Your character name, world, and password are never sent to any index server, however your character Fingerprint (which is encrypted by all three), is. It is safe to use any index server you wish, you may also use multiple at the same time.");
						ImGui.TextDisabled("You can remove index servers in the right-click context menu");
						ImGui.EndTooltip();
					}

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

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}
		}

		if (ImGui.CollapsingHeader($"Characters ({Configuration.Current.Characters.Count})###CharactersSection", ImGuiTreeNodeFlags.Framed))
		{
			if (ImGui.BeginTable("CharactersTable", 3))
			{
				ImGui.TableSetupColumn("Finger", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Password", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableNextRow();

				foreach (Configuration.Character character in Configuration.Current.Characters.AsReadOnly())
				{
					ImGui.TableNextColumn();

					// Fingerprint
					ImGuiEx.Icon(FontAwesomeIcon.Fingerprint);

					if (ImGui.IsItemHovered())
					{
						ImGui.BeginTooltip();
						ImGui.Text($"{character.GetFingerprint()}");
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

					if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
					{
						ImGui.SetNextWindowSizeConstraints(new Vector2(400, 0), new Vector2(400, 400));
						ImGui.BeginTooltip();

						ImGui.TextWrapped("The password is used to encrypt your character details. Other users can only pair with you if they have your password. It is safe to give your password to people you trust and want to pair with.");

						ImGui.Spacing();

						ImGuiEx.Icon(0xFF0080FF, FontAwesomeIcon.ExclamationTriangle);
						ImGui.SameLine();
						ImGui.TextColoredWrapped(0xFF0080FF, "Changing your password will break all existing pairs!");
						ImGui.TextColoredWrapped(0x80FFFFFF, "You can change your password in the right-click context menu");

						ImGui.EndTooltip();
					}

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
						if (ImGui.MenuItem("Copy"))
						{
							ImGui.SetClipboardText(password);
						}

						if (ImGui.MenuItem("Edit"))
						{
							this.editingCharacterPassword = character;
						}

						if (ImGui.MenuItem("Randomize"))
						{
							character.GeneratePassword();
							Configuration.Current.Save();
						}

						ImGui.PopID();
						ImGui.EndPopup();
					}

					ImGui.TableNextRow();
				}
			}

			ImGui.EndTable();
		}

		startPos = ImGui.GetCursorPos();
		ImGui.SetCursorPosX(startPos.X + (ImGui.GetContentRegionAvail().X - 25));
		ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
		if (ImGui.Button("+###AddPairButton", new Vector2(25, 0)))
		{
			Plugin.Instance?.PairWindow.Show();
		}

		ImGui.PopStyleColor();

		ImGui.SetCursorPos(startPos);

		if (ImGui.CollapsingHeader($"Pairs ({Configuration.Current.Pairs.Count})###PairsSection"))
		{
			Configuration.Pair? pairToRemove = null;

			if (ImGui.BeginTable("PairsTable", 4))
			{
				ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 24);
				ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Hover", ImGuiTableColumnFlags.WidthFixed);

				foreach (Configuration.Pair pair in Configuration.Current.Pairs)
				{
					CharacterSync? sync = null;
					if (pair.CharacterName != null && pair.World != null)
						sync = Plugin.Instance?.GetCharacterSync(pair.CharacterName, pair.World);

					List<SyncProgressBase>? progresses = null;
					if (sync != null)
						progresses = plugin.GetSyncProgress(sync);

					// Status
					ImGui.TableNextColumn();
					if (sync != null)
					{
						ImGuiEx.Icon(sync.CurrentStatus.GetIcon());

						// Name
						ImGui.TableNextColumn();
						ImGui.Text($"{pair.CharacterName} @ {pair.World}");
					}
					else
					{
						// Name
						ImGui.TableNextColumn();
						ImGui.TextDisabled($"{pair.CharacterName} @ {pair.World}");
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

					ImGui.TableNextColumn();

					// Tooltip
					ImGui.Selectable(
						$"##RowSelector{pair}",
						false,
						ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

					if (ImGui.IsMouseReleased(ImGuiMouseButton.Right)
						&& ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
					{
						ImGui.OpenPopup($"pair_{pair}_contextMenu");
					}

					if (ImGui.BeginPopup(
						$"pair_{pair}_contextMenu",
						ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings))
					{
						ImGui.PushID($"pair_{pair}_contextMenu");
						if (ImGui.MenuItem("Remove"))
							pairToRemove = pair;

						ImGui.PopID();
						ImGui.EndPopup();
					}

					if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
					{
						ImGui.SetNextWindowSizeConstraints(new Vector2(256, 0), new Vector2(256, 400));
						ImGui.BeginTooltip();

						ImGui.Text($"{pair.CharacterName} @ {pair.World}");
						ImGui.Separator();

						ImGuiEx.Icon(0xFFFFFFFF, FontAwesomeIcon.Fingerprint, 1.25f);
						ImGui.SameLine();
						ImGui.SetWindowFontScale(0.75f);
						ImGui.TextColoredWrapped(0x80FFFFFF, $"{pair.GetFingerprint()}");
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
							if (ImGui.BeginTable("PairProgressInfoTable", 3))
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

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}

			if (pairToRemove != null)
			{
				Configuration.Current.Pairs.Remove(pairToRemove);
				Configuration.Current.Save();
			}
		}

		foreach (SyncProviderBase syncProvider in plugin.SyncProviders)
		{
			syncProvider.DrawStatus();
		}
	}
}
