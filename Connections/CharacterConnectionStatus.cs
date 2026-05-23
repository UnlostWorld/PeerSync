// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Connections;

using System;
using Dalamud.Interface;

public enum CharacterConnectionStatus
{
	Initializing,
	Indexing,
	IndexingFailed,
	Connecting,
	HandShaking,
	Connected,
}

public static class CharacterConnectionStatusExtensions
{
	public static FontAwesomeIcon GetIcon(this CharacterConnectionStatus self)
	{
		switch (self)
		{
			case CharacterConnectionStatus.Initializing: return FontAwesomeIcon.Hourglass;
			case CharacterConnectionStatus.Indexing: return FontAwesomeIcon.Search;
			case CharacterConnectionStatus.IndexingFailed: return FontAwesomeIcon.None;
			case CharacterConnectionStatus.Connecting: return FontAwesomeIcon.HandshakeSimple;
			case CharacterConnectionStatus.HandShaking: return FontAwesomeIcon.Handshake;
			case CharacterConnectionStatus.Connected: return FontAwesomeIcon.Wifi;
		}

		throw new Exception("Missing icon");
	}

	public static string GetMessage(this CharacterConnectionStatus self)
	{
		switch (self)
		{
			case CharacterConnectionStatus.Initializing: return "Initializing";
			case CharacterConnectionStatus.Indexing: return "Searching";
			case CharacterConnectionStatus.IndexingFailed: return "Offline";
			case CharacterConnectionStatus.Connecting: return "Connecting...";
			case CharacterConnectionStatus.HandShaking: return "Handshaking...";
			case CharacterConnectionStatus.Connected: return "Connected";
		}

		throw new Exception("Missing message");
	}

	public static uint GetColor(this CharacterConnectionStatus self)
	{
		return 0xFFFFFFFF;
	}
}