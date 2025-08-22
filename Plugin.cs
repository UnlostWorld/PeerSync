// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioSync;

using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SharpOpenNat;
using StudioOnline.Sync;
using StudioSync.UI;
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

	public static string? LocalCharacterId;
	public static string? CharacterName;
	public static string? World;
	public static string Status = "";

	public readonly WindowSystem WindowSystem = new("StudioSync");
	private MainWindow MainWindow { get; init; }

	private static bool shuttingDown = false;

	public Plugin(IDalamudPluginInterface pluginInterface)
	{
		MainWindow = new MainWindow();
		WindowSystem.AddWindow(MainWindow);

		MainWindow.IsOpen = true;

		PluginInterface.UiBuilder.Draw += this.OnDalamudDrawUI;
		PluginInterface.UiBuilder.OpenMainUi += this.OnDalamudOpenMainUi;

		shuttingDown = false;
		Task.Run(this.InitializeAsync);

		Framework.Update += this.OnFrameworkUpdate;
	}

	public string Name => "Studio Sync";

	public void Dispose()
	{
		Log.Information("Stopping...");
		Status = "Stopped";
		shuttingDown = true;

		AddressChange addressChange = new();
		addressChange.Id = LocalCharacterId;
		Task.Run(addressChange.Send);

		Framework.Update -= this.OnFrameworkUpdate;
	}

	private void OnDalamudOpenMainUi()
	{
		MainWindow.Toggle();
	}

	private void OnDalamudDrawUI()
	{
		WindowSystem.Draw();
	}

	private async Task InitializeAsync()
	{
		if (shuttingDown)
			return;

		// Get local character id
		try
		{
			await Framework.RunOnUpdate();

			LocalCharacterId = null;
			Plugin.Log.Information("Starting...");
			Status = "Starting...";
			while (string.IsNullOrEmpty(LocalCharacterId))
			{
				await Framework.Delay(500);
				if (shuttingDown)
					return;

				if (!ClientState.IsLoggedIn)
					continue;

				IPlayerCharacter? player = ClientState.LocalPlayer;
				if (player == null)
					continue;

				CharacterName = player.Name.ToString();
				World = player.HomeWorld.Value.Name.ToString();
				string? password = Configuration.Current.GetPassword(CharacterName, World);
				if (password == null)
				{
					Status = "No password set for this character.";
					Plugin.Log.Information("No password set for this character.");
					return;
				}

				LocalCharacterId = CharacterSync.GetSyncId(CharacterName, World, password);
			}
		}
		catch (Exception ex)
		{
			Status = "Failed to connect to studio sync";
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
			Status = "NAT discovery...";
			Plugin.Log.Information($"NAT discovery...");

			CancellationTokenSource cts = new CancellationTokenSource(5000);
			INatDevice device = await OpenNat.Discoverer.DiscoverDeviceAsync();
			address = await device.GetExternalIPAsync();
			if (address == null)
				throw new Exception("Failed to get external IP address");

			Plugin.Log.Information($"The external IP Address is: {address} ");

			port = Configuration.Current.Port;
			Status = "Opening port...";
			Plugin.Log.Information($"Opening port...");
			await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1600, port, "Studio Sync"));
			Plugin.Log.Information($"Opened Port: {port} ");
		}
		catch (Exception ex)
		{
			Status = "Failed NAT discorvery";
			Plugin.Log.Error(ex, $"Failed NAT discorvery");
			return;
		}

		Status = "Connecting to Studio Online...";
		Plugin.Log.Information("Connecting to Studio Online...");

		try
		{
			AddressChange addressChange = new();
			addressChange.Id = LocalCharacterId;
			addressChange.Address = address.ToString();
			addressChange.Port = port;
			string message = await addressChange.Send();

			Status = $"Connected: {message}";
			Plugin.Log.Info($"{message} {CharacterName}@{World} ({LocalCharacterId})");
		}
		catch (Exception ex)
		{
			Status = "Failed to connect to Studio Online";
			Plugin.Log.Error(ex, $"Failed to connect to Studio Online");
			return;
		}
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
	}
}
