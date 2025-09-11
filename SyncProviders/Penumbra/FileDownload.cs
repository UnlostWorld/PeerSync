// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PeerSync.Network;

public class FileDownload : FileTransfer
{
	public long BytesToReceive = 0;
	public long BytesReceived = 0;

	private FileStream? fileStream;
	private byte queueIndex;
	private Exception? receiveError;

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
		FileInfo? file = this.sync.FileCache.GetFile(this.hash);

		if (file == null)
			return;

		if (this.cancellationToken.IsCancellationRequested
			|| this.Character.Connection == null)
			return;

		lock (this.sync)
		{
			this.queueIndex = this.sync.LastQueueIndex++;
		}

		this.Character.Connection.Received += this.OnReceived;

		byte[] hashBytes = Encoding.UTF8.GetBytes(this.hash);
		byte[] objectBytes = new byte[hashBytes.Length + 1];
		objectBytes[0] = this.queueIndex;
		Array.Copy(hashBytes, 0, objectBytes, 1, hashBytes.Length);

		this.Character.Send(PacketTypes.FileRequest, objectBytes);

		bool gotAllData = false;
		while (!gotAllData && !this.cancellationToken.IsCancellationRequested)
		{
			lock (this)
			{
				gotAllData = this.BytesReceived >= this.BytesToReceive;
			}

			if (this.receiveError != null)
				throw this.receiveError;

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
		this.sync.FileCache.GetFileHash(file.FullName, out string gotHash, out long fileSize);
		if (gotHash != this.hash)
		{
			Plugin.Log.Warning($"File failed to pass validation. Expected: {this.hash}, got {gotHash}");
			file.Delete();
			this.BytesReceived = 0;
			this.Retry();
			return;
		}
	}

	private void OnReceived(Connection connection, PacketTypes type, byte[] data)
	{
		if (type == PacketTypes.FileData)
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
					FileInfo? file = this.sync.FileCache.GetFile(this.hash);
					this.fileStream = new(file.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.None);
				}

				lock (this)
				{
					this.fileStream.Write(data);
					this.BytesReceived += data.Length;
				}

				this.sync.GetProgress(this.Character)?.AddCurrentDownload(data.Length);
			}
		}
		catch (Exception ex)
		{
			this.receiveError = new Exception("Error receiving file data", ex);
		}
	}
}