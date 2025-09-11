// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PeerSync.Network;

namespace PeerSync.SyncProviders.Penumbra;

public class FileDownload : FileTransfer
{
	public long BytesToReceive = 0;
	public long BytesReceived = 0;

	private FileStream? fileStream;
	private byte queueIndex;
	private Exception? recieveError;

	public FileDownload(
		PenumbraSync sync,
		string name,
		string hash,
		long expectedSize,
		CharacterSync character,
		CancellationToken token)
		: base(sync, hash, character, token)
	{
		this.Name = name;
		this.BytesToReceive = expectedSize;

		sync.GetProgress(this.Character)?.AddTotalDownload(expectedSize);
	}

	public override float Progress => (float)this.BytesReceived / (float)this.BytesToReceive;

	public override void Dispose()
	{
		this.fileStream?.Dispose();

		if (this.Character.Connection != null)
		{
			this.Character.Connection.Received -= this.OnReceived;
		}
	}

	protected override async Task Transfer()
	{
		// Simulate
		if (this.Character.Peer.IsTestPeer)
		{
			this.Name += " (fake)";
			while (this.BytesReceived < this.BytesToReceive)
			{
				PenumbraProgress? prog = sync.GetProgress(this.Character);
				if (prog == null)
					return;

				long chunk = 1024 * 32;

				if (this.BytesReceived + chunk >= this.BytesToReceive)
					chunk = this.BytesToReceive - this.BytesReceived;

				this.BytesReceived += chunk;
				prog.AddCurrentDownload(chunk);

				await Task.Delay(100);
			}

			return;
		}


		FileInfo? file = sync.fileCache.GetFile(hash);

		if (file == null)
			return;

		if (this.cancellationToken.IsCancellationRequested
			|| this.Character.Connection == null)
			return;

		this.queueIndex = this.sync.lastQueueIndex++;
		this.Character.Connection.Received += this.OnReceived;

		byte[] hashBytes = Encoding.UTF8.GetBytes(hash);
		byte[] objectBytes = new byte[hashBytes.Length + 1];
		objectBytes[0] = this.queueIndex;
		Array.Copy(hashBytes, 0, objectBytes, 1, hashBytes.Length);

		this.Character.Send(Objects.FileRequest, objectBytes);

		bool gotAllData = false;
		while (!gotAllData && !this.cancellationToken.IsCancellationRequested)
		{
			lock (this)
			{
				gotAllData = this.BytesReceived >= this.BytesToReceive;
			}

			if (this.recieveError != null)
				throw this.recieveError;

			await Task.Delay(10, this.cancellationToken);
		}

		if (this.fileStream != null)
		{
			lock (this.fileStream)
			{
				this.fileStream.Flush();
				this.fileStream.Dispose();
				this.fileStream = null;
			}
		}

		if (this.cancellationToken.IsCancellationRequested)
		{
			file.Delete();
			return;
		}

		// hash verify
		this.sync.fileCache.GetFileHash(file.FullName, out string gotHash, out long fileSize);
		if (gotHash != hash)
		{
			Plugin.Log.Warning($"File failed to pass validation. Expected: {hash}, got {gotHash}");
			file.Delete();
			this.BytesReceived = 0;
			this.Retry();
			return;
		}
	}

	private void OnReceived(Connection connection, byte typeId, byte[] data)
	{
		if (typeId == Objects.FileData)
		{
			byte clientQueueIndex = data[0];

			if (clientQueueIndex != this.queueIndex)
				return;

			if (connection != this.Character.Connection)
				return;

			byte[] fileData = new byte[data.Length - 1];
			Array.Copy(data, 1, fileData, 0, data.Length - 1);
			this.OnFileData(fileData);
		}
	}

	private void OnFileData(byte[] data)
	{
		if (this.cancellationToken.IsCancellationRequested)
			return;

		try
		{
			if (data.Length > 1)
			{
				if (this.fileStream == null)
				{
					FileInfo? file = sync.fileCache.GetFile(hash);
					this.fileStream = new(file.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.None);
				}

				lock (this)
				{
					this.fileStream.Write(data);
					BytesReceived += data.Length;
				}

				sync.GetProgress(this.Character)?.AddCurrentDownload(data.Length);
			}
		}
		catch (Exception ex)
		{
			this.recieveError = new Exception("Error receiving file data", ex);
		}
	}
}