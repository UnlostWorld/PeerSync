// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

public class MainWindow : Window, IDisposable
{
	// We give this window a hidden ID using ##.
	// The user will see "My Amazing Window" as window title,
	// but for ImGui the ID is "My Amazing Window##With a hidden ID"
	public MainWindow() : base(
		"Peer Sync##MainWindow",
		ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(250, 350),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};
	}

	public void Dispose() { }

	public override void Draw()
	{
		Plugin? plugin = Plugin.Instance;
		if (plugin == null)
			return;

		if (ImGui.BeginTabBar("##tabs"))
		{
			if (ImGui.BeginTabItem("Status"))
			{
				ImGui.Text(plugin.Status);

				if (plugin.CharacterName != null && plugin.World != null)
				{
					if (ImGui.CollapsingHeader($"{plugin.CharacterName} @ {plugin.World}", ImGuiTreeNodeFlags.Framed))
					{
						string password = Configuration.Current.GetPassword(plugin.CharacterName, plugin.World) ?? string.Empty;
						if (ImGui.InputText("Password", ref password))
						{
							Configuration.Current.SetPassword(plugin.CharacterName, plugin.World, password);
						}
					}
				}

				if (ImGui.CollapsingHeader($"Pairs ({Configuration.Current.Pairs.Count})"))
				{
					if (ImGui.BeginTable("Table", 2))
					{
						ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed);
						ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch);
						////ImGui.TableHeadersRow();
						ImGui.TableNextRow();

						foreach (Configuration.Pair pair in Configuration.Current.Pairs)
						{
							CharacterSync? sync = null;
							if (pair.CharacterName != null && pair.World != null)
								sync = Plugin.Instance?.GetCharacterSync(pair.CharacterName, pair.World);

							ImGui.TableNextColumn();

							if (sync != null)
							{
								ImGui.PushFont(UiBuilder.IconFont);
								ImGui.SetWindowFontScale(0.75f);
								ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3);
								ImGui.Spacing();
								ImGui.SameLine();

								switch (sync.CurrentStatus)
								{
									case CharacterSync.Status.None:
									{
										ImGui.Text(FontAwesomeIcon.Hourglass.ToIconString());
										break;
									}

									case CharacterSync.Status.Listening:
									{
										ImGui.Text(FontAwesomeIcon.Hourglass.ToIconString());
										break;
									}

									case CharacterSync.Status.Searching:
									{
										ImGui.Text(FontAwesomeIcon.Search.ToIconString());
										break;
									}

									case CharacterSync.Status.Disconnected:
									case CharacterSync.Status.Offline:
									{
										ImGui.Text(FontAwesomeIcon.Bed.ToIconString());
										break;
									}

									case CharacterSync.Status.Connecting:
									case CharacterSync.Status.Handshake:
									{
										ImGui.Text(FontAwesomeIcon.Handshake.ToIconString());
										break;
									}

									case CharacterSync.Status.Connected:
									{
										ImGui.Text(FontAwesomeIcon.Wifi.ToIconString());
										break;
									}

									case CharacterSync.Status.HandshakeFailed:
									case CharacterSync.Status.ConnectionFailed:
									{
										ImGui.PushStyleColor(ImGuiCol.Text, 0xFF0080FF);
										ImGui.Text(FontAwesomeIcon.ExclamationTriangle.ToIconString());
										ImGui.PopStyleColor();
										break;
									}
								}

								ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 3);

								ImGui.SetWindowFontScale(1.0f);
								ImGui.PopFont();
							}
							else
							{
								ImGui.Text("        ");
							}


							if (ImGui.IsItemHovered())
							{
								ImGui.BeginTooltip();
								ImGui.Text($"{sync?.CurrentStatus}");
								ImGui.EndTooltip();
							}

							ImGui.TableNextColumn();
							ImGui.Text($"{pair.CharacterName} @ {pair.World}");

							ImGui.TableNextRow();
						}

						ImGui.EndTable();
					}
				}

				foreach (SyncProviderBase syncProvider in plugin.SyncProviders)
				{
					syncProvider.DrawStatus();
				}

				ImGui.EndTabItem();
			}

			if (ImGui.BeginTabItem("Settings"))
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

				int maxUploads = Configuration.Current.MaxConcurrentUploads;
				if (ImGui.InputInt("Simultaneous Uploads", ref maxUploads, 1))
				{
					Configuration.Current.MaxConcurrentUploads = maxUploads;
					Configuration.Current.Save();
				}

				int maxDownloads = Configuration.Current.MaxConcurrentDownloads;
				if (ImGui.InputInt("Simultaneous Downloads", ref maxDownloads, 1))
				{
					Configuration.Current.MaxConcurrentDownloads = maxDownloads;
					Configuration.Current.Save();
				}

				ImGui.EndTabItem();
			}


			ImGui.EndTabBar();
		}
	}
}