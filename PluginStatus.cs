// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using Dalamud.Interface;

public enum PluginStatus
{
	None,

	Init_OpenPort,
	Init_Listen,
	Init_Character,
	Init_Index,

	Error_NoPort,
	Error_NoIndexServer,
	Error_CantListen,
	Error_NoPassword,
	Error_NoCharacter,
	Error_Index,

	Online,

	ShutdownRequested,
	Shutdown,
}

public static class PluginStatusExtensions
{
	public static FontAwesomeIcon GetIcon(this PluginStatus self)
	{
		switch (self)
		{
			case PluginStatus.Init_OpenPort:
			case PluginStatus.Init_Listen:
			case PluginStatus.Init_Character:
			case PluginStatus.Init_Index: return FontAwesomeIcon.Hourglass;
			case PluginStatus.Error_NoPort:
			case PluginStatus.Error_NoIndexServer:
			case PluginStatus.Error_CantListen:
			case PluginStatus.Error_NoPassword:
			case PluginStatus.Error_NoCharacter:
			case PluginStatus.Error_Index: return FontAwesomeIcon.ExclamationTriangle;
			case PluginStatus.Online: return FontAwesomeIcon.Wifi;
			case PluginStatus.ShutdownRequested: return FontAwesomeIcon.Hourglass;
			case PluginStatus.Shutdown: return FontAwesomeIcon.Bed;
		}

		return FontAwesomeIcon.Question;
	}

	public static string GetMessage(this PluginStatus self)
	{
		switch (self)
		{
			case PluginStatus.Init_OpenPort: return "Opening Port...";
			case PluginStatus.Init_Listen: return "Creating a listen server...";
			case PluginStatus.Init_Character: return "Waiting for character...";
			case PluginStatus.Init_Index: return "Connecting to Index servers...";
			case PluginStatus.Error_NoPort: return "Unable to open port";
			case PluginStatus.Error_NoIndexServer: return "No Index server configured";
			case PluginStatus.Error_CantListen: return "Failed to create a listen server";
			case PluginStatus.Error_NoPassword: return "No password is set for the current character";
			case PluginStatus.Error_NoCharacter: return "Failed to get the current character";
			case PluginStatus.Error_Index: return "Failed to communicate with Index servers";
			case PluginStatus.Online: return "Online";
			case PluginStatus.ShutdownRequested: return "Stopping...";
			case PluginStatus.Shutdown: return "Stopped";
		}

		return string.Empty;
	}
}