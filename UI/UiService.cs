// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.UI;

using System;
using Dalamud.Interface.Windowing;

public class UiService : IDisposable
{
	private readonly WindowSystem windowSystem = new("PeerSync");

	public UiService()
	{
		this.MainWindow = new MainWindow();
		this.AddPeerWindow = new AddPeerWindow();
		this.AddGroupWindow = new AddGroupWindow();
		this.DialogBox = new DialogBoxWindow();
		this.InspectWindow = new InspectWindow();

		this.windowSystem.AddWindow(this.MainWindow);
		this.windowSystem.AddWindow(this.AddPeerWindow);
		this.windowSystem.AddWindow(this.AddGroupWindow);
		this.windowSystem.AddWindow(this.DialogBox);
		this.windowSystem.AddWindow(this.InspectWindow);

#if DEBUG
		this.MainWindow.IsOpen = true;
#endif

		Plugin.PluginInterface.UiBuilder.Draw += this.OnDalamudDrawUI;
		Plugin.PluginInterface.UiBuilder.OpenMainUi += this.OnDalamudOpenMainUi;
	}

	public MainWindow MainWindow { get; init; }
	public AddPeerWindow AddPeerWindow { get; init; }
	public AddGroupWindow AddGroupWindow { get; init; }
	public DialogBoxWindow DialogBox { get; init; }
	public InspectWindow InspectWindow { get; init; }

	public void Dispose()
	{
		Plugin.PluginInterface.UiBuilder.Draw -= this.OnDalamudDrawUI;
		Plugin.PluginInterface.UiBuilder.OpenMainUi -= this.OnDalamudOpenMainUi;
	}

	private void OnDalamudDrawUI()
	{
		Plugin.Overlays.Draw();
		this.windowSystem.Draw();
	}

	private void OnDalamudOpenMainUi()
	{
		this.MainWindow.Toggle();
	}
}