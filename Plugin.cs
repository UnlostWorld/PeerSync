// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PeerSync.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using PeerSync.SyncProviders.Glamourer;
using PeerSync.SyncProviders.Penumbra;
using System.Text;
using System.Threading;
using SharpOpenNat;
using PeerSync.Online;
using PeerSync.Network;
using PeerSync.SyncProviders.CustomizePlus;
using PeerSync.SyncProviders.Moodles;
using PeerSync.SyncProviders.Honorific;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using PeerSync.SyncProviders;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Dalamud.Game.ClientState.Conditions;
using System.Diagnostics;
using Dalamud.Game.Text;
using PeerSync.SyncBlockers;
using PeerSync.Connections;
using Newtonsoft.Json;
using PeerSync.Index;
using PeerSync.Characters;

public sealed partial class Plugin : IDalamudPlugin
{
	public const int MaxConnectionAttempts = 10;
	public static readonly LightlessCommunicator Lightless = new();

	public readonly List<SyncProviderBase> SyncProviders = new();
	public readonly CharacterData LocalCharacterData = new();

	public IPAddress? LocalIpAddress;
	public ushort LocalPort;

	private const long ForceSendDataMs = 10000;
	private readonly string[] commandNames = [
		"/peersync",
		"/pissync",
		"/pissinc",
		"/pisssync",
		"/piercesink"];
	private readonly WindowSystem windowSystem = new("PeerSync");
	private readonly IDtrBarEntry dtrBarEntry;
	private readonly Dictionary<string, SyncProviderBase> providerLookup = new();
	private readonly CancellationTokenSource tokenSource = new();
	private bool isInitialized = false;

	public Plugin(IDalamudPluginInterface pluginInterface)
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

		foreach (string str in this.commandNames)
		{
			CommandManager.AddHandler(str, new CommandInfo(this.OnCommand)
			{
				HelpMessage = "Show the Peer Sync window.",
			});
		}

		this.dtrBarEntry = DtrBar.Get("Peer Sync");
		this.dtrBarEntry.Text = SeStringUtils.ToSeString("\uE0BC");
		this.dtrBarEntry.Tooltip = SeStringUtils.ToSeString($"Peer Sync");
		this.dtrBarEntry.OnClick = this.OnDtrClicked;

		Instance = this;

		Connections = new();
		Index = new();
		Characters = new();

		Framework.Update += this.OnFrameworkUpdate;
		ContextMenu.OnMenuOpened += this.OnContextMenuOpened;
		PluginInterface.UiBuilder.Draw += this.OnDalamudDrawUI;
		PluginInterface.UiBuilder.OpenMainUi += this.OnDalamudOpenMainUi;

		this.tokenSource = new();

		lock (this.SyncProviders)
		{
			this.SyncProviders.Clear();
			this.SyncProviders.Add(new CustomizePlusSync());
			this.SyncProviders.Add(new MoodlesSync());
			this.SyncProviders.Add(new HonorificSync());
			this.SyncProviders.Add(new GlamourerSync());
			this.SyncProviders.Add(new PenumbraSync());
			this.SyncProviders.Add(new PetNamesSync());
			this.SyncProviders.Add(new SimpleHeelsSync());
		}

		Task.Run(this.InitializeAsync, this.tokenSource.Token);

		foreach (SyncProviderBase provider in this.SyncProviders)
		{
			this.providerLookup.Add(provider.Key, provider);
		}
	}

	public static ConnectionService Connections { get; private set; } = null!;
	public static IndexService Index { get; private set; } = null!;
	public static CharacterService Characters { get; private set; } = null!;

	[PluginService] public static IPluginLog Log { get; private set; } = null!;
	[PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] public static IDataManager DataManager { get; private set; } = null!;
	[PluginService] public static IClientState ClientState { get; private set; } = null!;
	[PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
	[PluginService] public static IFramework Framework { get; private set; } = null!;
	[PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
	[PluginService] public static IContextMenu ContextMenu { get; private set; } = null!;
	[PluginService] public static IDtrBar DtrBar { get; private set; } = null!;
	[PluginService] public static ICondition Condition { get; private set; } = null!;
	[PluginService] public static IChatGui ChatGui { get; private set; } = null!;

	public static Plugin? Instance { get; private set; } = null;
	public MainWindow MainWindow { get; init; }
	public AddPeerWindow AddPeerWindow { get; init; }
	public AddGroupWindow AddGroupWindow { get; init; }
	public DialogBoxWindow DialogBox { get; init; }
	public InspectWindow InspectWindow { get; init; }

	public string Name => "Peer Sync";

	public SyncProviderBase? GetSyncProvider(string key)
	{
		if (this.providerLookup.TryGetValue(key, out var provider))
			return provider;

		return null;
	}

	public List<SyncProgressBase> GetSyncProgress(CharacterConnection character)
	{
		List<SyncProgressBase> progresses = new();

		lock (this.SyncProviders)
		{
			foreach (SyncProviderBase sync in this.SyncProviders)
			{
				SyncProgressBase? progress = sync.GetProgress(character);
				if (progress != null)
				{
					progresses.Add(progress);
				}
			}
		}

		return progresses;
	}

	public void Dispose()
	{
		this.tokenSource.Cancel();

		lock (this.SyncProviders)
		{
			foreach (SyncProviderBase sync in this.SyncProviders)
			{
				sync.Dispose();
			}

			this.SyncProviders.Clear();
		}

		this.providerLookup.Clear();

		Connections.Dispose();
		Index.Dispose();
		Characters.Dispose();

		foreach (string str in this.commandNames)
		{
			CommandManager.RemoveHandler(str);
		}

		Framework.Update -= this.OnFrameworkUpdate;
		ContextMenu.OnMenuOpened -= this.OnContextMenuOpened;
		PluginInterface.UiBuilder.Draw -= this.OnDalamudDrawUI;
		PluginInterface.UiBuilder.OpenMainUi -= this.OnDalamudOpenMainUi;

		Instance = null;
	}

	private void OnCommand(string command, string args)
	{
		this.MainWindow.Toggle();
	}

	private void OnDalamudOpenMainUi()
	{
		this.MainWindow.Toggle();
	}

	private void OnDtrClicked(DtrInteractionEvent @evt)
	{
		this.MainWindow.Toggle();
	}

	private void OnDalamudDrawUI()
	{
		this.windowSystem.Draw();
	}

	private void OnContextMenuOpened(IMenuOpenedArgs args)
	{
		if (args.Target is not MenuTargetDefault target)
			return;

		if (target.TargetObject is not IPlayerCharacter character)
			return;

		// TODO
		/*string characterName = character.Name.ToString();
		string world = character.HomeWorld.Value.Name.ToString();
		Configuration.Peer? peer = Configuration.Current.GetFriend(characterName, world);

		if (peer == null)
		{
			args.AddMenuItem(new MenuItem()
			{
				Name = SeStringUtils.ToSeString("Add peer"),
				OnClicked = (a) => this.AddPeer(character),
				UseDefaultPrefix = false,
				PrefixChar = 'S',
				PrefixColor = 526,
			});
		}

		CharacterConnection? sync = this.GetCharacterSync(characterName, world);
		if (sync != null && sync.CurrentStatus == CharacterConnection.Status.Connected)
		{
			args.AddMenuItem(new MenuItem()
			{
				Name = SeStringUtils.ToSeString("Resync with peer"),
				OnClicked = (a) => this.ResyncPeer(sync, character),
				UseDefaultPrefix = false,
				PrefixChar = 'S',
				PrefixColor = 526,
			});
		}*/
	}

	private void AddPeer(IPlayerCharacter character)
	{
		string characterName = character.Name.ToString();
		string world = character.HomeWorld.Value.Name.ToString();
		this.AddPeerWindow.Show(characterName, world);
	}

	private void ResyncPeer(CharacterConnection sync, IPlayerCharacter character)
	{
		sync.Reset();
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

		// Start the main tasks
		Task updateDataTask = Task.Run(this.UpdateData);

		this.isInitialized = true;

		await updateDataTask;
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
		if (!this.isInitialized)
			return;

		Characters.FrameworkUpdate();
		Connections.FrameworkUpdate();
		Index.FrameworkUpdate();
	}

	private async Task UpdateData()
	{
		CharacterData data = new();
		Stopwatch timeSinceLastSendTimer = new();
		timeSinceLastSendTimer.Start();

		while (!this.tokenSource.IsCancellationRequested)
		{
			await Task.Delay(500);

			data.Clear();

			if (Plugin.Characters.Current == null)
				continue;

			await Plugin.Framework.RunOnUpdate();
			if (this.tokenSource.IsCancellationRequested)
				return;

			// Do not sync character if we are in combat is loading
			if (Plugin.Condition[ConditionFlag.InCombat]
				|| Plugin.Condition[ConditionFlag.BetweenAreas]
				|| Plugin.Condition[ConditionFlag.BetweenAreas51]
				|| Plugin.Condition[ConditionFlag.LoggingOut])
			{
				continue;
			}

			IPlayerCharacter? player = ObjectTable.LocalPlayer;
			if (Plugin.Characters.Current == null || player == null)
				continue;

			IGameObject? mountOrMinion = Plugin.ObjectTable[player.ObjectIndex + 1];
			data.Fingerprint = Plugin.Characters.Current.GetFingerprint();

			IGameObject? pet = null;
			unsafe
			{
				BattleChara* pPet = CharacterManager.Instance()->LookupPetByOwnerObject((BattleChara*)player.Address);
				if (pPet != null)
				{
					pet = Plugin.ObjectTable[pPet->ObjectIndex];
				}
			}

			List<SyncProviderBase> providers;
			lock (this.SyncProviders)
			{
				providers = new(this.SyncProviders);
			}

			foreach (SyncProviderBase sync in providers)
			{
				if (this.tokenSource.IsCancellationRequested)
					return;

				try
				{
					string? content = await sync.Serialize(Plugin.Characters.Current, player.ObjectIndex);
					data.Character.Add(sync.Key, content);

					if (mountOrMinion != null)
					{
						content = await sync.Serialize(Plugin.Characters.Current, mountOrMinion.ObjectIndex);
						data.MountOrMinion.Add(sync.Key, content);
					}

					if (pet != null)
					{
						content = await sync.Serialize(Plugin.Characters.Current, pet.ObjectIndex);
						data.Pet.Add(sync.Key, content);
					}
				}
				catch (Exception ex)
				{
					Plugin.Log.Error(ex, "Error collecting character data");
				}
			}

			if (this.LocalCharacterData.IsSame(data) && timeSinceLastSendTimer.ElapsedMilliseconds < ForceSendDataMs)
				continue;

			timeSinceLastSendTimer.Restart();
			data.CopyTo(this.LocalCharacterData);

			string json = JsonConvert.SerializeObject(data);
			byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
			Connections.Send(PacketTypes.CharacterData, jsonBytes);
		}
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