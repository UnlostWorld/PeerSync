// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using PeerSync.Network;

namespace PeerSync.SyncProviders.Penumbra;

public class FileDownload : IDisposable
{
	public long BytesToReceive = 0;
	public long BytesReceived = 0;

	public string Name;

	private readonly PenumbraSync sync;
	private readonly string hash;
	private readonly CharacterSync character;
	private FileStream? fileStream;
	private byte queueIndex;
	private readonly CancellationTokenSource tokenSource = new();

	public FileDownload(
		PenumbraSync sync,
		string name,
		string hash,
		long expectedSize,
		CharacterSync character)
	{
		this.sync = sync;
		this.Name = name;
		this.hash = hash;
		this.character = character;
		this.BytesToReceive = expectedSize;

		sync.GetProgress(this.Character)?.AddTotalDownload(expectedSize);

		Task.Run(this.Transfer);
	}

	public CharacterSync Character => character;
	public bool IsWaiting { get; private set; }
	public float Progress => (float)this.BytesReceived / (float)this.BytesToReceive;
	public bool IsComplete { get; private set; }

	public void Dispose()
	{
		if (!this.tokenSource.IsCancellationRequested)
			this.tokenSource.Cancel();

		this.tokenSource.Dispose();
		this.fileStream?.Dispose();
		if (this.character.Connection != null)
			this.character.Connection.Received -= this.OnReceived;

		this.sync.downloads.TryRemove(this);
	}

	private async Task Transfer()
	{
		try
		{
			this.sync.downloads.Add(this);

			this.IsComplete = false;
			this.IsWaiting = true;

			do
			{
				lock (sync.downloads)
				{
					this.IsWaiting = sync.GetActiveDownloadCount() >= Configuration.Current.MaxConcurrentDownloads;
				}

				await Task.Delay(500);
			}
			while (this.IsWaiting && !this.tokenSource.IsCancellationRequested);

			this.IsWaiting = false;

			// Simulate
			if (this.character.Pair.IsTestPair)
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

				this.IsComplete = true;
				return;
			}


			FileInfo? file = sync.fileCache.GetFile(hash);

			if (file == null)
				return;

			while (!this.IsComplete)
			{
				if (this.sync.IsDisposed || this.character.Connection == null)
					return;

				this.queueIndex = this.sync.lastQueueIndex++;
				this.character.Connection.Received += this.OnReceived;

				byte[] hashBytes = Encoding.UTF8.GetBytes(hash);
				byte[] objectBytes = new byte[hashBytes.Length + 1];
				objectBytes[0] = this.queueIndex;
				Array.Copy(hashBytes, 0, objectBytes, 1, hashBytes.Length);

				character.Send(Objects.FileRequest, objectBytes);

				while (!this.IsComplete
					&& !this.tokenSource.IsCancellationRequested
					&& !this.sync.IsDisposed)
				{
					await Task.Delay(10);
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

				if (this.sync.IsDisposed || this.tokenSource.IsCancellationRequested)
				{
					file.Delete();
					return;
				}

				if (this.BytesReceived <= 0)
				{
					Plugin.Log.Warning($"Received 0 length file");
					file.Delete();
				}

				// hash verify
				bool found = this.sync.fileCache.GetFileHash(file.FullName, out string gotHash, out long fileSize);
				if (gotHash != hash)
				{
					Plugin.Log.Warning($"File failed to pass validation. Expected: {hash}, got {gotHash}");
					file.Delete();
				}

				file = sync.fileCache.GetFile(hash);
				this.IsComplete = file.Exists;

				if (!this.IsComplete)
				{
					Plugin.Log.Information($"Retry download: {this.Name}");
					this.BytesReceived = 0;
					await Task.Delay(1000);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error downloading file");
		}
		finally
		{
			this.IsComplete = true;

			if (this.fileStream != null)
			{
				lock (this.fileStream)
				{
					this.fileStream.Flush();
					this.fileStream.Dispose();
					this.fileStream = null;
				}
			}

			if (this.character.Connection != null)
				this.character.Connection.Received -= this.OnReceived;

			this.sync.downloads.TryRemove(this);

			GC.Collect();
		}
	}

	private void OnReceived(Connection connection, byte typeId, byte[] data)
	{
		if (typeId == Objects.FileData)
		{
			byte clientQueueIndex = data[0];

			if (clientQueueIndex != this.queueIndex)
				return;

			if (connection != this.character.Connection)
				return;

			byte[] fileData = new byte[data.Length - 1];
			Array.Copy(data, 1, fileData, 0, data.Length - 1);
			this.OnFileData(fileData);
		}
	}

	private void OnFileData(byte[] data)
	{
		if (this.sync.IsDisposed || this.tokenSource.IsCancellationRequested)
			return;

		try
		{
			if (data.Length <= 1)
			{
				this.IsComplete = true;
			}
			else
			{
				if (this.fileStream == null)
				{
					FileInfo? file = sync.fileCache.GetFile(hash);
					this.fileStream = new(file.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.None);
				}

				lock (this.fileStream)
				{
					this.fileStream.Write(data);
				}

				BytesReceived += data.Length;
				sync.GetProgress(this.Character)?.AddCurrentDownload(data.Length);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error receiving file data");
		}
	}
}