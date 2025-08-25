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
using StudioOnline.Sync;
using PeerSync.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using SharpOpenNat;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using System.IO;
using System.Security.Cryptography;
using NetworkCommsDotNet.DPSBase;
using System.Text;
using Newtonsoft.Json;
using NetworkCommsDotNet.DPSBase.SevenZipLZMACompressor;
using PeerSync.SyncProviders.Glamourer;

public sealed class Plugin : IDalamudPlugin
{
	public readonly FileCache FileCache = new();
	public readonly List<SyncProviderBase> SyncProviders = new()
	{
		new GlamourerSync(),
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
	public string Status = "";

	public readonly WindowSystem WindowSystem = new("StudioSync");
	private MainWindow MainWindow { get; init; }
	private PairWindow PairWindow { get; init; }

	private bool connected = false;
	private bool shuttingDown = false;
	private readonly Dictionary<string, CharacterSync> checkedCharacters = new();
	private readonly Dictionary<string, SyncProviderBase> providerLookup = new();

	public Plugin(IDalamudPluginInterface pluginInterface)
	{
		Instance = this;
		MainWindow = new MainWindow();
		PairWindow = new PairWindow();

		WindowSystem.AddWindow(MainWindow);
		WindowSystem.AddWindow(PairWindow);

		MainWindow.IsOpen = true;

		PluginInterface.UiBuilder.Draw += this.OnDalamudDrawUI;
		PluginInterface.UiBuilder.OpenMainUi += this.OnDalamudOpenMainUi;

		shuttingDown = false;
		Task.Run(this.InitializeAsync);

		ContextMenu.OnMenuOpened += this.OnContextMenuOpened;

		Framework.Update += this.OnFrameworkUpdate;

		foreach (SyncProviderBase provider in this.SyncProviders)
		{
			providerLookup.Add(provider.Key, provider);
		}
	}

	public string Name => "Peer Sync";

	public CharacterSync? GetCharacterSync(string characterName, string world)
	{
		string compoundName = $"{characterName}@{world}";
		if (!this.checkedCharacters.ContainsKey(compoundName))
			return null;

		return this.checkedCharacters[compoundName];
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

	public void Dispose()
	{
		Plugin.Log.Information("Stopping...");
		this.Status = "Stopped";
		this.shuttingDown = true;

		Framework.Update -= this.OnFrameworkUpdate;

		foreach (CharacterSync sync in checkedCharacters.Values)
		{
			sync.Dispose();
		}

		this.checkedCharacters.Clear();

		NetworkComms.Shutdown();
		Instance = null;
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

		SeStringBuilder seStringBuilder = new();
		SeString pairString = seStringBuilder.AddText(password == null ? "Add Peer" : "Remove Peer").Build();

		args.AddMenuItem(new MenuItem()
		{
			Name = pairString,
			OnClicked = (a) => this.TogglePair(character),
			UseDefaultPrefix = false,
			PrefixChar = 'S',
			PrefixColor = 526
		});

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

		if (shuttingDown)
			return;

		if (!FileCache.IsValid())
		{
			Status = $"Invalid cache directory";
			return;
		}

		// Open port
		ushort port = Configuration.Current.Port;

		if (port <= 0)
			port = (ushort)(15550 + Random.Shared.Next(50));

		Plugin.Log.Information($"Opening port {port}");
		Status = $"Opening port {port}";
		using CancellationTokenSource cts = new(10000);
		INatDevice device = await OpenNat.Discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts.Token);
		await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, "Sync port"));
		Plugin.Log.Information($"Opened port {port}");
		Status = $"Opened port {port}";

		// Setup TCP listen
		Status = $"listen for connections...";
		try
		{
			JSONSerializer serializer = new();
			DPSManager.AddDataSerializer(serializer);

			List<DataProcessor> dataProcessors = new()
			{
				DPSManager.GetDataProcessor<LZMACompressor>(),
			};

			Dictionary<string, string> dataProcessorOptions = new();

			NetworkComms.DefaultSendReceiveOptions = new(serializer, dataProcessors, dataProcessorOptions);

			NetworkComms.AppendGlobalConnectionEstablishHandler(this.OnClientEstablished);
			NetworkComms.AppendGlobalConnectionCloseHandler(this.OnClientShutdown);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("iam", this.OnIAmPacket);
			Connection.StartListening(ConnectionType.TCP, new IPEndPoint(IPAddress.Any, port));
			Status = $"Started listening for connections";
			Plugin.Log.Information("Started listening for connections");
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error listening for connections");
			Status = "Error";
			return;
		}

		IPAddress? localIp = null;
		foreach (IPEndPoint localEndPoint in Connection.ExistingLocalListenEndPoints(ConnectionType.TCP))
		{
			if (localEndPoint.AddressFamily != AddressFamily.InterNetwork)
				continue;

			if (IPAddress.IsLoopback(localEndPoint.Address))
				continue;

			localIp = localEndPoint.Address;
			Plugin.Log.Information($"Got Local Address: {localEndPoint.Address}");
		}

		// Get local character id
		try
		{
			await Framework.RunOnUpdate();

			LocalCharacterIdentifier = null;
			Status = "Starting...";
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
					Status = "No password set for this character.";
					Plugin.Log.Information("No password set for this character.");
					return;
				}

				LocalCharacterIdentifier = CharacterSync.GetIdentifier(CharacterName, World, password);
				LocalCharacterData = new(LocalCharacterIdentifier);
			}
		}
		catch (Exception ex)
		{
			Status = "Failed to connect to Peer sync";
			Plugin.Log.Error(ex, $"Failed to connect to Peer sync");
			return;
		}

		if (shuttingDown)
			return;

		Status = "Connecting to server...";

		while (!shuttingDown)
		{
			try
			{
				SyncHeartbeat heartbeat = new();
				heartbeat.Identifier = LocalCharacterIdentifier;
				heartbeat.Port = port;
				heartbeat.LocalAddress = localIp?.ToString();
				await heartbeat.Send();

				await this.UpdateData();

				Status = $"Connected";
				this.connected = true;
				await Task.Delay(5000);
			}
			catch (Exception ex)
			{
				Status = "Failed to connect to server";
				Plugin.Log.Error(ex, $"Failed to connect to server");
				return;
			}
		}
	}

	private void OnIAmPacket(PacketHeader packetHeader, Connection connection, string incomingObject)
	{
		CharacterSync? sync = this.GetCharacterSync(incomingObject);
		if (sync == null)
			return;

		sync.SetConnection(connection);
	}

	private void OnClientEstablished(Connection connection)
	{
		Plugin.Log.Information("Client " + connection.ConnectionInfo + " connected.");
	}

	private void OnClientShutdown(Connection connection)
	{
		Plugin.Log.Information("Client " + connection.ConnectionInfo + " disconnected.");
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
		if (!this.connected)
			return;

		// Update characters, remove missing characters
		HashSet<string> toRemove = new();
		foreach ((string identifier, CharacterSync sync) in this.checkedCharacters)
		{
			bool isValid = sync.Update();
			if (!isValid)
			{
				toRemove.Add(identifier);
				sync.Dispose();
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

				CharacterSync sync = new(characterName, world, password);
				sync.ObjectTableIndex = character.ObjectIndex;
				this.checkedCharacters.Add(compoundName, sync);
			}
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
			string? content = await sync.Serialize(player.ObjectIndex);
			LocalCharacterData.Syncs.Add(sync.Key, content);
		}

		foreach (CharacterSync sync in checkedCharacters.Values)
		{
			sync.SendData(LocalCharacterData);
		}
	}
}

[DataSerializerProcessor(4)]
public class JSONSerializer : DataSerializer
{
	public JSONSerializer()
	{
	}

	protected override void SerialiseDataObjectInt(
		Stream outputStream,
		object objectToSerialise,
		Dictionary<string, string> options)
	{
		if (outputStream == null)
			throw new ArgumentNullException("outputStream");

		if (objectToSerialise == null)
			throw new ArgumentNullException("objectToSerialize");

		outputStream.Seek(0, 0);
		var data = Encoding.Unicode.GetBytes(JsonConvert.SerializeObject(objectToSerialise));
		outputStream.Write(data, 0, data.Length);
		outputStream.Seek(0, 0);
	}

	protected override object? DeserialiseDataObjectInt(
		Stream inputStream,
		Type resultType,
		Dictionary<string, string> options)
	{
		byte[] data = new byte[inputStream.Length];
		inputStream.ReadExactly(data);
		return JsonConvert.DeserializeObject(new String(Encoding.Unicode.GetChars(data)), resultType);
	}
}
