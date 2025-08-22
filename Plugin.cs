// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioSync;

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
using StudioSync.UI;
using System;
using System.Collections.Generic;
using System.Security.AccessControl;
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
	[PluginService] public static IContextMenu ContextMenu { get; private set; } = null!;

	public static string? LocalCharacterId;
	public static string? CharacterName;
	public static string? World;
	public static string Status = "";

	public readonly WindowSystem WindowSystem = new("StudioSync");
	private MainWindow MainWindow { get; init; }
	private PairWindow PairWindow { get; init; }

	private bool connected = false;
	private static bool shuttingDown = false;
	private readonly Dictionary<string, CharacterSync> checkedCharacters = new();

	public Plugin(IDalamudPluginInterface pluginInterface)
	{
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
	}

	public string Name => "Studio Sync";

	public void Dispose()
	{
		Log.Information("Stopping...");
		Status = "Stopped";
		shuttingDown = true;

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
		SeString pairString = seStringBuilder.AddText(password == null ? "Pair" : "Unpair").Build();

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

		Status = "Connecting to Studio Online...";
		Plugin.Log.Information("Connecting to Studio Online...");

		while (!shuttingDown)
		{
			try
			{
				SyncHeartbeat heartbeat = new();
				heartbeat.Identifier = LocalCharacterId;
				heartbeat.Port = Configuration.Current.Port;
				await heartbeat.Send();

				Status = $"Connected";
				this.connected = true;
				await Task.Delay(10000);
			}
			catch (Exception ex)
			{
				Status = "Failed to connect to Studio Online";
				Plugin.Log.Error(ex, $"Failed to connect to Studio Online");
				return;
			}
		}
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
}
