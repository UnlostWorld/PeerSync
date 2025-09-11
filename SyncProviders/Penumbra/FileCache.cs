// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using PeerSync;
using PeerSync.UI;

public enum FileDeletionReasons
{
	None,
	Age,
	Hash,
}

public class FileCache : IDisposable
{
	private const int ScanDelay = 10 * 60 * 1000; // every 10 minutes after boot.

	private readonly CancellationTokenSource tokenSource = new();
	private readonly Dictionary<string, FileInfo> hashToFileLookup = new();
	private readonly Dictionary<FileInfo, FileDeletionReasons> deletedFiles = new();

	private float totalCacheSizeGb = 0;
	private int fileCount = 0;
	private int scanCount = 0;

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
		string cacheSizeStr = this.totalCacheSizeGb.ToString("F2") + "Gb";
		float p = (float)this.scanCount / (float)this.fileCount;

		if (p < 1)
			cacheSizeStr = "Scanning...";

		if (ImGui.CollapsingHeader($"File Cache ({cacheSizeStr})###cacheSection"))
		{
			string cache = Configuration.Current.CacheDirectory ?? string.Empty;
			if (ImGui.InputText("Directory", ref cache, 512, ImGuiInputTextFlags.EnterReturnsTrue))
			{
				Configuration.Current.CacheDirectory = cache;
				Configuration.Current.Save();
			}

			if (p < 1)
			{
				ImGui.BeginGroup();
				ImGui.Text("Scanning");
				ImGui.SameLine();
				ImGuiEx.ThinProgressBar(p, -1);
				ImGui.EndGroup();

				if (ImGui.IsItemHovered())
				{
					ImGui.BeginTooltip();
					ImGui.Text($"Scanning {this.scanCount} of {this.fileCount} files in cache");
					ImGui.EndTooltip();
				}
			}

			lock (this.deletedFiles)
			{
				if (this.deletedFiles.Count > 0)
				{
					ImGui.Text("Removed:");
					if (this.deletedFiles.Count < 256)
					{
						foreach ((FileInfo file, FileDeletionReasons reason) in this.deletedFiles)
						{
							if (reason == FileDeletionReasons.Age)
							{
								ImGuiEx.Icon(FontAwesomeIcon.Clock);
							}
							else
							{
								ImGuiEx.Icon(FontAwesomeIcon.ExclamationTriangle);
							}

							ImGui.SameLine();
							ImGui.Text(file.Name);
						}
					}
					else
					{
						ImGui.SameLine();
						ImGui.Text($"{this.deletedFiles.Count} files from cache");
					}
				}
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
				this.fileCount = files.Length;
				this.scanCount = 0;

				this.deletedFiles.Clear();

				foreach (FileInfo file in files)
				{
					if (this.tokenSource.IsCancellationRequested)
						return;

					this.scanCount++;
					this.GetFileHash(file.FullName, out string hash, out long size);

					if (file.Name != hash)
					{
						Plugin.Log.Warning($"Incorrect file hash! Expected: {file.Name}, got: {hash}");
						this.deletedFiles.Add(file, FileDeletionReasons.Hash);
						continue;
					}

					TimeSpan age = DateTime.UtcNow - file.LastWriteTimeUtc;
					if (age.TotalDays > 30)
					{
						this.deletedFiles.Add(file, FileDeletionReasons.Age);
						continue;
					}

					totalCacheSize += size;
				}

				foreach (FileInfo file in this.deletedFiles.Keys)
				{
					try
					{
						file.Delete();
					}
					catch (Exception ex)
					{
						Plugin.Log.Warning(ex, $"Failed to delete file: {file.FullName}");
					}
				}

				this.totalCacheSizeGb = totalCacheSize / 1024.0f / 1024.0f / 1024.0f;
			}

			await Task.Delay(ScanDelay);
		}
	}
}