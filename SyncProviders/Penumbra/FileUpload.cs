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
using System.Threading;
using System.Threading.Tasks;

public class FileUpload : FileTransfer
{
	public long BytesSent = 0;
	public long BytesToSend = 0;
	private readonly byte clientQueueIndex;

	public FileUpload(PenumbraSync sync, byte clientQueueIndex, string hash, CharacterSync character, CancellationToken token)
		: base(sync, hash, character, token)
	{
		this.clientQueueIndex = clientQueueIndex;
		this.Name = sync.FileCache.GetFileName(hash);
	}

	public override long Current => this.BytesSent;
	public override long Total => this.BytesToSend;

	protected override async Task Transfer()
	{
		FileInfo? fileInfo = this.sync.FileCache.GetFileInfo(this.hash);
		if (fileInfo == null || !fileInfo.Exists)
		{
			Plugin.Log.Warning($"File: {this.hash} missing!");
			this.Character.Send(PacketTypes.FileData, [this.clientQueueIndex]);
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

		stream.Position = 0;

		do
		{
			long bytesLeft = this.BytesToSend - this.BytesSent;
			int thisChunkSize = (int)Math.Min(PenumbraSync.FileChunkSize, bytesLeft);

			byte[] bytes = new byte[thisChunkSize + 1];
			bytes[0] = this.clientQueueIndex;
			stream.ReadExactly(bytes, 1, thisChunkSize);

			this.Character.Send(PacketTypes.FileData, bytes);
			this.BytesSent += thisChunkSize;
			await Task.Delay(10, this.cancellationToken);
		}
		while (this.BytesSent < this.BytesToSend && !this.cancellationToken.IsCancellationRequested);

		// Send the complete flag
		this.Character.Send(PacketTypes.FileData, [this.clientQueueIndex]);
	}
}
