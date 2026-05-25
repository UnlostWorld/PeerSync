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
using PeerSync.UI;
using System;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using SharpOpenNat;
using PeerSync.SyncProviders;
using System.Diagnostics;
using PeerSync.SyncBlockers;
using PeerSync.Connections;
using PeerSync.Index;
using PeerSync.Characters;
using PeerSync.Overlays;

public sealed partial class Plugin : IDalamudPlugin
{
	public const int MaxConnectionAttempts = 10;
	public static readonly LightlessCommunicator Lightless = new();

	public IPAddress? LocalIpAddress;
	public ushort LocalPort;

	private readonly CancellationTokenSource tokenSource = new();
	private bool isInitialized = false;

	public Plugin(IDalamudPluginInterface pluginInterface)
	{
		Instance = this;

		Connections = new();
		Index = new();
		Characters = new();
		Dtr = new();
		Sync = new();
		Overlays = new();
		Ui = new();
		Commands = new();
		ContextMenu = new();

		Framework.Update += this.OnFrameworkUpdate;

		this.tokenSource = new();
		Task.Run(this.InitializeAsync, this.tokenSource.Token);
	}

	public static ConnectionService Connections { get; private set; } = null!;
	public static IndexService Index { get; private set; } = null!;
	public static CharacterService Characters { get; private set; } = null!;
	public static DtrService Dtr { get; private set; } = null!;
	public static SyncService Sync { get; private set; } = null!;
	public static OverlayService Overlays { get; private set; } = null!;
	public static UiService Ui { get; private set; } = null!;
	public static CommandService Commands { get; private set; } = null!;
	public static ContextMenuService ContextMenu { get; private set; } = null!;

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

	public static Plugin? Instance { get; private set; } = null;

	public string Name => "Peer Sync";

	public void Dispose()
	{
		this.tokenSource.Cancel();

		Connections.Dispose();
		Index.Dispose();
		Characters.Dispose();
		Dtr.Dispose();
		Sync.Dispose();
		Overlays.Dispose();
		Ui.Dispose();
		Commands.Dispose();
		ContextMenu.Dispose();

		Framework.Update -= this.OnFrameworkUpdate;

		Instance = null;
	}

	private async Task InitializeAsync()
	{
		if (this.tokenSource.IsCancellationRequested)
			return;

		// Open port
		ushort port = 0;
		bool isCustomPort = Configuration.Current.Port != 0;
		int attempts = 0;
		while (!this.tokenSource.IsCancellationRequested && port == 0)
		{
			port = Configuration.Current.Port;
			attempts++;

			if (port <= 0)
				port = Configuration.Current.LastPort;

			if (port <= 0)
				port = (ushort)(15400 + Random.Shared.Next(99));

			try
			{
				OpenNat.TraceSource.Switch.Level = SourceLevels.Off;
				OpenNat.TraceSource.Listeners.Add(new NatTraceListener());

				Plugin.Log.Information($"Opening port {port}");
				using CancellationTokenSource cts = new(10000);
				INatDevice device = await OpenNat.Discoverer.DiscoverDeviceAsync(cts.Token);
				await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, "Sync port"));
				Plugin.Log.Information($"Opened port {port} with {device}");
			}
			catch (NatDeviceNotFoundException)
			{
				if (!isCustomPort)
				{
					// first attempt always fails in debug for some reason, so don't bother
					// logging, and just try again quickly.
					if (attempts == 1)
					{
						await Task.Delay(250, this.tokenSource.Token);
						continue;
					}

					Plugin.Log.Error("Failed to open port, no NAT device found");
					await Task.Delay(5000, this.tokenSource.Token);
					continue;
				}
			}
			catch (Exception ex)
			{
				// If a custom port is set, and we failed to open the port for
				// any reason, just continue on as its likely the user has
				// done th port forwarding themselves.
				if (!isCustomPort)
				{
					Plugin.Log.Error(ex, "Failed to open port");
					port = 0;
					Configuration.Current.LastPort = 0;
					await Task.Delay(5000, this.tokenSource.Token);
					continue;
				}
				else
				{
					Plugin.Log.Warning($"Failed to open custom port: {port}, assuming port forwarding is manual.");
				}
			}
		}

		if (Configuration.Current.LastPort != port)
		{
			Configuration.Current.LastPort = port;
			Configuration.Current.Save();
		}

		// Setup TCP listen
		try
		{
			Connections.BeginListen(port);
			Plugin.Log.Information($"Started listening for connections on port {port}");
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error listening for connections");
			return;
		}

		// Get local IpAddress
		// https://stackoverflow.com/questions/6803073/get-local-ip-address
		IPAddress? localIp = null;
		try
		{
			// Try asking the DNS system for our local IP
			if (localIp == null)
			{
				string hostName = Dns.GetHostName();
				IPHostEntry? host = Dns.GetHostEntry(hostName);

				foreach (IPAddress ipAddress in host.AddressList)
				{
					if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
						continue;

					if (IPAddress.IsLoopback(ipAddress))
						continue;

					localIp = ipAddress;
				}
			}

			// Try opening a UDP socket and getting our IP from it
			if (localIp == null)
			{
				using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
				{
					socket.Connect("8.8.8.8", 65530);
					IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
					localIp = endPoint?.Address;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Warning($"Error getting local IP: {ex.Message}");
		}

		Plugin.Log.Information($"Got Local Address: {localIp}");

		this.LocalIpAddress = localIp;
		this.LocalPort = port;

		this.isInitialized = true;
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
		if (!this.isInitialized)
			return;

		Characters.FrameworkUpdate();
		Connections.FrameworkUpdate();
		Index.FrameworkUpdate();
		Dtr.FrameworkUpdate();
		Sync.FrameworkUpdate();
	}
}

public class NatTraceListener : TraceListener
{
	public override void Write(string? message)
	{
		if (message == null)
			return;

		Plugin.Log.Info(message);
	}

	public override void WriteLine(string? message)
	{
		if (message == null)
			return;

		Plugin.Log.Info(message);
	}
}