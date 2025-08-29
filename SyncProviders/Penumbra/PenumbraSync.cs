// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using PeerSync.Network;

namespace PeerSync.SyncProviders.Penumbra;

public class PenumbraSync : SyncProviderBase
{
	const int fileTimeout = 240_000;
	const int fileChunkSize = 1024 * 128; // 128kb chunks

	private readonly PenumbraCommunicator penumbra = new();
	private readonly FileCache fileCache = new();

	private readonly ConcurrentHashSet<FileDownload> downloads = new();
	private readonly ConcurrentHashSet<FileUpload> uploads = new();

	private readonly Dictionary<string, FileInfo> hashToFileLookup = new();

	private byte lastQueueIndex = 0;

	public override string Key => "Penumbra";

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

	public override void OnCharacterConnected(CharacterSync character)
	{
		if (character.Connection != null)
			character.Connection.Received += this.OnReceived;

		base.OnCharacterConnected(character);
	}

	public override void OnCharacterDisconnected(CharacterSync character)
	{
		if (character.Connection != null)
			character.Connection.Received -= this.OnReceived;

		base.OnCharacterDisconnected(character);
	}

	private bool GetFileHash(string path, out string hash, out long fileSize)
	{
		FileInfo file = new(path);

		hash = string.Empty;
		fileSize = 0;

		if (!file.Exists)
			return false;

		using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, fileChunkSize, false);
		fileSize = stream.Length;

		SHA1 sha = SHA1.Create();
		byte[] bytes = sha.ComputeHash(stream);
		string str = BitConverter.ToString(bytes);
		str = str.Replace("-", string.Empty, StringComparison.Ordinal);
		str += Path.GetExtension(path);
		hash = str;

		return true;
	}

	private void OnReceived(Connection connection, byte typeId, byte[] data)
	{
		if (typeId == Objects.FileRequest)
		{
			byte clientQueueIndex = data[0];

			byte[] hashData = new byte[data.Length - 1];
			Array.Copy(data, 1, hashData, 0, data.Length - 1);

			string hash = Encoding.UTF8.GetString(hashData);

			Plugin.Log.Info($"Got file request Id: {clientQueueIndex}, {hash}");

			string? fileExtension = Path.GetExtension(hash);
			if (fileExtension == null)
				throw new Exception("Invalid file request");

			if (!AllowedFileExtensions.Contains(fileExtension))
				throw new Exception("Attempt to request forbidden file extension");

			this.OnFileRequest(connection, clientQueueIndex, hash);
		}
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
					bool found = GetFileHash(path, out string hash, out long fileSize);
					if (!found)
					{
						Plugin.Log.Warning($"File not found for sync: {path}");
						continue;
					}

					hashToFileLookup[hash] = new(path);
					data.Files[gamePath] = hash;
					data.FileSizes[hash] = fileSize;
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
		byte[] bytes = Encoding.UTF8.GetBytes(json);
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
			foreach (FileDownload t in this.downloads)
			{
				if (t.Character == character && !t.IsComplete)
				{
					done = false;
					break;
				}
			}

			await Task.Delay(100);
		}

		// Verify that we did get all the files we need.
		// This may fail if some downloads were corrupted or dropped.
		// just silently fail out and let the next deserialize try again.
		foreach ((string gamePath, string hash) in data.Files)
		{
			FileInfo? file = this.fileCache.GetFile(hash);
			if (file == null || !file.Exists)
			{
				return;
			}
		}

		// TODO: Make mod collection!
		Plugin.Log.Info($"Files synced!");
	}

	public int GetActiveDownloadCount()
	{
		int count = 0;
		foreach (FileDownload download in this.downloads)
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
		foreach (FileUpload upload in this.uploads)
		{
			if (upload.IsWaiting)
				continue;

			count++;
		}

		return count;
	}

	public override void DrawStatus()
	{
		base.DrawStatus();

		int uploadCount = this.GetActiveUploadCount();
		int downloadCount = this.GetActiveDownloadCount();

		string title = "Transfers";
		if (uploadCount > 0 && downloadCount == 0)
			title = $"Transfers (▲{uploadCount})";

		if (downloadCount > 0 && uploadCount == 0)
			title = $"Transfers (▼{downloadCount})";

		if (downloadCount > 0 && uploadCount > 0)
			title = $"Transfers (▲{uploadCount} ▼{downloadCount})";

		if (ImGui.CollapsingHeader($"{title}###transfersSection"))
		{
			int queuedDownloads = this.downloads.Count - downloadCount;
			int queuedUploads = this.uploads.Count - uploadCount;

			if (uploadCount > 0 || queuedUploads > 0)
			{
				ImGui.Text($"▲ Uploading");

				if (queuedUploads > 0)
				{
					ImGui.SameLine();
					ImGui.Text($" ({queuedUploads} in queue)");
				}

				foreach (FileUpload upload in this.uploads)
				{
					if (upload.IsWaiting)
						continue;

					ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 10);
					ImGui.ProgressBar(upload.Progress, new(64, 5), string.Empty);
					ImGui.SameLine();
					ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 10);
					ImGui.Text(upload.Name);
				}
			}

			if (downloadCount > 0 || queuedDownloads > 0)
			{
				ImGui.Text($"▼ Downloading");

				if (queuedUploads > 0)
				{
					ImGui.SameLine();
					ImGui.Text($" ({queuedDownloads} in queue)");
				}

				foreach (FileDownload download in this.downloads)
				{
					if (download.IsWaiting)
						continue;

					ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 10);
					ImGui.ProgressBar(download.Progress, new(64, 5), string.Empty);
					ImGui.SameLine();
					ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 10);
					ImGui.Text(download.Name);
				}
			}
		}
	}

	private void OnFileRequest(Connection connection, byte clientQueueIndex, string hash)
	{
		CharacterSync? character = Plugin.Instance?.GetCharacterSync(connection);
		if (character == null)
		{
			Plugin.Log.Warning($"File request from unknown connection!");
			return;
		}

		new FileUpload(this, clientQueueIndex, hash, character);
	}

	public override void Dispose()
	{
		base.Dispose();

		foreach (FileDownload download in this.downloads)
		{
			download.Dispose();
		}

		this.downloads.Clear();

		foreach (FileUpload upload in this.uploads)
		{
			upload.Dispose();
		}

		this.uploads.Clear();
	}

	public class PenumbraData
	{
		public Dictionary<string, string> Files { get; set; } = new();
		public Dictionary<string, long> FileSizes { get; set; } = new();
		public string? MetaManipulations { get; set; }
	}

	public class FileUpload : IDisposable
	{
		public int BytesSent = 0;
		public long BytesToSend = 0;

		private readonly PenumbraSync sync;
		private readonly string hash;
		private readonly CharacterSync character;
		private readonly byte clientQueueIndex;
		private readonly CancellationTokenSource tokenSource = new();

		public FileUpload(PenumbraSync sync, byte clientQueueIndex, string hash, CharacterSync character)
		{
			this.sync = sync;
			this.hash = hash;
			this.character = character;
			this.clientQueueIndex = clientQueueIndex;

			FileInfo? fileInfo = null;
			if (sync.hashToFileLookup.TryGetValue(hash, out fileInfo) && fileInfo != null)
			{
				this.Name = fileInfo.Name;
			}
			else
			{
				this.Name = hash;
			}

			sync.uploads.Add(this);

			Task.Run(this.Transfer);
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
			this.IsWaiting = true;

			do
			{
				lock (sync.downloads)
				{
					this.IsWaiting = sync.GetActiveUploadCount() >= Configuration.Current.MaxConcurrentUploads;
				}

				await Task.Delay(250);
			}
			while (this.IsWaiting && !this.tokenSource.IsCancellationRequested);

			this.IsWaiting = false;
			try
			{
				FileInfo? fileInfo = null;
				if (!sync.hashToFileLookup.TryGetValue(hash, out fileInfo) || fileInfo == null || !fileInfo.Exists)
				{
					Plugin.Log.Warning($"File: {hash} missing!");
					this.character.Send(Objects.FileData, [this.clientQueueIndex]);
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
				stream.Position = 0;

				do
				{
					long bytesLeft = this.BytesToSend - this.BytesSent;
					int thisChunkSize = (int)Math.Min(fileChunkSize, bytesLeft);

					byte[] bytes = new byte[thisChunkSize + 1];
					bytes[0] = this.clientQueueIndex;
					stream.ReadExactly(bytes, 1, thisChunkSize);

					this.character.Send(Objects.FileData, bytes);
					this.BytesSent += thisChunkSize;
					await Task.Delay(10);
				}
				while (this.BytesSent < this.BytesToSend && !this.tokenSource.IsCancellationRequested);

				// File complete flag
				this.character.Send(Objects.FileData, [this.clientQueueIndex]);
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

	public class FileDownload : IDisposable
	{
		public long BytesToReceive = 0;
		public long BytesReceived = 0;

		public readonly string Name;

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
				FileInfo? file = sync.fileCache.GetFile(hash);

				if (file == null)
					return;

				if (!file.Exists)
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

					Stopwatch sw = new();
					sw.Start();
					while (!this.IsComplete && sw.ElapsedMilliseconds < PenumbraSync.fileTimeout
						 && !this.tokenSource.IsCancellationRequested)
					{
						await Task.Delay(10);
					}

					sw.Stop();

					if (this.fileStream != null)
					{
						lock (this.fileStream)
						{
							this.fileStream.Flush();
							this.fileStream.Dispose();
							this.fileStream = null;
						}
					}

					if (this.tokenSource.IsCancellationRequested)
						return;

					if (sw.ElapsedMilliseconds >= PenumbraSync.fileTimeout)
						throw new TimeoutException();

					if (this.BytesReceived <= 0)
						throw new Exception("Received 0 length file");

					// hash verify
					bool found = sync.GetFileHash(file.FullName, out string gotHash, out long fileSize);
					if (gotHash != hash)
					{
						Plugin.Log.Warning($"File failed to pass validation. Expected: {hash}, got {gotHash}");
						file.Delete();
					}

					this.IsComplete = true;
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
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error receiving file data");
			}
		}
	}
}