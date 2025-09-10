// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;

namespace PeerSync.SyncProviders.Penumbra;

public class FileUpload : IDisposable
{
	public long BytesSent = 0;
	public long BytesToSend = 0;

	public readonly CharacterSync Character;

	private readonly PenumbraSync sync;
	private readonly string hash;
	private readonly byte clientQueueIndex;
	private readonly CancellationTokenSource tokenSource = new();

	public FileUpload(PenumbraSync sync, byte clientQueueIndex, string hash, CharacterSync character)
	{
		this.sync = sync;
		this.hash = hash;
		this.Character = character;
		this.clientQueueIndex = clientQueueIndex;
		this.Name = sync.fileCache.GetFileName(hash);

		sync.uploads.Add(this);

		Task.Run(this.Transfer, tokenSource.Token);
	}

	public string Name { get; private set; }
	public bool IsWaiting { get; private set; }
	public float Progress => (float)this.BytesSent / (float)this.BytesToSend;

	public void Dispose()
	{
		if (!this.tokenSource.IsCancellationRequested)
			this.tokenSource.Cancel();

		this.tokenSource.Dispose();
	}

	private async Task Transfer()
	{
		await Plugin.Framework.RunOutsideUpdate();

		this.IsWaiting = true;

		do
		{
			lock (sync.uploads)
			{
				this.IsWaiting = sync.GetActiveUploadCount() >= Configuration.Current.MaxConcurrentUploads;
			}

			await Task.Delay(250);
		}
		while (this.IsWaiting && !this.tokenSource.IsCancellationRequested);

		this.IsWaiting = false;

		// Simulate
		if (this.Character.Pair.IsTestPair)
		{
			this.BytesToSend = 1024 * 1024 * 32;
			this.Name += " (fake)";
			while (this.BytesSent < this.BytesToSend)
			{
				long chunk = 1024 * 512;

				if (this.BytesSent + chunk >= this.BytesToSend)
					chunk = this.BytesToSend - this.BytesSent;

				this.BytesSent += chunk;
				sync.GetProgress(this.Character)?.AddCurrentUpload(chunk);

				await Task.Delay(100);
			}

			return;
		}

		try
		{
			FileInfo? fileInfo = sync.fileCache.GetFileInfo(hash);
			if (fileInfo == null || !fileInfo.Exists)
			{
				Plugin.Log.Warning($"File: {hash} missing!");
				this.Character.Send(Objects.FileData, [this.clientQueueIndex]);
				return;
			}

			this.Name = fileInfo.Name;

			FileStream? stream = null;
			int attempts = 5;
			Exception? lastException = null;
			while (stream == null && attempts > 0)
			{
				try
				{
					stream = new(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
				}
				catch (IOException ex)
				{
					lastException = ex;
					stream?.Dispose();
					stream = null;
					await Task.Delay(100);
				}
			}

			if (stream == null)
			{
				Plugin.Log.Error(lastException, "Error reading file for upload");
				return;
			}

			await Task.Delay(10);

			this.BytesSent = 0;
			this.BytesToSend = stream.Length;

			sync.GetProgress(this.Character)?.AddTotalUpload(BytesToSend);

			stream.Position = 0;

			do
			{
				long bytesLeft = this.BytesToSend - this.BytesSent;
				int thisChunkSize = (int)Math.Min(PenumbraSync.FileChunkSize, bytesLeft);

				byte[] bytes = new byte[thisChunkSize + 1];
				bytes[0] = this.clientQueueIndex;
				stream.ReadExactly(bytes, 1, thisChunkSize);

				this.Character.Send(Objects.FileData, bytes);
				this.BytesSent += thisChunkSize;
				sync.GetProgress(this.Character)?.AddCurrentUpload(thisChunkSize);
				await Task.Delay(10);
			}
			while (this.BytesSent < this.BytesToSend && !this.tokenSource.IsCancellationRequested);

			// File complete flag
			this.Character.Send(Objects.FileData, [this.clientQueueIndex]);
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error uploading file");
		}
		finally
		{
			if (!this.sync.uploads.TryRemove(this))
			{
				Plugin.Log.Error("Error removing upload from queue");
			}

			GC.Collect();
		}
	}
}
