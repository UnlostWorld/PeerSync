// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

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

public static class Objects
{
	public const byte IAm = 1;
	public const byte CharacterData = 2;
	public const byte FileRequest = 3;
	public const byte FileData = 4;
}

public sealed class Plugin : IDalamudPlugin
{
	public readonly List<SyncProviderBase> SyncProviders = new()
	{
		new GlamourerSync(),
		new PenumbraSync(),
		new CustomizePlusSync(),
		new MoodlesSync(),
		new HonorificSync(),
	};

	[PluginService] public static IPluginLog Log { get; private set; } = null!;
	[PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] public static IDataManager DataManager { get; private set; } = null!;
	[PluginService] public static IClientState ClientState { get; private set; } = null!;
	[PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
	[PluginService] public static IFramework Framework { get; private set; } = null!;
	[PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
	[PluginService] public static IContextMenu ContextMenu { get; private set; } = null!;

	public static Plugin? Instance { get; private set; } = null;

	public CharacterData? LocalCharacterData;
	public string? LocalCharacterIdentifier;
	public string? CharacterName;
	public string? World;
	public Status CurrentStatus;

	public readonly WindowSystem WindowSystem = new("StudioSync");
	private MainWindow MainWindow { get; init; }
	private PairWindow PairWindow { get; init; }

	private bool connected = false;
	private bool shuttingDown = false;
	private readonly Dictionary<string, CharacterSync> checkedCharacters = new();
	private readonly Dictionary<string, SyncProviderBase> providerLookup = new();
	private readonly ConnectionManager network = new();

	public Plugin(IDalamudPluginInterface pluginInterface)
	{
		MainWindow = new MainWindow();
		PairWindow = new PairWindow();

		WindowSystem.AddWindow(MainWindow);
		WindowSystem.AddWindow(PairWindow);

		MainWindow.IsOpen = true;

		this.Start();
	}

	public string Name => "Peer Sync";

	public enum Status
	{
		None,

		Init_OpenPort,
		Init_Listen,
		Init_Character,
		Init_Index,

		Error_NoIndexServer,
		Error_CantListen,
		Error_NoPassword,
		Error_NoCharacter,
		Error_Index,

		Online,

		ShutdownRequested,
		Shutdown,
	}

	public CharacterSync? GetCharacterSync(string characterName, string world)
	{
		string compoundName = $"{characterName}@{world}";
		if (!this.checkedCharacters.ContainsKey(compoundName))
			return null;

		return this.checkedCharacters[compoundName];
	}

	public CharacterSync? GetCharacterSync(Connection connection)
	{
		foreach (CharacterSync sync in this.checkedCharacters.Values)
		{
			if (sync.Connection == connection)
			{
				return sync;
			}
		}

		return null;
	}

	public CharacterSync? GetCharacterSync(string identifier)
	{
		foreach (CharacterSync sync in this.checkedCharacters.Values)
		{
			if (sync.Identifier == identifier)
			{
				return sync;
			}
		}

		return null;
	}

	public SyncProviderBase? GetSyncProvider(string key)
	{
		if (this.providerLookup.TryGetValue(key, out var provider))
			return provider;

		return null;
	}

	public void Start()
	{
		Instance = this;

		Framework.Update += this.OnFrameworkUpdate;
		ContextMenu.OnMenuOpened += this.OnContextMenuOpened;
		PluginInterface.UiBuilder.Draw += this.OnDalamudDrawUI;
		PluginInterface.UiBuilder.OpenMainUi += this.OnDalamudOpenMainUi;

		this.shuttingDown = false;
		Task.Run(this.InitializeAsync);

		foreach (SyncProviderBase provider in this.SyncProviders)
		{
			providerLookup.Add(provider.Key, provider);
		}

		this.network.IncomingConnected += this.OnIncomingConnectionConnected;
	}

	public void Restart()
	{
		Task.Run(this.RestartAsync);
	}

	public async Task RestartAsync()
	{
		this.Dispose();

		while (this.CurrentStatus != Status.Shutdown)
			await Task.Delay(500);

		this.Start();
	}

	public void Dispose()
	{
		Plugin.Log.Information("Stopping...");
		if (this.CurrentStatus != Status.Shutdown)
			this.CurrentStatus = Status.ShutdownRequested;

		this.shuttingDown = true;

		Framework.Update -= this.OnFrameworkUpdate;
		ContextMenu.OnMenuOpened -= this.OnContextMenuOpened;
		PluginInterface.UiBuilder.Draw -= this.OnDalamudDrawUI;
		PluginInterface.UiBuilder.OpenMainUi -= this.OnDalamudOpenMainUi;

		foreach (CharacterSync sync in checkedCharacters.Values)
		{
			sync.Connected -= this.OnCharacterConnected;
			sync.Disconnected -= this.OnCharacterDisconnected;
			sync.Dispose();
		}

		this.checkedCharacters.Clear();

		foreach (SyncProviderBase sync in this.SyncProviders)
		{
			sync.Dispose();
		}

		this.providerLookup.Clear();

		this.network.IncomingConnected -= this.OnIncomingConnectionConnected;
		this.network.Dispose();
		Instance = null;

		Plugin.Log.Information("Stopped");
	}

	private void OnDalamudOpenMainUi()
	{
		MainWindow.Toggle();
	}

	private void OnDalamudDrawUI()
	{
		WindowSystem.Draw();
	}

	private void OnContextMenuOpened(IMenuOpenedArgs args)
	{
		if (args.Target is not MenuTargetDefault target)
			return;

		if (target.TargetObject is not IPlayerCharacter character)
			return;

		string characterName = character.Name.ToString();
		string world = character.HomeWorld.Value.Name.ToString();
		string? password = Configuration.Current.GetPassword(characterName, world);

		args.AddMenuItem(new MenuItem()
		{
			Name = SeStringUtils.ToSeString(password == null ? "Add Peer" : "Remove Peer"),
			OnClicked = (a) => this.TogglePair(character),
			UseDefaultPrefix = false,
			PrefixChar = 'S',
			PrefixColor = 526
		});

		CharacterSync? sync = this.GetCharacterSync(characterName, world);
		if (sync != null)
		{
			args.AddMenuItem(new MenuItem()
			{
				Name = SeStringUtils.ToSeString("Re-apply"),
				OnClicked = (a) => sync.Flush(),
				UseDefaultPrefix = false,
				PrefixChar = 'S',
				PrefixColor = 526
			});
		}
	}

	private void TogglePair(IPlayerCharacter character)
	{
		string characterName = character.Name.ToString();
		string world = character.HomeWorld.Value.Name.ToString();
		PairWindow.Show(characterName, world);
	}

	private async Task InitializeAsync()
	{
		this.connected = false;

		try
		{
			if (shuttingDown)
				return;

			if (Configuration.Current.IndexServers.Count <= 0)
			{
				this.CurrentStatus = Status.Error_NoIndexServer;
				return;
			}

			// Open port
			ushort port = Configuration.Current.Port;

			if (port <= 0)
				port = (ushort)(15400 + Random.Shared.Next(99));

			Plugin.Log.Information($"Opening port {port}");
			this.CurrentStatus = Status.Init_OpenPort;
			using CancellationTokenSource cts = new(10000);
			INatDevice device = await OpenNat.Discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts.Token);
			await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, "Sync port"));
			Plugin.Log.Information($"Opened port {port}");

			// Setup TCP listen
			this.CurrentStatus = Status.Init_Listen;
			try
			{
				this.network.BeginListen(port);
				Plugin.Log.Information($"Started listening for connections on port {port}");
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error listening for connections");
				this.CurrentStatus = Status.Error_CantListen;
				return;
			}

			IPAddress? localIp = null;
			IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (IPAddress ipAddress in host.AddressList)
			{
				if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
					continue;

				if (IPAddress.IsLoopback(ipAddress))
					continue;

				localIp = ipAddress;
				Plugin.Log.Information($"Got Local Address: {ipAddress}");
			}

			// Get local character id
			try
			{
				await Framework.RunOnUpdate();

				LocalCharacterIdentifier = null;
				this.CurrentStatus = Status.Init_Character; ;
				while (string.IsNullOrEmpty(LocalCharacterIdentifier))
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
						this.CurrentStatus = Status.Error_NoPassword;
						Plugin.Log.Information("No password set for this character.");
						return;
					}

					LocalCharacterIdentifier = CharacterSync.GetIdentifier(CharacterName, World, password);
					LocalCharacterData = new(LocalCharacterIdentifier);

					Plugin.Log.Information($"Got local character with Id: {LocalCharacterIdentifier}");
				}
			}
			catch (Exception ex)
			{
				this.CurrentStatus = Status.Error_NoCharacter;
				Plugin.Log.Error(ex, $"Failed to get current character");
				return;
			}

			if (shuttingDown)
				return;

			this.CurrentStatus = Status.Init_Index;
			while (!shuttingDown)
			{
				try
				{
					SyncHeartbeat heartbeat = new();
					heartbeat.Identifier = LocalCharacterIdentifier;
					heartbeat.Port = port;
					heartbeat.LocalAddress = localIp?.ToString();

					foreach (string indexServer in Configuration.Current.IndexServers.ToArray())
					{
						await heartbeat.Send(indexServer);
					}

					await this.UpdateData();

					this.CurrentStatus = Status.Online;
					this.connected = true;
					await Task.Delay(5000);
				}
				catch (Exception ex)
				{
					this.CurrentStatus = Status.Error_Index;
					Plugin.Log.Error(ex, $"Failed to connect to index server");
					return;
				}
			}
		}
		finally
		{
			this.CurrentStatus = Status.Shutdown;
		}
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
		if (!this.connected)
			return;

		// Update characters, remove missing characters
		HashSet<string> toRemove = new();
		foreach ((string identifier, CharacterSync character) in this.checkedCharacters)
		{
			bool isValid = character.Update();
			if (!isValid)
			{
				toRemove.Add(identifier);
				character.Connected -= this.OnCharacterConnected;
				character.Disconnected -= this.OnCharacterDisconnected;
				character.Dispose();
			}
		}

		foreach (string identifier in toRemove)
		{
			this.checkedCharacters.Remove(identifier);
		}

		// Find new characters
		foreach (IBattleChara battleChara in Plugin.ObjectTable.PlayerObjects)
		{
			if (battleChara is IPlayerCharacter character)
			{
				if (character == ClientState.LocalPlayer)
					continue;

				string characterName = character.Name.ToString();
				string world = character.HomeWorld.Value.Name.ToString();
				string compoundName = $"{characterName}@{world}";

				if (this.checkedCharacters.ContainsKey(compoundName))
					continue;

				string? password = Configuration.Current.GetPassword(characterName, world);
				if (password == null)
					continue;

				CharacterSync sync = new(this.network, characterName, world, password);
				sync.ObjectTableIndex = character.ObjectIndex;
				sync.Connected += this.OnCharacterConnected;
				sync.Disconnected += this.OnCharacterDisconnected;
				this.checkedCharacters.Add(compoundName, sync);
			}
		}
	}

	private void OnCharacterConnected(CharacterSync character)
	{
		foreach (SyncProviderBase sync in this.SyncProviders)
		{
			sync.OnCharacterConnected(character);
		}
	}

	private void OnCharacterDisconnected(CharacterSync character)
	{
		foreach (SyncProviderBase sync in this.SyncProviders)
		{
			sync.OnCharacterDisconnected(character);
		}
	}

	private async Task UpdateData()
	{
		if (LocalCharacterData == null)
			return;

		LocalCharacterData.Syncs.Clear();

		await Plugin.Framework.RunOnUpdate();
		if (shuttingDown)
			return;

		IPlayerCharacter? player = ClientState.LocalPlayer;
		if (player == null)
			return;

		foreach (SyncProviderBase sync in this.SyncProviders)
		{
			try
			{
				string? content = await sync.Serialize(player.ObjectIndex);
				LocalCharacterData.Syncs.Add(sync.Key, content);
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error collecting character data");
			}
		}

		foreach (CharacterSync sync in checkedCharacters.Values)
		{
			if (!sync.IsConnected)
				continue;

			try
			{
				sync.SendData(LocalCharacterData);
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error sending character data");
			}
		}
	}

	private void OnIncomingConnectionConnected(Connection connection)
	{
		connection.Received += this.OnReceived;
	}

	private void OnReceived(Connection connection, byte typeId, byte[] data)
	{
		if (typeId == Objects.IAm)
		{
			string identifier = Encoding.UTF8.GetString(data);

			Plugin.Log.Information($"Received IAm: {identifier}");

			CharacterSync? sync = this.GetCharacterSync(identifier);
			if (sync == null)
			{
				Plugin.Log.Warning($"Invalid I am identifier: {identifier}");
				return;
			}

			Task.Run(() => sync.SetConnection(connection));
			connection.Received -= this.OnReceived;
		}
	}
}