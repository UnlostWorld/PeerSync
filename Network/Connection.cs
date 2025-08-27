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
		try
		{
			await this.stream.WriteAsync((byte[])[objectType], this.tokenSource.Token);
			await this.stream.WriteAsync(BitConverter.GetBytes(data.Length), this.tokenSource.Token);
			await this.stream.WriteAsync(data, this.tokenSource.Token);
		}
		catch (SocketException ex)
		{
			if (ex.ErrorCode == 10053)
			{
				this.Disconnected?.Invoke(this);
				this.Dispose();
			}
			else
			{
				Plugin.Log.Error(ex, "Error sending data");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error sending data");
		}
	}

	private async Task Receive()
	{
		while (!this.tokenSource.IsCancellationRequested)
		{
			try
			{
				byte[] typeBytes = new byte[1];
				await stream.ReadExactlyAsync(typeBytes, 0, 1, this.tokenSource.Token);

				byte[] chunkLengthBytes = new byte[sizeof(int)];
				await stream.ReadExactlyAsync(chunkLengthBytes, 0, sizeof(int), this.tokenSource.Token);

				int chunkLength = BitConverter.ToInt32(chunkLengthBytes);
				Plugin.Log.Information($">> {chunkLength}");

				byte[] data = new byte[chunkLength];

				int bytesToReceive = chunkLength;
				int bytesReceived = 0;
				while (bytesToReceive > 0 && !this.tokenSource.IsCancellationRequested)
				{
					int availableBytes = Math.Min(client.Available, Math.Min(readBufferSize, bytesToReceive));
					await stream.ReadExactlyAsync(data, bytesReceived, availableBytes);

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