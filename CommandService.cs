// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System;
using System.Collections.Generic;
using Dalamud.Game.Command;

public class CommandService : IDisposable
{
	private readonly HashSet<string> registeredCommands = new();

	public CommandService()
	{
		this.AddCommand("/peersync", "Show the Peer Sync window", Plugin.Ui.MainWindow.Toggle);
		this.AddCommand("/pissync", Plugin.Ui.MainWindow.Toggle);
		this.AddCommand("/pissinc", Plugin.Ui.MainWindow.Toggle);
		this.AddCommand("/pisssync", Plugin.Ui.MainWindow.Toggle);
		this.AddCommand("/piercesink", Plugin.Ui.MainWindow.Toggle);
	}

	public void Dispose()
	{
		foreach (string str in this.registeredCommands)
		{
			Plugin.CommandManager.RemoveHandler(str);
		}
	}

	public void AddCommand(string command, Action callback)
	{
		this.AddCommand(command, null, callback);
	}

	public void AddCommand(string command, string? description, Action callback)
	{
		CommandInfo info = new((s, a) => callback.Invoke());
		info.ShowInHelp = description != null;

		if (description != null)
		{
			info.HelpMessage = description;
		}

		Plugin.CommandManager.AddHandler(command, info);
		this.registeredCommands.Add(command);
	}

	private void OnCommand(string command, string args)
	{
		Plugin.Ui.MainWindow.Toggle();
	}
}