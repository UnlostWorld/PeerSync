// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PeerSync.SyncProviders.Penumbra;

public class FileUpload : FileTransfer
{
	public long BytesSent = 0;
	public long BytesToSend = 0;
	private readonly byte clientQueueIndex;

	public FileUpload(PenumbraSync sync, byte clientQueueIndex, string hash, CharacterSync character, CancellationToken token)
		: base(sync, hash, character, token)
	{
		this.clientQueueIndex = clientQueueIndex;
		this.Name = sync.fileCache.GetFileName(hash);
	}

	public override float Progress => (float)this.BytesSent / (float)this.BytesToSend;

	protected override async Task Transfer()
	{
		// Simulate
		if (this.Character.Peer.IsTestPeer)
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
		else // Real
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
					await Task.Delay(100, this.cancellationToken);
				}
			}

			if (stream == null)
			{
				Plugin.Log.Error(lastException, "Error reading file for upload");
				return;
			}

			await Task.Delay(10, this.cancellationToken);

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
				await Task.Delay(10, this.cancellationToken);
			}
			while (this.BytesSent < this.BytesToSend && !this.cancellationToken.IsCancellationRequested);

			// Send the complete flag
			this.Character.Send(Objects.FileData, [this.clientQueueIndex]);
		}
	}
}
