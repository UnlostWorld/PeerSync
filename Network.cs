// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System;
using System.Collections.Generic;
using System.IO;
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

public class Connection : IDisposable
{
	private const int readBufferSize = 1024;

	private readonly CancellationTokenSource tokenSource = new();
	private readonly TcpClient client;
	private readonly NetworkStream stream;

	public event ConnectionDelegate? Disconnected;

	public delegate void ObjectDelegate(Connection connection, byte typeId, byte[] data);

	public event ObjectDelegate? Received;

	public Connection(TcpClient client)
	{
		this.client = client;
		this.stream = client.GetStream();

		this.EndPoint = client.Client.RemoteEndPoint;

		Task.Run(this.Receive, this.tokenSource.Token);
	}

	public EndPoint? EndPoint { get; private set; }
	public bool IsConnected => this.client.Connected;

	public void Dispose()
	{
		if (!this.tokenSource.IsCancellationRequested)
			this.tokenSource.Cancel();

		this.tokenSource.Dispose();
		this.stream.Dispose();
		this.client.Dispose();
	}

	public async Task SendAsync(byte objectType, byte[] data)
	{
		await this.stream.WriteAsync((byte[])[objectType], this.tokenSource.Token);
		await this.stream.WriteAsync(BitConverter.GetBytes(data.Length), this.tokenSource.Token);
		await this.stream.WriteAsync(data, this.tokenSource.Token);
	}

	private async Task Receive()
	{
		int read = 0;
		while (!this.tokenSource.IsCancellationRequested)
		{
			try
			{
				byte[] typeBytes = new byte[1];
				read = await stream.ReadAsync(typeBytes, this.tokenSource.Token);
				if (read != 1)
					continue;

				byte[] chunkLengthBytes = new byte[sizeof(int)];
				read = await stream.ReadAsync(chunkLengthBytes, this.tokenSource.Token);
				if (read != sizeof(int))
					continue;

				int packetLength = BitConverter.ToInt32(chunkLengthBytes);
				byte[] data = new byte[packetLength];

				int bytesToReceive = packetLength;
				int bytesReceived = 0;
				while (bytesToReceive > 0 && !this.tokenSource.IsCancellationRequested)
				{
					int availableBytes = Math.Min(readBufferSize, bytesToReceive);
					if (client.Available < availableBytes)
						availableBytes = client.Available;

					read = stream.Read(data, bytesReceived, availableBytes);

					bytesReceived += availableBytes;
					bytesToReceive -= availableBytes;
				}

				try
				{
					Plugin.Log.Information($"Received object: {typeBytes[0]} of length: {data.Length}");
					this.Received?.Invoke(this, typeBytes[0], data);
				}
				catch (Exception ex)
				{
					Plugin.Log.Error(ex, "Error invoking received callbacks");
				}
			}
			catch (IOException)
			{
				this.Dispose();
				this.Disconnected?.Invoke(this);
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, $"error in receiving chunk");
			}
		}
	}
}