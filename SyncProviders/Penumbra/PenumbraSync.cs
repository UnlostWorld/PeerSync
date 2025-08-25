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
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using Newtonsoft.Json;

namespace PeerSync.SyncProviders.Penumbra;

public class PenumbraSync : SyncProviderBase
{
	const ushort fileTimeout = 30000;

	private readonly PenumbraCommunicator penumbra = new();
	private readonly FileCache fileCache = new();

	private readonly Dictionary<string, FileInfo> hashToFileLookup = new();

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
					FileStream stream = file.OpenRead();

					// Hopefully the same as Mare's hash which used the Deprecated SHA1CryptoServiceProvider
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
				if (character.Connection == null || !character.Connection.ConnectionAlive())
					continue;

				byte[]? fileData = null;
				character.Connection.AppendIncomingPacketHandler<byte[]>(hash, (_, _, packet) => fileData = packet);
				character.Connection.SendObject("FileRequest", hash);

				Stopwatch sw = new();
				sw.Start();
				while (fileData == null && sw.ElapsedMilliseconds < fileTimeout)
				{
					await Task.Delay(100);
				}

				if (character.Connection == null || !character.Connection.ConnectionAlive())
					return;

				character.Connection.RemoveIncomingPacketHandler(hash);

				sw.Stop();
				if (fileData == null || sw.ElapsedMilliseconds >= fileTimeout)
				{
					Plugin.Log.Warning($"File transfer timeout");
					continue;
				}
				else if (fileData.Length <= 0)
				{
					Plugin.Log.Warning($"Peer did not send file: {hash}");
				}
				else
				{
					int size = fileData.Length / 1024;
					Plugin.Log.Information($"Took {sw.ElapsedMilliseconds}ms to transfer file: {hash} ({size}kb)");

					this.fileCache.SaveFile(fileData, hash);
				}
			}
		}

		await Task.Delay(100);
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

		byte[] data = File.ReadAllBytes(fileInfo.FullName);

		int size = data.Length / 1024;
		Plugin.Log.Information($"Sending file: {hash} ({size}kb)");

		connection.SendObject(hash, data);
	}

	public class PenumbraData
	{
		public Dictionary<string, string> Files { get; set; } = new();
		public string? MetaManipulations { get; set; }
	}
}