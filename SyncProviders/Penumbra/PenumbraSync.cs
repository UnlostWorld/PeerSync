// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using Newtonsoft.Json;
using static NetworkCommsDotNet.Tools.StreamTools;

namespace PeerSync.SyncProviders.Penumbra;

public class PenumbraSync : SyncProviderBase
{
	const int fileTimeout = 120_000;
	const int fileChunkSize = 1024 * 100; // 100kb chunks
	const int maxConcurrentUploads = 5;
	const int maxConcurrentDownloads = 5;

	private readonly PenumbraCommunicator penumbra = new();
	private readonly FileCache fileCache = new();

	private readonly Dictionary<string, FileInfo> hashToFileLookup = new();

	public override string Key => "Penumbra";
	public override bool HasTab => true;

	private List<FileDownload> Downloads { get; init; } = new();
	private List<FileUpload> Uploads { get; init; } = new();
	private int ActiveUploadCount { get; set; } = 0;
	private int ActiveDownloadCount { get; set; } = 0;

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
				}
				else
				{
					// for redirects that are not modded files, don't hash it, just send it as is.
					data.Files[gamePath] = path;
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
				new FileDownload(this, hash, character);
			}
		}

		// Wait for all downloads from the target character to complete...
		bool done = false;
		while (!done)
		{
			done = true;
			foreach (FileDownload t in this.Downloads)
			{
				if (t.Character == character)
				{
					done = false;
					await t.Await();
					break;
				}
			}
		}

		// TODO: Make mod collection!
		Plugin.Log.Warning($"Files synced!");
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

		ImGui.Text($"Uploading {this.ActiveUploadCount} / {this.Uploads.Count}");
		foreach (FileUpload upload in this.Uploads)
		{
			ImGui.Text($"{(int)(upload.Progress * 100)}%");
		}

		ImGui.Text($"Downloading {this.ActiveDownloadCount} / {this.Downloads.Count}");
		foreach (FileDownload upload in this.Downloads)
		{
			ImGui.Text($"...%");
		}
	}

	public class PenumbraData
	{
		public Dictionary<string, string> Files { get; set; } = new();
		public string? MetaManipulations { get; set; }
	}

	public class FileUpload
	{
		public long BytesSent = 0;
		public long BytesToSend = 0;

		private readonly Task? transferTask;
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

			sync.Uploads.Add(this);
			this.transferTask = Task.Run(this.Transfer);
		}

		public bool IsWaiting { get; private set; }
		public float Progress => (float)this.BytesSent / (float)this.BytesToSend;

		public Task Await() => this.transferTask ?? Task.CompletedTask;

		private async Task Transfer()
		{
			this.IsWaiting = true;
			while (sync.ActiveUploadCount >= maxConcurrentUploads)
				await Task.Delay(500);

			this.IsWaiting = false;
			sync.ActiveUploadCount++;
			try
			{
				using FileStream stream = new(file.FullName, FileMode.Open);
				using ThreadSafeStream threadSafeStream = new ThreadSafeStream(stream);
				Plugin.Log.Information($"Sending file: {hash} ({stream.Length / 1024}kb)");

				this.BytesSent = 0;
				this.BytesToSend = stream.Length;

				do
				{
					await Task.Delay(10);
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
				this.sync.Uploads.Remove(this);
				this.sync.ActiveUploadCount--;
			}
		}
	}

	public class FileDownload
	{
		public long BytesReceived = 0;

		private readonly Task? transferTask;
		private readonly PenumbraSync sync;
		private readonly string hash;
		private readonly CharacterSync character;

		public FileDownload(PenumbraSync sync, string hash, CharacterSync character)
		{
			this.sync = sync;
			this.hash = hash;
			this.character = character;
			this.transferTask = Task.Run(this.Transfer);

			this.sync.Downloads.Add(this);
		}

		public CharacterSync Character => character;
		public bool IsWaiting { get; private set; }
		public Task Await() => this.transferTask ?? Task.CompletedTask;

		private async Task Transfer()
		{
			try
			{
				this.IsWaiting = true;
				while (sync.ActiveDownloadCount >= maxConcurrentDownloads)
					await Task.Delay(500);

				this.IsWaiting = false;
				sync.ActiveDownloadCount++;
				FileInfo? file = sync.fileCache.GetFile(hash);

				if (file == null)
					return;

				if (!file.Exists)
				{
					if (character.Connection == null || !character.Connection.ConnectionAlive())
						return;

					//We create a file on disk so that we can receive large files
					using FileStream fileStream = new(file.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, fileChunkSize, FileOptions.None);

					bool complete = false;
					character.Connection.AppendIncomingPacketHandler<byte[]>(hash, (_, _, data) =>
					{
						if (data.Length <= 1)
						{
							complete = true;
						}
						else
						{
							fileStream.Write(data);
							BytesReceived += data.Length;
						}
					});

					character.Connection.SendObject("FileRequest", hash);

					Stopwatch sw = new();
					sw.Start();
					while (!complete && sw.ElapsedMilliseconds < fileTimeout)
					{
						await Task.Delay(100);
					}

					fileStream.Flush();

					if (character.Connection == null || !character.Connection.ConnectionAlive())
						return;

					character.Connection.RemoveIncomingPacketHandler(hash);

					sw.Stop();
					if (!complete || sw.ElapsedMilliseconds >= fileTimeout)
					{
						Plugin.Log.Warning($"File transfer timeout");
						return;
					}
					else if (BytesReceived <= 0)
					{
						Plugin.Log.Warning($"Peer did not send file: {hash}");
					}
					else
					{
						Plugin.Log.Information($"Took {sw.ElapsedMilliseconds}ms to transfer file: {hash} ({BytesReceived / 1024}kb)");
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Error transferring file");
			}
			finally
			{
				sync.ActiveDownloadCount--;
				this.sync.Downloads.Remove(this);
			}
		}
	}
}