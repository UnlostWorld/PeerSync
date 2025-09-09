// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using Dalamud.Interface;

public static class CharacterSyncExtensions
{
	public static string GetMessage(this CharacterSync.Status self)
	{
		switch (self)
		{
			case CharacterSync.Status.None: return "Initializing...";
			case CharacterSync.Status.Listening: return "Listening for connections ..";
			case CharacterSync.Status.Searching: return "Searching for peer.";
			case CharacterSync.Status.Offline: return "Peer is offline.";
			case CharacterSync.Status.Connecting: return "Connecting...";
			case CharacterSync.Status.ConnectionFailed: return "Failed to connect to peer.";
			case CharacterSync.Status.Handshake: return "Connecting...";
			case CharacterSync.Status.HandshakeFailed: return "Failed to connect to peer.";
			case CharacterSync.Status.Connected: return "Connected to peer.";
			case CharacterSync.Status.Disconnected: return "Peer is offline";
		}

		return string.Empty;
	}

	public static FontAwesomeIcon GetIcon(this CharacterSync.Status self)
	{
		switch (self)
		{
			case CharacterSync.Status.None: return FontAwesomeIcon.Hourglass;
			case CharacterSync.Status.Listening: return FontAwesomeIcon.Hourglass;
			case CharacterSync.Status.Searching: return FontAwesomeIcon.Search;
			case CharacterSync.Status.Offline: return FontAwesomeIcon.Bed;
			case CharacterSync.Status.Connecting: return FontAwesomeIcon.Handshake;
			case CharacterSync.Status.ConnectionFailed: return FontAwesomeIcon.ExclamationTriangle;
			case CharacterSync.Status.Handshake: return FontAwesomeIcon.Handshake;
			case CharacterSync.Status.HandshakeFailed: return FontAwesomeIcon.ExclamationTriangle;
			case CharacterSync.Status.Connected: return FontAwesomeIcon.Wifi;
			case CharacterSync.Status.Disconnected: return FontAwesomeIcon.Bed;
		}

		return FontAwesomeIcon.None;
	}
}