// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PeerSync.SyncBlockers;

public sealed partial class Plugin : IDalamudPlugin
{
	public Plugin(IDalamudPluginInterface pluginInterface)
	{
		Plugin.Network = new();
		Plugin.Connections = new();
		Plugin.Index = new();
		Plugin.Characters = new();
		Plugin.Dtr = new();
		Plugin.Sync = new();
		Plugin.Overlays = new();
		Plugin.Ui = new();
		Plugin.Commands = new();
		Plugin.ContextMenu = new();

		Plugin.Framework.Update += this.OnFrameworkUpdate;
	}

	public static Connections.ConnectionService Connections { get; private set; } = null!;
	public static Index.IndexService Index { get; private set; } = null!;
	public static Characters.CharacterService Characters { get; private set; } = null!;
	public static DtrService Dtr { get; private set; } = null!;
	public static SyncProviders.SyncService Sync { get; private set; } = null!;
	public static Overlays.OverlayService Overlays { get; private set; } = null!;
	public static UI.UiService Ui { get; private set; } = null!;
	public static CommandService Commands { get; private set; } = null!;
	public static Characters.ContextMenuService ContextMenu { get; private set; } = null!;
	public static Network.NetworkService Network { get; private set; } = null!;

	[PluginService] public static IPluginLog Log { get; private set; } = null!;
	[PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] public static IDataManager DataManager { get; private set; } = null!;
	[PluginService] public static IClientState ClientState { get; private set; } = null!;
	[PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
	[PluginService] public static IFramework Framework { get; private set; } = null!;
	[PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
	[PluginService] public static IContextMenu XivContextMenu { get; private set; } = null!;
	[PluginService] public static IDtrBar DtrBar { get; private set; } = null!;
	[PluginService] public static ICondition Condition { get; private set; } = null!;
	[PluginService] public static IChatGui ChatGui { get; private set; } = null!;
	[PluginService] public static IGameGui GameGui { get; private set; } = null!;

	public string Name => "Peer Sync";

	public void Dispose()
	{
		Plugin.Network.Dispose();
		Plugin.Connections.Dispose();
		Plugin.Index.Dispose();
		Plugin.Characters.Dispose();
		Plugin.Dtr.Dispose();
		Plugin.Sync.Dispose();
		Plugin.Overlays.Dispose();
		Plugin.Ui.Dispose();
		Plugin.Commands.Dispose();
		Plugin.ContextMenu.Dispose();

		Plugin.Framework.Update -= this.OnFrameworkUpdate;
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
		if (!Plugin.Network.IsInitialized)
			return;

		Plugin.Characters.FrameworkUpdate();
		Plugin.Connections.FrameworkUpdate();
		Plugin.Index.FrameworkUpdate();
		Plugin.Dtr.FrameworkUpdate();
		Plugin.Sync.FrameworkUpdate();
	}
}