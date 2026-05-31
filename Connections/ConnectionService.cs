// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Connections;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using PeerSync;
using PeerSync.Network;

public partial class ConnectionService : IDisposable
{
	private readonly ConcurrentDictionary<string, CharacterConnection> connectionLookup = new();
	private readonly List<CharacterConnection> connections = new();
	private readonly List<string> alphabeticalIds = new();

	private readonly NetworkManager network = new();

	public ConnectionService()
	{
		this.network = new();
		this.network.IncomingConnected += this.OnIncomingConnectionConnected;
	}

	public int Count { get; private set; } = 0;

	public void BeginListen(int port)
	{
		this.network.BeginListen(port);
	}

	public void Send(PacketTypes type, byte[] data)
	{
		foreach (CharacterConnection connection in this.connections)
		{
			try
			{
				connection.Send(type, data);
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error sending data");
			}
		}
	}

	public void FrameworkUpdate()
	{
		this.Count = 0;

		for (int i = this.connections.Count - 1; i >= 0; i--)
		{
			CharacterConnection.States state = this.connections[i].Update();
			if (state == CharacterConnection.States.TimedOut)
			{
				this.Remove(this.connections[i]);
			}
			else if (this.connections[i].IsConnected)
			{
				this.Count++;
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

	public void Dispose()
	{
		foreach (CharacterConnection connection in this.connections)
		{
			connection.Dispose();
		}

		this.network.IncomingConnected -= this.OnIncomingConnectionConnected;
		this.network.Dispose();
	}

	public async Task<Connection> Connect(IPAddress address, IPAddress? localAddress, int port)
	{
		CancellationTokenSource localCancel = new();
		CancellationTokenSource wideCancel = new();

		wideCancel.CancelAfter(5000);
		localCancel.CancelAfter(5000);

		Connection? connection = null;
		Exception? exception = null;

		Task wideConnectTask = Task.Run(
			async () =>
			{
				try
				{
					Connection? wideConnection = await this.network.Connect(new(address, port), wideCancel.Token);

					if (wideConnection != null)
					{
						wideConnection.Name = "Wide Out";
						connection = wideConnection;
						localCancel.Cancel();
					}
				}
				catch (Exception ex)
				{
					if (ex is not TaskCanceledException && ex is not OperationCanceledException)
					{
						exception = ex;
					}
				}
			},
			wideCancel.Token);

		Task? localConnectTask = null;
		if (localAddress != null)
		{
			localConnectTask = Task.Run(
				async () =>
				{
					try
					{
						Connection? localConnection = await this.network.Connect(new(localAddress, port), localCancel.Token);

						if (localConnection != null)
						{
							localConnection.Name = "Local Out";
							connection = localConnection;
							wideCancel.Cancel();
						}
					}
					catch (Exception ex)
					{
						if (ex is not TaskCanceledException && ex is not OperationCanceledException)
						{
							exception = ex;
						}
					}
				},
				localCancel.Token);
		}

		if (localConnectTask != null)
			await localConnectTask;

		await wideConnectTask;

		if (connection == null && exception != null)
		{
			throw exception;
		}
		else if (connection == null)
		{
			throw new Exception("Failed to establish a connection for an unknown reason");
		}
		else
		{
			return connection;
		}
	}

	public CharacterConnection GetOrCreate(IPlayerCharacter character)
	{
		string id = character.GetId();
		CharacterConnection? connection = null;
		if (this.connectionLookup.TryGetValue(id, out connection) && connection != null)
			return connection;

		CharacterConnection newConnection = new(character);
		this.connections.Add(newConnection);
		this.connectionLookup.TryAdd(id, newConnection);
		this.alphabeticalIds.Add(id);
		this.alphabeticalIds.Sort();
		return newConnection;
	}

	private void Remove(CharacterConnection connection)
	{
		string id = connection.CharacterId;
		connection.Dispose();
		this.connections.Remove(connection);
		this.connectionLookup.TryRemove(id, out _);
		this.alphabeticalIds.Remove(id);
	}

	private void OnIncomingConnectionConnected(Connection connection)
	{
		connection.Name = "In";
		connection.Received += this.OnReceived;
	}

	private void OnReceived(Connection connection, PacketTypes type, byte[] data)
	{
		if (type == PacketTypes.IAm)
		{
			string characterId = Encoding.UTF8.GetString(data);
			CharacterConnection? characterConnection = null;
			if (this.connectionLookup.TryGetValue(characterId, out characterConnection) && characterConnection != null)
			{
				characterConnection.SetIncomingNetworkConnection(connection);
				connection.Received -= this.OnReceived;
			}
			else
			{
				Plugin.Log.Warning($"Unrecognized IAm: {characterId} from {connection.EndPoint}");
			}
		}
	}
}
