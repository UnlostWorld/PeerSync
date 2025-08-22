// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioSync;

using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SharpOpenNat;
using StudioOnline.Sync;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

public sealed class Plugin : IDalamudPlugin
{
	[PluginService] public static IPluginLog Log { get; private set; } = null!;
	[PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] public static IDataManager DataManager { get; private set; } = null!;
	[PluginService] public static IClientState ClientState { get; private set; } = null!;
	[PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
	[PluginService] public static IFramework Framework { get; private set; } = null!;
	[PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;

	private static bool shuttingDown = false;
	private string? localCharacterId;

	public Plugin(IDalamudPluginInterface pluginInterface)
	{
		/*CommandInfo command = new(this.OnCommand);
		command.HelpMessage = "Toggle Studio Sync";
		command.ShowInHelp = true;
		CommandManager.AddHandler("/sync", command);*/

		PluginInterface.UiBuilder.OpenMainUi += this.OnDalamudOpenMainUi;
		PluginInterface.UiBuilder.OpenConfigUi += this.OnDalamudOpenConfigUi;

		shuttingDown = false;
		Task.Run(this.InitializeAsync);

		Framework.Update += this.OnFrameworkUpdate;
	}

	public string Name => "Studio Sync";

	public void Dispose()
	{
		Log.Information("Stopping...");
		shuttingDown = true;

		AddressChange addressChange = new();
		addressChange.Id = localCharacterId;
		Task.Run(addressChange.Send);

		Framework.Update -= this.OnFrameworkUpdate;
	}

	private void OnDalamudOpenMainUi()
	{
	}

	private void OnDalamudOpenConfigUi()
	{
	}

	private async Task InitializeAsync()
	{
		if (shuttingDown)
			return;

		string characterName = string.Empty;
		string world = string.Empty;

		// Get local character id
		try
		{
			await Framework.RunOnUpdate();

			Plugin.Log.Information("Starting...");
			while (string.IsNullOrEmpty(this.localCharacterId))
			{
				await Framework.Delay(500);
				if (shuttingDown)
					return;

				if (!ClientState.IsLoggedIn)
					continue;

				IPlayerCharacter? player = ClientState.LocalPlayer;
				if (player == null)
					continue;

				characterName = player.Name.ToString();
				world = player.HomeWorld.Value.Name.ToString();
				string? password = Configuration.Current.GetPassword(characterName, world);
				if (password == null)
				{
					await Framework.Delay(5000);
					continue;
				}

				this.localCharacterId = CharacterSync.GetSyncId(characterName, world, password);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, $"Failed to connect to studio sync");
			return;
		}

		if (shuttingDown)
			return;

		// Open port
		IPAddress? address;
		int port;
		try
		{
			Plugin.Log.Information($"Beginning NAT discovery...");

			CancellationTokenSource cts = new CancellationTokenSource(10000);
			INatDevice device = await OpenNat.Discoverer.DiscoverDeviceAsync();
			address = await device.GetExternalIPAsync();
			if (address == null)
				throw new Exception("Failed to get external IP address");

			Plugin.Log.Information($"The external IP Address is: {address} ");

			port = Configuration.Current.Port;
			Plugin.Log.Information($"Opening port...");
			await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1600, port, "Studio Sync"));
			Plugin.Log.Information($"Opened Port: {port} ");
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, $"Failed to open port");
			return;
		}


		Plugin.Log.Information("Connecting to Studio Online...");

		AddressChange addressChange = new();
		addressChange.Id = localCharacterId;
		addressChange.Address = address.ToString();
		addressChange.Port = port;
		string message = await addressChange.Send();

		Plugin.Log.Info($"{message} {characterName}@{world} ({localCharacterId})");

		////new CharacterSync("P'dhamyan Cirha", "Sophia", "boobs");
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
	}
}
