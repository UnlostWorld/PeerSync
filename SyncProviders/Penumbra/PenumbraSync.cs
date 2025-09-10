// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ConcurrentCollections;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Newtonsoft.Json;
using PeerSync.Network;
using PeerSync.UI;

namespace PeerSync.SyncProviders.Penumbra;

public class PenumbraSync : SyncProviderBase<PenumbraProgress>
{
	public const int FileTimeout = 240_000;
	public const int FileChunkSize = 1024 * 128; // 128kb chunks

	public readonly FileCache fileCache = new();
	public readonly ConcurrentHashSet<FileDownload> downloads = new();
	public readonly ConcurrentHashSet<FileUpload> uploads = new();
	public byte lastQueueIndex = 0;

	private readonly Penumbra penumbra = new();
	private readonly ResourceMonitor resourceMonitor = new();
	private readonly Dictionary<string, Guid> appliedCollections = new();
	private readonly HashSet<int> hasSeenBefore = new();

	public override string DisplayName => "Penumbra";
	public override string Key => "p";

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

	public override void GetDtrStatus(ref SeStringBuilder dtrEntryBuilder, ref SeStringBuilder dtrTooltipBuilder)
	{
		base.GetDtrStatus(ref dtrEntryBuilder, ref dtrTooltipBuilder);

		int downloads = this.GetActiveDownloadCount();
		int uploads = this.GetActiveUploadCount();

		if (downloads > 0)
		{
			dtrEntryBuilder.AddText("↓");
		}

		if (uploads > 0)
		{
			dtrEntryBuilder.AddText("↑");
		}
	}

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

	private void OnReceived(Connection connection, byte typeId, byte[] data)
	{
		if (typeId == Objects.FileRequest)
		{
			byte clientQueueIndex = data[0];

			byte[] hashData = new byte[data.Length - 1];
			Array.Copy(data, 1, hashData, 0, data.Length - 1);

			string hash = Encoding.UTF8.GetString(hashData);
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
		await Plugin.Framework.RunOnUpdate();

		if (!penumbra.GetIsAvailable())
			return null;

		if (!this.hasSeenBefore.Contains(objectIndex))
		{
			this.penumbra.RedrawObject.Invoke(objectIndex);
			await Task.Delay(1000);
			await Plugin.Framework.RunOnUpdate();
			this.hasSeenBefore.Add(objectIndex);
		}

		// Perform file hashing on a separate thread.
		await Plugin.Framework.RunOutsideUpdate();

		PenumbraData data = new();

		// Get file hashes
		data.Files = new();
		ReadOnlyDictionary<string, string>? resources = this.resourceMonitor.GetResources(objectIndex);
		if (resources != null)
		{
			foreach ((string gamePath, string redirectPath) in resources.AsReadOnly())
			{
				bool isFilePath = Path.IsPathRooted(redirectPath);
				if (isFilePath)
				{
					bool found = this.fileCache.GetFileHash(redirectPath, out string hash, out long fileSize);
					if (!found)
					{
						Plugin.Log.Warning($"File not found for sync: {redirectPath}");
						continue;
					}

					data.Files[gamePath] = hash;
					data.FileSizes[hash] = fileSize;
				}
				else
				{
					data.Redirects[gamePath] = redirectPath;
				}
			}
		}

		await Plugin.Framework.RunOnUpdate();


		// get meta manipulations
		data.MetaManipulations = this.penumbra.GetMetaManipulations.Invoke(objectIndex);

		return JsonConvert.SerializeObject(data);
	}

	public override async Task Deserialize(string? lastContent, string? content, CharacterSync character)
	{
		if (!penumbra.GetIsAvailable())
		{
			if (!string.IsNullOrEmpty(content))
				this.SetStatus(character, SyncProgressStatus.NotApplied);

			return;
		}

		if (!this.fileCache.IsValid())
			return;

		if (lastContent == content)
			return;

		if (content == null)
		{
			await Plugin.Framework.RunOnUpdate();

			if (this.appliedCollections.TryGetValue(character.Pair.GetIdentifier(), out Guid existingCollectionId))
			{
				this.penumbra.DeleteTemporaryCollection.Invoke(existingCollectionId);
			}

			this.SetStatus(character, SyncProgressStatus.Empty);
			return;
		}

		await Plugin.Framework.RunOutsideUpdate();

		PenumbraData? lastData = null;
		if (lastContent != null)
			lastData = JsonConvert.DeserializeObject<PenumbraData>(lastContent);

		PenumbraData? data = JsonConvert.DeserializeObject<PenumbraData>(content);
		if (data == null)
			return;

		if (lastData != null && data.IsSame(lastData))
			return;

		this.SetStatus(character, SyncProgressStatus.Syncing);

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
			else
			{
				file.LastWriteTimeUtc = DateTime.UtcNow;
			}
		}

		// Wait for all downloads from the target character to complete...
		bool done = false;
		while (!done)
		{
			done = true;
			foreach (FileDownload download in this.downloads)
			{
				if (download.Character == character && !download.IsComplete)
				{
					done = false;
					break;
				}
			}

			await Task.Delay(100);
		}

		// Don't actually apply to test pairs.
		if (character.Pair.IsTestPair)
		{
			this.SetStatus(character, SyncProgressStatus.Applied);
			return;
		}

		Dictionary<string, string> paths = new();
		foreach ((string gamePath, string hash) in data.Files)
		{
			FileInfo? file = this.fileCache.GetFile(hash);

			// Verify that we did get all the files we need.
			if (file == null || !file.Exists)
			{
				Plugin.Log.Error($"Failed to download file: {gamePath} ({hash})");
				this.SetStatus(character, SyncProgressStatus.Error);
				return;
			}

			paths[gamePath] = file.FullName;
		}

		foreach ((string gamePath, string redirectPath) in data.Redirects)
		{
			paths[gamePath] = redirectPath;
		}

		await Plugin.Framework.RunOnUpdate();

		Guid collectionId;
		if (!this.appliedCollections.ContainsKey(character.Pair.GetIdentifier()))
		{
			this.penumbra.CreateTemporaryCollection.Invoke(
				"PeerSync",
				character.Pair.GetIdentifier(),
				out collectionId).ThrowOnFailure();

			this.appliedCollections.Add(character.Pair.GetIdentifier(), collectionId);
		}
		else
		{
			collectionId = this.appliedCollections[character.Pair.GetIdentifier()];
		}

		try
		{
			this.penumbra.AssignTemporaryCollection.Invoke(
				collectionId,
				character.ObjectTableIndex,
				true).ThrowOnFailure();

			this.penumbra.RemoveTemporaryMod.Invoke(
				"PeerSync",
				collectionId, 0).ThrowOnFailure();

			this.penumbra.AddTemporaryMod.Invoke(
				"PeerSync",
				collectionId,
				paths,
				data.MetaManipulations ?? string.Empty,
				0).ThrowOnFailure();

			this.penumbra.RedrawObject.Invoke(character.ObjectTableIndex);
			this.SetStatus(character, SyncProgressStatus.Applied);
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error applying penumbra collection");
			this.SetStatus(character, SyncProgressStatus.Error);
		}
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

		if (ImGui.CollapsingHeader($"Downloads ({this.GetActiveDownloadCount()} / {this.downloads.Count})###downloadsSection"))
		{
			ImGui.SetNextItemWidth(50);

			int maxDownloads = Configuration.Current.MaxConcurrentDownloads;
			if (ImGui.InputInt("Simultaneous Downloads", ref maxDownloads))
			{
				Configuration.Current.MaxConcurrentDownloads = maxDownloads;
				Configuration.Current.Save();
			}

			if (ImGui.BeginTable("DownloadProgressTable", 3))
			{
				ImGui.TableSetupColumn("###DownloadProgressTableName", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("###DownloadProgressTableProgress", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("###DownloadProgressTableHover", ImGuiTableColumnFlags.WidthFixed);

				int downloadIndex = 0;
				foreach (FileDownload download in this.downloads)
				{
					downloadIndex++;

					ImGui.TableNextColumn();
					ImGui.Text(download.Name);

					ImGui.TableNextColumn();

					if (!download.IsWaiting)
						ImGuiEx.ThinProgressBar(download.Progress);

					ImGui.TableNextColumn();
					ImGui.Selectable(
						$"##DownloadProgressRowSelector{downloadIndex}",
						false,
						ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

					if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
					{
						ImGui.BeginTooltip();

						ImGui.Text(download.Name);

						ImGui.Text($"{(download.Progress * 100).ToString("F0")}% ({download.BytesReceived / 1024} kb / {download.BytesToReceive / 1024} kb)");
						ImGui.Text($"{download.Character.Pair.CharacterName} @ {download.Character.Pair.World}");

						ImGui.EndTooltip();
					}

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}
		}

		if (ImGui.CollapsingHeader($"Uploads ({this.GetActiveUploadCount()} / {this.uploads.Count})###uploadsSection"))
		{
			ImGui.SetNextItemWidth(50);
			int maxUploads = Configuration.Current.MaxConcurrentUploads;
			if (ImGui.InputInt("Simultaneous Uploads", ref maxUploads))
			{
				Configuration.Current.MaxConcurrentUploads = maxUploads;
				Configuration.Current.Save();
			}

			if (ImGui.BeginTable("UploadProgressTable", 3))
			{
				ImGui.TableSetupColumn("###UploadProgressTableName", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("###UploadProgressTableProgress", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("###UploadProgressTableHover", ImGuiTableColumnFlags.WidthFixed);

				int uploadIndex = 0;
				foreach (FileUpload upload in this.uploads)
				{
					uploadIndex++;

					ImGui.TableNextColumn();
					ImGui.Text(upload.Name);

					ImGui.TableNextColumn();

					if (!upload.IsWaiting)
						ImGuiEx.ThinProgressBar(upload.Progress);

					ImGui.TableNextColumn();
					ImGui.Selectable(
						$"##UploadProgressRowSelector{uploadIndex}",
						false,
						ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

					if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
					{
						ImGui.BeginTooltip();

						ImGui.Text(upload.Name);

						ImGui.Text($"{(upload.Progress * 100).ToString("F0")}% ({upload.BytesSent / 1024} kb / {upload.BytesToSend / 1024} kb)");
						ImGui.Text($"{upload.Character.Pair.CharacterName} @ {upload.Character.Pair.World}");

						ImGui.EndTooltip();
					}

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}
		}

		this.fileCache.DrawInfo();
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

		this.fileCache.Dispose();

		foreach ((string identifier, Guid guid) in this.appliedCollections)
		{
			try
			{
				this.penumbra.DeleteTemporaryCollection.Invoke(guid);
			}
			catch (Exception)
			{
			}
		}

		this.appliedCollections.Clear();

		this.resourceMonitor.Dispose();

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
}