// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using Dalamud.Bindings.ImGui;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using Newtonsoft.Json;
using static NetworkCommsDotNet.Tools.StreamTools;

namespace PeerSync.SyncProviders.Penumbra;

public class PenumbraSync : SyncProviderBase
{
	const int fileTimeout = 120_000;
	const int fileChunkSize = 1024 * 10; // 10kb chunks
	const int maxConcurrentUploads = 5;
	const int maxConcurrentDownloads = 5;

	private readonly PenumbraCommunicator penumbra = new();
	private readonly FileCache fileCache = new();

	private readonly Dictionary<string, FileInfo> hashToFileLookup = new();

	public override string Key => "Penumbra";
	public override bool HasTab => true;

	private ConcurrentHashSet<FileDownload> Downloads { get; init; } = new();
	private ConcurrentHashSet<FileUpload> Uploads { get; init; } = new();

	public static readonly HashSet<string> AllowedFileExtensions =
	[
		".mdl",
		".tex",
		".mtrl",
		".tmb",
		".pap",
		".avfx",
		".atex",
		".sklb",
		".eid",
		".phyb",
		".pbd",
		".scd",
		".skp",
		".shpk"
	];

	public override void OnInitialized()
	{
		NetworkComms.AppendGlobalIncomingPacketHandler<string>("FileRequest", this.OnFileRequest);
	}

	public override async Task<string?> Serialize(ushort objectIndex)
	{
		if (!penumbra.GetIsAvailable())
			return null;

		Dictionary<string, HashSet<string>>? resourcePaths = await this.penumbra.GetGameObjectResourcePaths(objectIndex);
		if (resourcePaths == null)
			return null;

		// Perform file hashing on a separate thread.
		await Task.Delay(1).ConfigureAwait(false);

		PenumbraData data = new();
		SHA1 sha = SHA1.Create();
		byte[] bytes;

		// Get file hashes
		data.Files = new();
		foreach ((string path, HashSet<string> gamePaths) in resourcePaths)
		{
			foreach (string gamePath in gamePaths)
			{
				// Is this a redirect?
				if (gamePath == path)
					continue;

				if (!AllowedFileExtensions.Contains(Path.GetExtension(gamePath)))
					continue;

				bool isFilePath = Path.IsPathRooted(path);
				if (isFilePath)
				{
					FileInfo file = new(path);
					using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, fileChunkSize, false);

					bytes = sha.ComputeHash(stream);
					string str = BitConverter.ToString(bytes);
					str = str.Replace("-", string.Empty, StringComparison.Ordinal);
					str += Path.GetExtension(path);

					hashToFileLookup[str] = file;
					data.Files[gamePath] = str;
					data.FileSizes[str] = stream.Length;
				}
				else
				{
					// Unsure about this one.
					Plugin.Log.Warning("I haven't written this part yet.");
					////data.Files[gamePath] = path;
				}
			}
		}

		// get meta manipulations
		data.MetaManipulations = await this.penumbra.GetMetaManipulations(objectIndex);

		// serialize to Base64 compressed json
		string json = JsonConvert.SerializeObject(data);
		bytes = Encoding.UTF8.GetBytes(json);
		using MemoryStream compressedStream = new();
		using (GZipStream zipStream = new(compressedStream, CompressionMode.Compress))
		{
			zipStream.Write(bytes, 0, bytes.Length);
		}

		return Convert.ToBase64String(compressedStream.ToArray());
	}

	public override async Task Deserialize(string? content, CharacterSync character)
	{
		if (!penumbra.GetIsAvailable())
			return;

		if (!this.fileCache.IsValid())
			return;

		if (content == null)
		{
			// TODO: Disable mod collection
			return;
		}

		byte[] bytes = Convert.FromBase64String(content);
		using MemoryStream compressedStream = new(bytes);
		using GZipStream zipStream = new(compressedStream, CompressionMode.Decompress);
		using MemoryStream resultStream = new();
		zipStream.CopyTo(resultStream);
		bytes = resultStream.ToArray();

		string json = Encoding.UTF8.GetString(bytes, 0, bytes.Length);

		PenumbraData? data = JsonConvert.DeserializeObject<PenumbraData>(json);
		if (data == null)
			return;

		foreach ((string gamePath, string hash) in data.Files)
		{
			FileInfo? file = this.fileCache.GetFile(hash);
			if (file == null || !file.Exists)
			{
				long expectedSize = 0;
				data.FileSizes.TryGetValue(hash, out expectedSize);

				string name = Path.GetFileName(gamePath);
				new FileDownload(this, name, hash, expectedSize, character);
			}
		}

		// Wait for all downloads from the target character to complete...
		bool done = false;
		while (!done)
		{
			done = true;
			foreach (FileDownload t in this.Downloads)
			{
				if (t.Character == character && !t.IsComplete)
				{
					done = false;
					break;
				}
			}

			await Task.Delay(100);
		}

		// TODO: Make mod collection!
		Plugin.Log.Warning($"Files synced!");
	}

	public int GetActiveDownloadCount()
	{
		int count = 0;
		foreach (FileDownload download in this.Downloads)
		{
			if (download.IsWaiting)
				continue;

			count++;
		}

		return count;
	}

	public int GetActiveUploadCount()
	{
		int count = 0;
		foreach (FileUpload upload in this.Uploads)
		{
			if (upload.IsWaiting)
				continue;

			count++;
		}

		return count;
	}

	private void OnFileRequest(PacketHeader packetHeader, Connection connection, string hash)
	{
		FileInfo? fileInfo = null;
		if (!hashToFileLookup.TryGetValue(hash, out fileInfo) || fileInfo == null || !fileInfo.Exists)
		{
			Plugin.Log.Warning($"File: {hash} missing!");
			connection.SendObject(hash, new byte[0]);
			return;
		}

		CharacterSync? character = Plugin.Instance?.GetCharacterSync(connection);
		if (character == null)
		{
			Plugin.Log.Warning($"File request from unknown connection!");
			return;
		}

		new FileUpload(this, hash, fileInfo, character);
	}

	public override void DrawTab()
	{
		base.DrawTab();

		ImGui.Text($"Uploading {this.GetActiveUploadCount()} / {this.Uploads.Count}");
		foreach (FileUpload upload in this.Uploads)
		{
			if (upload.IsWaiting)
				continue;

			ImGui.Text($" > {upload.Name} - {(int)(upload.Progress * 100)}%");
		}

		ImGui.Text($"Downloading {this.GetActiveDownloadCount()} / {this.Downloads.Count}");
		foreach (FileDownload download in this.Downloads)
		{
			if (download.IsWaiting)
				continue;

			ImGui.Text($" > {download.Name} - {(int)(download.Progress * 100)}%");
		}
	}

	public class PenumbraData
	{
		public Dictionary<string, string> Files { get; set; } = new();
		public Dictionary<string, long> FileSizes { get; set; } = new();
		public string? MetaManipulations { get; set; }
	}

	public class FileUpload
	{
		public long BytesSent = 0;
		public long BytesToSend = 0;

		public readonly string Name;

		private readonly PenumbraSync sync;
		private readonly string hash;
		private readonly FileInfo file;
		private readonly CharacterSync character;

		public FileUpload(PenumbraSync sync, string hash, FileInfo file, CharacterSync character)
		{
			this.sync = sync;
			this.hash = hash;
			this.file = file;
			this.character = character;

			this.Name = file.Name;
			sync.Uploads.Add(this);

			ThreadStart ts = new(this.Transfer);
			Thread thread = new(ts);
			thread.Start();
		}

		public bool IsWaiting { get; private set; }
		public float Progress => (float)this.BytesSent / (float)this.BytesToSend;

		private void Transfer()
		{
			this.IsWaiting = true;
			while (sync.GetActiveUploadCount() >= maxConcurrentUploads)
				Thread.Sleep(500);

			this.IsWaiting = false;
			try
			{
				FileStream? stream = null;
				int attempts = 5;
				Exception? lastException = null;
				while (stream == null && attempts > 0)
				{
					try
					{
						stream = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
					}
					catch (IOException ex)
					{
						lastException = ex;
						stream?.Dispose();
						stream = null;
						Thread.Sleep(100);
					}
				}

				if (stream == null)
				{
					Plugin.Log.Error(lastException, "Error reading file for upload");
					return;
				}

				using ThreadSafeStream threadSafeStream = new ThreadSafeStream(stream);

				this.BytesSent = 0;
				this.BytesToSend = stream.Length;

				do
				{
					long thisChunkSize = fileChunkSize;
					if (this.BytesSent + thisChunkSize > this.BytesToSend)
						thisChunkSize = this.BytesToSend - this.BytesSent;

					using StreamSendWrapper streamWrapper = new StreamSendWrapper(
						threadSafeStream,
						this.BytesSent,
						thisChunkSize);

					if (character.Connection == null || !character.Connection.ConnectionAlive())
						return;

					long packetSequenceNumber;
					this.character.Connection.SendObject(hash, streamWrapper, out packetSequenceNumber);
					this.BytesSent += thisChunkSize;
				}
				while (this.BytesSent < this.BytesToSend);

				if (character.Connection == null || !character.Connection.ConnectionAlive())
					return;

				// File complete flag
				byte[] b = [1];
				this.character.Connection?.SendObject(hash, b);
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error uploading file");
			}
			finally
			{
				if (!this.sync.Uploads.TryRemove(this))
				{
					Plugin.Log.Error("Error removing upload from queue");
				}
			}
		}
	}

	public class FileDownload : IDisposable
	{
		public long BytesToReceive = 0;
		public long BytesReceived = 0;

		public readonly string Name;

		private readonly PenumbraSync sync;
		private readonly string hash;
		private readonly CharacterSync character;
		private FileStream? fileStream;
		private readonly CancellationTokenSource cts;

		public FileDownload(
			PenumbraSync sync,
			string name,
			string hash,
			long expectedSize,
			CharacterSync character)
		{
			this.cts = new(fileTimeout);
			this.cts.Token.Register(this.OnTimeout);

			this.sync = sync;
			this.Name = name;
			this.hash = hash;
			this.character = character;
			this.BytesToReceive = expectedSize;

			this.Transfer();
		}

		private void OnTimeout()
		{
			throw new TimeoutException();
		}

		public CharacterSync Character => character;
		public bool IsWaiting { get; private set; }
		public float Progress => (float)this.BytesReceived / (float)this.BytesToReceive;
		public bool IsComplete { get; private set; }

		public void Dispose()
		{
			this.character.Connection?.RemoveIncomingPacketHandler(this.hash);
			this.fileStream?.Dispose();
			this.cts?.Dispose();

			if (!this.sync.Downloads.TryRemove(this))
			{
				Plugin.Log.Error("Error removing download from queue");
			}
		}

		private void Transfer()
		{
			try
			{
				this.sync.Downloads.Add(this);

				this.IsComplete = false;
				this.IsWaiting = true;
				while (sync.GetActiveDownloadCount() >= maxConcurrentDownloads)
					Thread.Sleep(500);

				this.IsWaiting = false;
				FileInfo? file = sync.fileCache.GetFile(hash);

				if (file == null)
					return;

				if (!file.Exists)
				{
					if (character.Connection == null || !character.Connection.ConnectionAlive())
						return;

					//We create a file on disk so that we can receive large files
					this.fileStream = new(file.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, fileChunkSize, FileOptions.None);

					character.Connection.AppendIncomingPacketHandler<byte[]>(hash, this.OnDataReceived);

					character.Connection.SendObject("FileRequest", hash);
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error downloading file");
			}
		}

		private void OnDataReceived(PacketHeader packetHeader, Connection connection, byte[] data)
		{
			if (data.Length <= 1)
			{
				this.IsComplete = true;
			}
			else
			{
				this.IsComplete = false;
				this.fileStream?.Write(data);
				BytesReceived += data.Length;
			}

			if (this.IsComplete)
			{
				this.fileStream?.Flush();
				this.fileStream?.Dispose();
				this.fileStream = null;
			}

			if (cts.IsCancellationRequested)
			{
				Plugin.Log.Warning($"File download timeout");
			}
			else if (BytesReceived <= 0)
			{
				Plugin.Log.Warning($"Peer did not send file: {hash}");
			}
		}
	}
}