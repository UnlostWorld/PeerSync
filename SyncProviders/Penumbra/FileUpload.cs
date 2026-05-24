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
using PeerSync.Connections;

public class FileUpload : FileTransfer
{
	public long BytesSent = 0;
	public long BytesToSend = 0;

	public FileUpload(PenumbraSync sync, byte clientQueueIndex, string hash, CharacterConnection character)
		: base(sync, hash, character, clientQueueIndex)
	{
		this.Name = sync.FileCache.GetFileName(hash);
	}

	public override long Current => this.BytesSent;
	public override long Total => this.BytesToSend;

	protected override async Task Transfer()
	{
		Plugin.Log.Debug($"Start upload {this.hash}");

		FileInfo? fileInfo = this.sync.FileCache.GetFileInfo(this.hash);
		if (fileInfo == null || !fileInfo.Exists)
		{
			Plugin.Log.Warning($"File: {this.hash} missing!");
			this.Character.Send(PacketTypes.FileData, [this.ClientQueueIndex]);
			return;
		}

		this.Name = fileInfo.Name;

		byte part = 0;

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

			if (bytesLeft <= 0)
				continue;

			byte[] bytes = new byte[thisChunkSize + 2];
			bytes[0] = this.ClientQueueIndex;
			bytes[1] = part;
			stream.ReadExactly(bytes, 2, thisChunkSize);

			this.Character.Send(PacketTypes.FileData, bytes);
			this.BytesSent += thisChunkSize;
			part++;
			await Task.Delay(10, this.cancellationToken);
		}
		while (this.BytesSent < this.BytesToSend && !this.cancellationToken.IsCancellationRequested);

		// Send the complete flag
		this.Character.Send(PacketTypes.FileData, [this.ClientQueueIndex]);
	}
}
