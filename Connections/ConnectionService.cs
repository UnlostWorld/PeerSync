// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using PeerSync;

public class ConnectionService
{
	private readonly Dictionary<string, CharacterConnection> connectionLookup = new();
	private readonly List<CharacterConnection> connections = new();

	public void FrameworkUpdate()
	{
		for (int i = this.connections.Count - 1; i >= 0; i--)
		{
			CharacterConnection.States state = this.connections[i].Update();
			if (state == CharacterConnection.States.TimedOut)
			{
				this.Remove(this.connections[i]);
			}
		}

		// Find new characters
		foreach (IGameObject? tObj in Plugin.ObjectTable)
		{
			if (tObj is IPlayerCharacter tCharacter)
			{
				// Is this our local character
				if (tCharacter.ObjectIndex == 0)
					continue;

				this.GetOrCreate(tCharacter);
			}
		}
	}

	public void DrawStatus()
	{
		foreach (CharacterConnection connection in this.connections)
		{
			connection.DrawStatus();
		}
	}

	private CharacterConnection GetOrCreate(IPlayerCharacter character)
	{
		string id = character.GetId();
		CharacterConnection? connection = null;
		if (this.connectionLookup.TryGetValue(id, out connection) && connection != null)
			return connection;

		CharacterConnection newConnection = new(character);
		this.connections.Add(newConnection);
		this.connectionLookup.Add(id, newConnection);
		return newConnection;
	}

	private void Remove(CharacterConnection connection)
	{
		string id = connection.CharacterId;
		connection.Dispose();
		this.connections.Remove(connection);
		this.connectionLookup.Remove(id);
	}
}
