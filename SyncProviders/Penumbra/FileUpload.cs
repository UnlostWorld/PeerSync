// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Diagnostics;
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
		else // Real
		{
			Stopwatch sw = new();
			sw.Start();

			Plugin.Log.Info($"1: {sw.ElapsedMilliseconds}");
			sw.Restart();

			FileInfo? fileInfo = sync.fileCache.GetFileInfo(hash);
			if (fileInfo == null || !fileInfo.Exists)
			{
				Plugin.Log.Warning($"File: {hash} missing!");
				this.Character.Send(Objects.FileData, [this.clientQueueIndex]);
				return;
			}

			Plugin.Log.Info($"2: {sw.ElapsedMilliseconds}");
			sw.Restart();

			this.Name = fileInfo.Name;

			Plugin.Log.Info($"3: {sw.ElapsedMilliseconds}");
			sw.Restart();

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

			Plugin.Log.Info($"4: {sw.ElapsedMilliseconds}");
			sw.Restart();

			if (stream == null)
			{
				Plugin.Log.Error(lastException, "Error reading file for upload");
				return;
			}

			Plugin.Log.Info($"5: {sw.ElapsedMilliseconds}");
			sw.Restart();

			this.BytesSent = 0;
			this.BytesToSend = stream.Length;

			sync.GetProgress(this.Character)?.AddTotalUpload(BytesToSend);

			stream.Position = 0;

			Plugin.Log.Info($"6: {sw.ElapsedMilliseconds}");
			sw.Restart();

			do
			{
				Plugin.Log.Info($"7: {sw.ElapsedMilliseconds}");
				sw.Restart();

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

			Plugin.Log.Info($"8: {sw.ElapsedMilliseconds}");
			sw.Restart();

			// Send the complete flag
			this.Character.Send(Objects.FileData, [this.clientQueueIndex]);

			Plugin.Log.Info($"9: {sw.ElapsedMilliseconds}");
		}
	}
}
