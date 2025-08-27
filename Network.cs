// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public delegate void ConnectionDelegate(Connection connection);

public class Network : IDisposable
{
	private int listenPort;

	private TcpListener? listener;
	private readonly CancellationTokenSource tokenSource = new();
	private readonly List<Connection> incomingConnections = new();
	private readonly List<Connection> outgoingConnections = new();

	public event ConnectionDelegate? IncomingConnected;
	public event ConnectionDelegate? IncomingDisconnected;
	public event ConnectionDelegate? OutgoingConnected;
	public event ConnectionDelegate? OutgoingDisconnected;

	public void BeginListen(int port)
	{
		this.listenPort = port;
		Task.Run(this.Listen, tokenSource.Token);
	}

	public void Dispose()
	{
		this.tokenSource.Cancel();
		this.tokenSource.Dispose();
		this.listener?.Stop();

		lock (this.incomingConnections)
		{
			foreach (Connection connection in this.incomingConnections)
			{
				connection.Dispose();
			}

			this.incomingConnections.Clear();
		}

		lock (this.outgoingConnections)
		{
			foreach (Connection connection in this.outgoingConnections)
			{
				connection.Dispose();
			}

			this.outgoingConnections.Clear();
		}
	}

	public async Task<Connection?> Connect(IPEndPoint endPoint, CancellationToken token)
	{
		TcpClient client = new();

		try
		{
			await client.ConnectAsync(endPoint, token);
		}
		catch (SocketException ex)
		{
			if (ex.ErrorCode == 10060)
			{
				Plugin.Log.Warning($"Timed out connecting to end point: {endPoint}");
			}
			else
			{
				Plugin.Log.Error(ex, $"Failed to connect to end point: {endPoint}");
			}

			client.Dispose();
			return null;
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, $"Failed to connect to end point: {endPoint}");
			client.Dispose();
			return null;
		}

		Connection connection = new Connection(client);
		this.OnOutgoingConnectionConnected(connection);
		return connection;
	}

	private async Task Listen()
	{
		try
		{
			IPEndPoint ipEndPoint = new(IPAddress.Any, this.listenPort);
			this.listener = new(ipEndPoint);

			listener.Start();

			while (!tokenSource.IsCancellationRequested)
			{
				TcpClient client = await listener.AcceptTcpClientAsync(tokenSource.Token);
				this.OnIncomingConnectionConnected(new(client));
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error listening for connections");
		}
	}

	private void OnIncomingConnectionConnected(Connection connection)
	{
		Plugin.Log.Info($"Connected: {connection.EndPoint}");
		connection.Disconnected += this.OnIncomingConnectionDisconnected;

		lock (this.incomingConnections)
		{
			this.incomingConnections.Add(connection);
		}

		this.IncomingConnected?.Invoke(connection);
	}

	private void OnIncomingConnectionDisconnected(Connection connection)
	{
		Plugin.Log.Info($"Disconnected: {connection.EndPoint}");
		lock (this.incomingConnections)
		{
			this.incomingConnections.Remove(connection);
		}

		this.IncomingDisconnected?.Invoke(connection);
	}

	private void OnOutgoingConnectionConnected(Connection connection)
	{
		Plugin.Log.Info($"Connected: {connection.EndPoint}");
		connection.Disconnected += this.OnOutgoingConnectionDisconnected;

		lock (this.outgoingConnections)
		{
			this.outgoingConnections.Add(connection);
		}

		this.OutgoingConnected?.Invoke(connection);
	}

	private void OnOutgoingConnectionDisconnected(Connection connection)
	{
		Plugin.Log.Info($"Disconnected: {connection.EndPoint}");
		lock (this.outgoingConnections)
		{
			this.outgoingConnections.Remove(connection);
		}

		this.OutgoingDisconnected?.Invoke(connection);
	}
}
