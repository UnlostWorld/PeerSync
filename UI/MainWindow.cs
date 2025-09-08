// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

public class MainWindow : Window, IDisposable
{
	private Configuration.Character? editingCharacterPassword = null;

	public MainWindow()
		: base($"Peer Sync - v{Plugin.PluginInterface.Manifest.AssemblyVersion}##PeerSyncMainWindow")
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

		switch (plugin.CurrentStatus)
		{
			case Plugin.Status.Init_OpenPort:
			{
				MainWindow.Icon(FontAwesomeIcon.Hourglass);
				ImGui.SameLine();
				ImGui.Text("Opening Port...");
				break;
			}

			case Plugin.Status.Init_Listen:
			{
				MainWindow.Icon(FontAwesomeIcon.Hourglass);
				ImGui.SameLine();
				ImGui.Text("Creating a listen server...");
				break;
			}

			case Plugin.Status.Init_Character:
			{
				MainWindow.Icon(FontAwesomeIcon.Hourglass);
				ImGui.SameLine();
				ImGui.Text("Waiting for character...");
				break;
			}

			case Plugin.Status.Init_Index:
			{
				MainWindow.Icon(FontAwesomeIcon.Hourglass);
				ImGui.SameLine();
				ImGui.Text("Connecting to Index servers...");
				break;
			}

			case Plugin.Status.Error_NoIndexServer:
			{
				MainWindow.Icon(FontAwesomeIcon.ExclamationTriangle, 0xFF0080FF);
				ImGui.SameLine();
				ImGui.TextColored(0xFF0080FF, "No Index server configured");
				break;
			}

			case Plugin.Status.Error_CantListen:
			{
				MainWindow.Icon(FontAwesomeIcon.ExclamationTriangle, 0xFF0080FF);
				ImGui.SameLine();
				ImGui.TextColored(0xFF0080FF, "Failed to create a listen server");
				break;
			}

			case Plugin.Status.Error_NoPassword:
			{
				MainWindow.Icon(FontAwesomeIcon.ExclamationTriangle, 0xFF0080FF);
				ImGui.SameLine();
				ImGui.TextColored(0xFF0080FF, "No password is set for the current character");
				break;
			}

			case Plugin.Status.Error_NoCharacter:
			{
				MainWindow.Icon(FontAwesomeIcon.ExclamationTriangle, 0xFF0080FF);
				ImGui.SameLine();
				ImGui.TextColored(0xFF0080FF, "Failed to get the current character");
				break;
			}

			case Plugin.Status.Error_Index:
			{
				MainWindow.Icon(FontAwesomeIcon.ExclamationTriangle, 0xFF0080FF);
				ImGui.SameLine();
				ImGui.TextColored(0xFF0080FF, "Failed to communicate with Index servers");
				break;
			}

			case Plugin.Status.Online:
			{
				MainWindow.Icon(FontAwesomeIcon.Wifi);
				ImGui.SameLine();
				ImGui.Text("Online");
				break;
			}

			case Plugin.Status.ShutdownRequested:
			{
				MainWindow.Icon(FontAwesomeIcon.Bed);
				ImGui.SameLine();
				ImGui.Text("Shutting down...");
				break;
			}

			case Plugin.Status.Shutdown:
			{
				MainWindow.Icon(FontAwesomeIcon.Bed);
				ImGui.SameLine();
				ImGui.Text("Shut down");
				break;
			}
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

			string cache = Configuration.Current.CacheDirectory ?? string.Empty;
			if (ImGui.InputText("Cache", ref cache))
			{
				Configuration.Current.CacheDirectory = cache;
				Configuration.Current.Save();
			}
		}

		if (ImGui.CollapsingHeader($"Index Servers ({Configuration.Current.IndexServers.Count})###IndexServersSection"))
		{
			if (ImGui.BeginTable("IndexServersTable", 2))
			{
				ImGui.TableSetupColumn("Url", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableNextRow();

				foreach (string indexServer in Configuration.Current.IndexServers.AsReadOnly())
				{
					// Url
					ImGui.TableNextColumn();
					ImGui.Text(indexServer);

					if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
					{
						ImGui.BeginTooltip();
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

					// Status
					ImGui.TableNextColumn();
					Plugin.IndexServerStatus status = Plugin.IndexServerStatus.None;
					Plugin.Instance?.IndexServersStatus.TryGetValue(indexServer, out status);

					if (status == Plugin.IndexServerStatus.Online)
					{
						MainWindow.Icon(FontAwesomeIcon.Wifi);

						if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
						{
							ImGui.BeginTooltip();
							ImGui.TextDisabled("Connected to index server");
							ImGui.EndTooltip();
						}
					}
					else if (status == Plugin.IndexServerStatus.Offline)
					{
						MainWindow.Icon(FontAwesomeIcon.ExclamationCircle, 0xFF0080FF);

						if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
						{
							ImGui.BeginTooltip();
							ImGui.TextDisabled("Failed to connect to index server");
							ImGui.EndTooltip();
						}
					}

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}

			if (ImGui.Button("Add a new Index Server"))
			{
				ImGui.OpenPopup("AddIndexPopup");
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

			if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
			{
				ImGui.BeginTooltip();
				ImGui.Text("Index servers are used to track the online status of peers.\nYour character name, world, and password are never sent to any index server,\nhowever your character Identifier (which is encrypted by all three), is.\nIt is safe to use any index server you wish, you may also use multiple at the same time.");
				ImGui.EndTooltip();
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
					MainWindow.Icon(FontAwesomeIcon.Fingerprint);

					if (ImGui.IsItemHovered())
					{
						ImGui.BeginTooltip();
						ImGui.Text($"{character.GetIdentifier()}");
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
						if (ImGui.InputText("###Password", ref password, 256, ImGuiInputTextFlags.EnterReturnsTrue))
						{
							character.Password = password;
							character.ClearIdentifier();
							Configuration.Current.Save();
							this.editingCharacterPassword = null;
						}

						ImGui.PopItemWidth();
					}
					else
					{
						ImGui.BeginDisabled();
						ImGui.PushItemWidth(-1);
						ImGui.InputText("###Password", ref password);
						ImGui.PopItemWidth();
						ImGui.EndDisabled();
					}

					if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
					{
						ImGui.BeginTooltip();

						ImGui.Text("The password is used to encrypt your character details. \nOther users can only pair with you if they have your password. \nIt is safe to give your password to people you trust and want to pair with.");

						ImGui.Spacing();

						MainWindow.Icon(FontAwesomeIcon.ExclamationTriangle, 0xFF0080FF);
						ImGui.SameLine();
						ImGui.TextColored(0xFF0080FF, "Changing your password will break all existing pairs!");
						ImGui.TextDisabled("You can change your password in the right-click context menu");

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

		if (ImGui.CollapsingHeader($"Pairs ({Configuration.Current.Pairs.Count})###PairsSection"))
		{
			if (ImGui.BeginTable("PairsTable", 3))
			{
				ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("Finger", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableNextRow();

				foreach (Configuration.Pair pair in Configuration.Current.Pairs)
				{
					// Fingerprint
					ImGui.TableNextColumn();
					MainWindow.Icon(FontAwesomeIcon.Fingerprint);

					if (ImGui.IsItemHovered())
					{
						ImGui.BeginTooltip();
						ImGui.Text($"{pair.GetIdentifier()}");
						ImGui.EndTooltip();
					}

					// Name
					ImGui.TableNextColumn();
					ImGui.Text($"{pair.CharacterName} @ {pair.World}");

					if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
					{
						ImGui.BeginTooltip();
						ImGui.TextDisabled("You can remove pairs in the right-click context menu");
						ImGui.EndTooltip();
					}

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
						{
							Configuration.Current.Pairs.Remove(pair);
							Configuration.Current.Save();
						}

						ImGui.PopID();
						ImGui.EndPopup();
					}

					// Status
					ImGui.TableNextColumn();

					CharacterSync? sync = null;
					if (pair.CharacterName != null && pair.World != null)
						sync = Plugin.Instance?.GetCharacterSync(pair.CharacterName, pair.World);

					if (sync != null)
					{
						switch (sync.CurrentStatus)
						{
							case CharacterSync.Status.None:
							{
								MainWindow.Icon(FontAwesomeIcon.Hourglass);

								if (ImGui.IsItemHovered())
								{
									ImGui.BeginTooltip();
									ImGui.Text("Initializing...");
									ImGui.EndTooltip();
								}

								break;
							}

							case CharacterSync.Status.Listening:
							{
								MainWindow.Icon(FontAwesomeIcon.Hourglass);

								if (ImGui.IsItemHovered())
								{
									ImGui.BeginTooltip();
									ImGui.Text("Listening for connections...");
									ImGui.EndTooltip();
								}
								break;
							}

							case CharacterSync.Status.Searching:
							{
								MainWindow.Icon(FontAwesomeIcon.Search);
								if (ImGui.IsItemHovered())
								{
									ImGui.BeginTooltip();
									ImGui.Text("Searching for peer");
									ImGui.EndTooltip();
								}
								break;
							}

							case CharacterSync.Status.Disconnected:
							case CharacterSync.Status.Offline:
							{
								MainWindow.Icon(FontAwesomeIcon.Bed);
								if (ImGui.IsItemHovered())
								{
									ImGui.BeginTooltip();
									ImGui.Text("Peer is offline");
									ImGui.EndTooltip();
								}
								break;
							}

							case CharacterSync.Status.Connecting:
							case CharacterSync.Status.Handshake:
							{
								MainWindow.Icon(FontAwesomeIcon.Handshake);
								if (ImGui.IsItemHovered())
								{
									ImGui.BeginTooltip();
									ImGui.Text("Connecting...");
									ImGui.EndTooltip();
								}
								break;
							}

							case CharacterSync.Status.Connected:
							{
								MainWindow.Icon(FontAwesomeIcon.Wifi);
								if (ImGui.IsItemHovered())
								{
									ImGui.BeginTooltip();
									ImGui.Text("Connected to peer");
									ImGui.EndTooltip();
								}
								break;
							}

							case CharacterSync.Status.HandshakeFailed:
							case CharacterSync.Status.ConnectionFailed:
							{
								MainWindow.Icon(FontAwesomeIcon.ExclamationTriangle, 0xFF0080FF);

								if (ImGui.IsItemHovered())
								{
									ImGui.BeginTooltip();
									ImGui.Text("Failed to connect to peer");
									ImGui.EndTooltip();
								}

								break;
							}
						}
					}

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}
		}

		foreach (SyncProviderBase syncProvider in plugin.SyncProviders)
		{
			syncProvider.DrawStatus();
		}
	}

	private static void Icon(FontAwesomeIcon icon, uint color = 0xFFFFFFFF)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		ImGui.SetWindowFontScale(0.75f);
		ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3);

		ImGui.SetNextItemWidth(ImGui.GetTextLineHeight());
		ImGui.TextColored(color, icon.ToIconString());

		ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 3);
		ImGui.SetWindowFontScale(1.0f);
		ImGui.PopFont();
	}
}