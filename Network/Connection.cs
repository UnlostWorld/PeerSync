// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Network;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
					////Plugin.Log.Information($"Received object: {typeBytes[0]} of length: {data.Length}");
					this.Received?.Invoke(this, typeBytes[0], data);
				}
				catch (Exception ex)
				{
					Plugin.Log.Error(ex, "Error invoking received callbacks");
				}
			}
			catch (OperationCanceledException)
			{
				this.Dispose();
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