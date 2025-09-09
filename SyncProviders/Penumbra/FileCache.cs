// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using PeerSync;

public class FileCache : IDisposable
{
	private readonly CancellationTokenSource tokenSource = new();

	private readonly Dictionary<string, FileInfo> hashToFileLookup = new();

	private float totalCacheSizeGb = 0;
	private int fileCount = 0;

	public FileCache()
	{
		Task.Run(this.CacheThread, this.tokenSource.Token);
	}

	public FileInfo? GetFileInfo(string hash)
	{
		FileInfo? fileInfo = null;
		this.hashToFileLookup.TryGetValue(hash, out fileInfo);
		return fileInfo;
	}

	public string GetFileName(string hash)
	{
		FileInfo? fileInfo = null;
		if (this.hashToFileLookup.TryGetValue(hash, out fileInfo) && fileInfo != null)
		{
			return fileInfo.Name;
		}

		return hash;
	}

	public bool GetFileHash(string path, out string hash, out long fileSize)
	{
		FileInfo file = new(path);

		hash = string.Empty;
		fileSize = 0;

		if (!file.Exists)
			return false;

		using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, PenumbraSync.FileChunkSize, false);
		fileSize = stream.Length;

		SHA1 sha = SHA1.Create();
		byte[] bytes = sha.ComputeHash(stream);
		string str = BitConverter.ToString(bytes);
		str = str.Replace("-", string.Empty, StringComparison.Ordinal);
		str += Path.GetExtension(path);
		hash = str;

		this.hashToFileLookup[hash] = new(path);

		return true;
	}

	public void Dispose()
	{
		this.tokenSource.Cancel();
	}

	public bool IsValid()
	{
		return this.GetDirectory() != null;
	}

	public FileInfo GetFile(string hash)
	{
		DirectoryInfo? dir = this.GetDirectory();
		if (dir == null)
			throw new Exception("Missing cache directory");

		return new FileInfo(Path.Combine(dir.FullName, hash));
	}

	public void DrawInfo()
	{
		if (ImGui.CollapsingHeader($"File Cache ({this.totalCacheSizeGb.ToString("F2")} Gb)###cacheSection"))
		{
			string cache = Configuration.Current.CacheDirectory ?? string.Empty;
			if (ImGui.InputText("Directory", ref cache))
			{
				Configuration.Current.CacheDirectory = cache;
				Configuration.Current.Save();
			}

			ImGui.Text($"{fileCount} files in cache");

			if (ImGui.Button("Clear"))
			{
				DirectoryInfo? dir = this.GetDirectory();
				dir?.Delete(true);
				dir?.Create();
				this.totalCacheSizeGb = 0;
				this.fileCount = 0;
			}
		}
	}

	private DirectoryInfo? GetDirectory()
	{
		if (string.IsNullOrEmpty(Configuration.Current.CacheDirectory))
			return null;

		DirectoryInfo dir;
		try
		{
			dir = new(Configuration.Current.CacheDirectory);

			if (!dir.Exists)
				dir.Create();

			return dir;
		}
		catch (Exception)
		{
		}

		return null;
	}

	private async Task CacheThread()
	{
		while (!this.tokenSource.IsCancellationRequested)
		{
			DirectoryInfo? dir = this.GetDirectory();
			long totalCacheSize = 0;
			if (dir != null && dir.Exists)
			{
				FileInfo[] files = dir.GetFiles();
				foreach (FileInfo file in files)
				{
					totalCacheSize += file.Length;
				}

				this.fileCount = files.Length;
			}

			this.totalCacheSizeGb = totalCacheSize / 1024.0f / 1024.0f / 1024.0f;

			await Task.Delay(3000);
		}
	}
}