﻿// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

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

public sealed partial class Plugin : IDalamudPlugin
{
	public readonly List<SyncProviderBase> SyncProviders = new();
	public readonly Dictionary<string, IndexServerStatus> IndexServersStatus = new();

	public CharacterData? LocalCharacterData;
	public Configuration.Character? LocalCharacter;
	public PluginStatus Status;

	private const string CommandName = "/psync";

	private readonly WindowSystem wWindowSystem = new("PeerSync");
	private readonly IDtrBarEntry dtrBarEntry;
	private readonly Dictionary<string, CharacterSync> checkedCharacters = new();
	private readonly Dictionary<string, SyncProviderBase> providerLookup = new();

	private CancellationTokenSource tokenSource = new();
	private ConnectionManager? network;

	public Plugin(IDalamudPluginInterface pluginInterface)
	{
		this.MainWindow = new MainWindow();
		this.AddPeerWindow = new AddPeerWindow();

		this.wWindowSystem.AddWindow(this.MainWindow);
		this.wWindowSystem.AddWindow(this.AddPeerWindow);

		this.MainWindow.IsOpen = true;

		CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
		{
			HelpMessage = "Show the Peer Sync window with /psync",
		});

		this.dtrBarEntry = DtrBar.Get("Peer Sync");
		this.dtrBarEntry.Text = SeStringUtils.ToSeString("\uE0BC");
		this.dtrBarEntry.Tooltip = SeStringUtils.ToSeString($"Peer Sync - {this.Status.GetMessage()}");
		this.dtrBarEntry.OnClick = this.OnDtrClicked;

		Instance = this;

		Framework.Update += this.OnFrameworkUpdate;
		ContextMenu.OnMenuOpened += this.OnContextMenuOpened;
		PluginInterface.UiBuilder.Draw += this.OnDalamudDrawUI;
		PluginInterface.UiBuilder.OpenMainUi += this.OnDalamudOpenMainUi;

		this.Start();
	}

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

	public static Plugin? Instance { get; private set; } = null;
	public MainWindow MainWindow { get; init; }
	public AddPeerWindow AddPeerWindow { get; init; }

	public string Name => "Peer Sync";

	public int CharacterSyncCount() => this.checkedCharacters.Count;

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

	public CharacterSync? GetCharacterSync(string fingerprint)
	{
		foreach (CharacterSync sync in this.checkedCharacters.Values)
		{
			if (sync.Peer.GetFingerprint() == fingerprint)
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

	public List<SyncProgressBase> GetSyncProgress(CharacterSync character)
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

	public void Stop()
	{
		if (this.Status != PluginStatus.Shutdown)
			this.Status = PluginStatus.ShutdownRequested;

		SeStringBuilder dtrEntryBuilder = new();
		dtrEntryBuilder.AddText($"\uE0BC");
		this.dtrBarEntry.Text = dtrEntryBuilder.Build();

		this.tokenSource.Cancel();

		foreach (CharacterSync sync in this.checkedCharacters.Values)
		{
			sync.Connected -= this.OnCharacterConnected;
			sync.Disconnected -= this.OnCharacterDisconnected;
			sync.Dispose();
		}

		this.checkedCharacters.Clear();

		lock (this.SyncProviders)
		{
			foreach (SyncProviderBase sync in this.SyncProviders)
			{
				sync.Dispose();
			}

			this.SyncProviders.Clear();
		}

		this.providerLookup.Clear();

		if (this.network != null)
		{
			this.network.IncomingConnected -= this.OnIncomingConnectionConnected;
			this.network.Dispose();
			this.network = null;
		}

		this.IndexServersStatus.Clear();

		CommandManager.RemoveHandler(CommandName);
	}

	public void Start()
	{
		this.tokenSource = new();

		lock (this.SyncProviders)
		{
			this.SyncProviders.Add(new CustomizePlusSync());
			this.SyncProviders.Add(new MoodlesSync());
			this.SyncProviders.Add(new HonorificSync());
			this.SyncProviders.Add(new GlamourerSync());
			this.SyncProviders.Add(new PenumbraSync());
		}

		Task.Run(this.InitializeAsync, this.tokenSource.Token);

		foreach (SyncProviderBase provider in this.SyncProviders)
		{
			this.providerLookup.Add(provider.Key, provider);
		}

		this.network = new();
		this.network.IncomingConnected += this.OnIncomingConnectionConnected;
	}

	public void Dispose()
	{
		this.Stop();

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
		this.wWindowSystem.Draw();
	}

	private void OnContextMenuOpened(IMenuOpenedArgs args)
	{
		if (args.Target is not MenuTargetDefault target)
			return;

		if (target.TargetObject is not IPlayerCharacter character)
			return;

		string characterName = character.Name.ToString();
		string world = character.HomeWorld.Value.Name.ToString();
		Configuration.Peer? peer = Configuration.Current.GetPeer(characterName, world);

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

		CharacterSync? sync = this.GetCharacterSync(characterName, world);
		if (sync != null && sync.CurrentStatus == CharacterSync.Status.Connected)
		{
			args.AddMenuItem(new MenuItem()
			{
				Name = SeStringUtils.ToSeString("Resync with peer"),
				OnClicked = (a) => sync.Flush(),
				UseDefaultPrefix = false,
				PrefixChar = 'S',
				PrefixColor = 526,
			});
		}
	}

	private void AddPeer(IPlayerCharacter character)
	{
		string characterName = character.Name.ToString();
		string world = character.HomeWorld.Value.Name.ToString();
		this.AddPeerWindow.Show(characterName, world);
	}

	private async Task InitializeAsync()
	{
		try
		{
			if (this.tokenSource.IsCancellationRequested)
				return;

			while (Configuration.Current.IndexServers.Count <= 0)
			{
				this.Status = PluginStatus.Error_NoIndexServer;
				await Task.Delay(5000);
			}

			// Open port
			ushort port = Configuration.Current.Port;

			if (port <= 0)
				port = (ushort)(15400 + Random.Shared.Next(99));

			Plugin.Log.Information($"Opening port {port}");
			this.Status = PluginStatus.Init_OpenPort;
			using CancellationTokenSource cts = new(10000);
			INatDevice device = await OpenNat.Discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts.Token);
			await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, "Sync port"));
			Plugin.Log.Information($"Opened port {port}");

			// Setup TCP listen
			this.Status = PluginStatus.Init_Listen;
			try
			{
				if (this.network == null)
					throw new Exception("No network");

				this.network.BeginListen(port);
				Plugin.Log.Information($"Started listening for connections on port {port}");
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error listening for connections");
				this.Status = PluginStatus.Error_CantListen;
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

				this.Status = PluginStatus.Init_Character;
				while (this.LocalCharacter == null || string.IsNullOrEmpty(this.LocalCharacter?.Password))
				{
					await Framework.Delay(500);
					if (this.tokenSource.IsCancellationRequested)
						return;

					if (!ClientState.IsLoggedIn)
						continue;

					IPlayerCharacter? player = ClientState.LocalPlayer;
					if (player == null)
						continue;

					string characterName = player.Name.ToString();
					string world = player.HomeWorld.Value.Name.ToString();

					foreach (Configuration.Character character in Configuration.Current.Characters)
					{
						if (character.CharacterName == characterName && character.World == world)
						{
							this.LocalCharacter = character;
							break;
						}
					}

					if (this.LocalCharacter == null)
					{
						this.LocalCharacter = new();
						this.LocalCharacter.CharacterName = characterName;
						this.LocalCharacter.World = world;
						this.LocalCharacter.GeneratePassword();
						Configuration.Current.Characters.Add(this.LocalCharacter);
						Configuration.Current.Save();
					}

					if (string.IsNullOrEmpty(this.LocalCharacter.Password))
					{
						this.Status = PluginStatus.Error_NoPassword;
						continue;
					}

					this.LocalCharacterData = new();

					Plugin.Log.Information($"Got local character: {this.LocalCharacter}");
				}
			}
			catch (Exception ex)
			{
				this.Status = PluginStatus.Error_NoCharacter;
				Plugin.Log.Error(ex, $"Failed to get current character");
				return;
			}

			if (this.tokenSource.IsCancellationRequested)
				return;

			this.Status = PluginStatus.Init_Index;
			while (!this.tokenSource.IsCancellationRequested)
			{
				try
				{
					SyncHeartbeat heartbeat = new();
					heartbeat.Fingerprint = this.LocalCharacter.GetFingerprint();
					heartbeat.Port = port;
					heartbeat.LocalAddress = localIp?.ToString();

					foreach (string indexServer in Configuration.Current.IndexServers.ToArray())
					{
						try
						{
							await heartbeat.Send(indexServer);
							this.IndexServersStatus[indexServer] = IndexServerStatus.Online;
						}
						catch (Exception ex)
						{
							this.IndexServersStatus[indexServer] = IndexServerStatus.Offline;
							Plugin.Log.Warning(ex, $"Failed to connect to index server: {indexServer}");
						}
					}

					await this.UpdateData();

					this.Status = PluginStatus.Online;
					await Task.Delay(5000, this.tokenSource.Token);
				}
				catch (TaskCanceledException)
				{
				}
				catch (Exception ex)
				{
					this.Status = PluginStatus.Error_Index;
					Plugin.Log.Error(ex, $"Error in update");
					return;
				}
			}
		}
		finally
		{
			this.Status = PluginStatus.Shutdown;
		}
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
		this.dtrBarEntry.Tooltip = SeStringUtils.ToSeString($"Peer Sync - {this.Status.GetMessage()}");

		if (this.Status != PluginStatus.Online)
			return;

		// Update characters, remove missing characters
		HashSet<string> toRemove = new();
		SeStringBuilder dtrTooltipBuilder = new();
		dtrTooltipBuilder.AddText($"Peer Sync - {this.Status.GetMessage()}");

		int connectedCount = 0;
		foreach ((string fingerprint, CharacterSync character) in this.checkedCharacters)
		{
			bool isValid = character.Update();
			if (!isValid)
			{
				toRemove.Add(fingerprint);
				character.Connected -= this.OnCharacterConnected;
				character.Disconnected -= this.OnCharacterDisconnected;
				character.Dispose();
			}
			else
			{
				if (character.IsConnected)
				{
					dtrTooltipBuilder.AddText($"\n・{character.Peer.CharacterName} @ {character.Peer.World}");
					connectedCount++;
				}
			}
		}

		foreach (string fingerprint in toRemove)
		{
			this.checkedCharacters.Remove(fingerprint);
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

				Configuration.Peer? peer = Configuration.Current.GetPeer(characterName, world);
				if (peer == null)
					continue;

				if (this.network == null)
					continue;

				CharacterSync sync = new(this.network, peer, character.ObjectIndex);
				sync.Connected += this.OnCharacterConnected;
				sync.Disconnected += this.OnCharacterDisconnected;
				this.checkedCharacters.Add(compoundName, sync);
			}
		}

		SeStringBuilder dtrEntryBuilder = new();
		dtrEntryBuilder.AddText($"\uE0BD");

		if (connectedCount > 0)
			dtrEntryBuilder.AddText($"{connectedCount}");

		lock (this.SyncProviders)
		{
			foreach (SyncProviderBase sync in this.SyncProviders)
			{
				sync.GetDtrStatus(ref dtrEntryBuilder, ref dtrTooltipBuilder);
			}
		}

		this.dtrBarEntry.Text = dtrEntryBuilder.ToString();
		this.dtrBarEntry.Tooltip = dtrTooltipBuilder.ToString();
	}

	private void OnCharacterConnected(CharacterSync character)
	{
		lock (this.SyncProviders)
		{
			foreach (SyncProviderBase sync in this.SyncProviders)
			{
				sync.OnCharacterConnected(character);
			}
		}
	}

	private void OnCharacterDisconnected(CharacterSync character)
	{
		lock (this.SyncProviders)
		{
			foreach (SyncProviderBase sync in this.SyncProviders)
			{
				sync.OnCharacterDisconnected(character);
			}
		}
	}

	private async Task UpdateData()
	{
		if (this.LocalCharacterData == null)
			return;

		this.LocalCharacterData.Syncs.Clear();
		this.LocalCharacterData.MountOrMinionSyncs.Clear();

		await Plugin.Framework.RunOnUpdate();
		if (this.tokenSource.IsCancellationRequested)
			return;

		IPlayerCharacter? player = ClientState.LocalPlayer;
		if (this.LocalCharacter == null || player == null)
			return;

		IGameObject? mountOrMinion = Plugin.ObjectTable[player.ObjectIndex + 1];
		this.LocalCharacterData.Fingerprint = this.LocalCharacter.GetFingerprint();

		foreach (SyncProviderBase sync in this.SyncProviders.AsReadOnly())
		{
			if (this.tokenSource.IsCancellationRequested)
				return;

			try
			{
				string? content = await sync.Serialize(player.ObjectIndex);
				this.LocalCharacterData.Syncs.Add(sync.Key, content);

				if (mountOrMinion != null)
				{
					content = await sync.Serialize(mountOrMinion.ObjectIndex);
					this.LocalCharacterData.MountOrMinionSyncs.Add(sync.Key, content);
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error collecting character data");
			}
		}

		foreach (CharacterSync sync in this.checkedCharacters.Values)
		{
			if (!sync.IsConnected)
				continue;

			try
			{
				sync.SendData(this.LocalCharacterData);
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

	private void OnReceived(Connection connection, PacketTypes type, byte[] data)
	{
		if (type == PacketTypes.IAm)
		{
			string fingerprint = Encoding.UTF8.GetString(data);

			Plugin.Log.Information($"Received IAm: {fingerprint}");

			CharacterSync? sync = this.GetCharacterSync(fingerprint);
			if (sync == null)
			{
				Plugin.Log.Warning($"Invalid I am fingerprint: {fingerprint}");
				return;
			}

			if (sync.SetConnection(connection))
			{
				connection.Received -= this.OnReceived;
			}
		}
	}
}