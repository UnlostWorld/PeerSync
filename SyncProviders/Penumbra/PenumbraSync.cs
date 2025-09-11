// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
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
	public byte lastQueueIndex = 0;

	private readonly Penumbra penumbra = new();
	private readonly ResourceMonitor resourceMonitor = new();
	private readonly Dictionary<string, Guid> appliedCollections = new();
	private readonly HashSet<int> hasSeenBefore = new();

	private readonly TransferGroup downloadGroup = new();
	private readonly TransferGroup uploadGroup = new();

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

	public PenumbraSync()
	{
		this.downloadGroup.SetCount(Configuration.Current.MaxDownloads);
		this.uploadGroup.SetCount(Configuration.Current.MaxUploads);
	}

	public override void GetDtrStatus(ref SeStringBuilder dtrEntryBuilder, ref SeStringBuilder dtrTooltipBuilder)
	{
		base.GetDtrStatus(ref dtrEntryBuilder, ref dtrTooltipBuilder);

		int downloads = this.downloadGroup.ActiveCount;
		int uploads = this.uploadGroup.ActiveCount;

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
			await Task.Delay(1000, this.CancellationToken);
			await Plugin.Framework.RunOnUpdate();
			this.hasSeenBefore.Add(objectIndex);
		}

		// Perform file hashing on a separate thread.
		await Plugin.Framework.RunOutsideUpdate();

		PenumbraData data = new();

		// Get file hashes
		data.Files = new();
		Dictionary<string, string>? liveResources = this.resourceMonitor.GetResources(objectIndex);
		if (liveResources != null)
		{
			Dictionary<string, string>? resources;
			lock (liveResources)
			{
				resources = new(liveResources);
			}

			foreach ((string gamePath, string redirectPath) in resources)
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

	public override async Task Deserialize(
		string? lastContent,
		string? content,
		CharacterSync character,
		ushort objectIndex)
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

			if (this.appliedCollections.TryGetValue(character.Pair.GetFingerprint(), out Guid existingCollectionId))
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
				FileDownload download = new(this, name, hash, expectedSize, character, this.CancellationToken);
				this.downloadGroup.Enqueue(download);
			}
			else
			{
				file.LastWriteTimeUtc = DateTime.UtcNow;
			}
		}

		// Wait for all downloads from the target character to complete...
		bool pending = true;
		while (pending)
		{
			pending = this.downloadGroup.IsCharacterPending(character);
			await Task.Delay(100, this.CancellationToken);
		}

		// Don't actually apply to test pairs.
		if (character.Pair.IsTestPair)
		{
			this.SetStatus(character, SyncProgressStatus.Applied);
			return;
		}

		if (this.CancellationToken.IsCancellationRequested)
			return;

		await Task.Delay(100, this.CancellationToken);

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
		if (!this.appliedCollections.ContainsKey(character.Pair.GetFingerprint()))
		{
			this.penumbra.CreateTemporaryCollection.Invoke(
				"PeerSync",
				character.Pair.GetFingerprint(),
				out collectionId).ThrowOnFailure();

			this.appliedCollections.Add(character.Pair.GetFingerprint(), collectionId);
		}
		else
		{
			collectionId = this.appliedCollections[character.Pair.GetFingerprint()];
		}

		try
		{
			this.penumbra.AssignTemporaryCollection.Invoke(
				collectionId,
				objectIndex,
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

			this.penumbra.RedrawObject.Invoke(objectIndex);
			this.SetStatus(character, SyncProgressStatus.Applied);
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error applying penumbra collection");
			this.SetStatus(character, SyncProgressStatus.Error);
		}
	}

	public override void DrawStatus()
	{
		base.DrawStatus();

		if (ImGui.CollapsingHeader($"Downloads ({this.downloadGroup.ActiveCount} / {this.downloadGroup.QueueCount})###DownloadsSection"))
		{
			int maxDownloads = Configuration.Current.MaxDownloads;
			if (ImGui.InputInt("Limit###LimitDownloads", ref maxDownloads))
			{
				Configuration.Current.MaxDownloads = maxDownloads;
				Configuration.Current.Save();

				this.downloadGroup.Cancel();
				this.downloadGroup.SetCount(maxDownloads);
			}

			this.downloadGroup.DrawStatus("DownloadTable");
		}

		if (ImGui.CollapsingHeader($"Uploads ({this.uploadGroup.ActiveCount} / {this.uploadGroup.QueueCount})###UploadsSection"))
		{
			int maxUploads = Configuration.Current.MaxUploads;
			if (ImGui.InputInt("Limit###LimitUploads", ref maxUploads))
			{
				Configuration.Current.MaxUploads = maxUploads;
				Configuration.Current.Save();

				this.uploadGroup.Cancel();
				this.uploadGroup.SetCount(maxUploads);
			}

			this.uploadGroup.DrawStatus("UploadTable");
		}

		this.fileCache.DrawInfo();
	}

	public override void Dispose()
	{
		base.Dispose();

		this.downloadGroup.Cancel();
		this.uploadGroup.Cancel();

		this.fileCache.Dispose();

		foreach ((string fingerprint, Guid guid) in this.appliedCollections)
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

		this.downloadGroup.Cancel();
		this.uploadGroup.Cancel();
	}


	private void OnFileRequest(Connection connection, byte clientQueueIndex, string hash)
	{
		CharacterSync? character = Plugin.Instance?.GetCharacterSync(connection);
		if (character == null)
		{
			Plugin.Log.Warning($"File request from unknown connection!");
			return;
		}

		FileUpload upload = new(this, clientQueueIndex, hash, character, this.CancellationToken);
		this.uploadGroup.Enqueue(upload);
	}
}

public class TransferGroup
{
	private readonly ConcurrentQueue<FileTransfer> pending = new();
	private readonly ConcurrentHashSet<FileTransfer> active = new();
	private CancellationTokenSource transferTaskTokenSource = new();

	public int ActiveCount => this.active.Count;
	public int QueueCount => this.pending.Count;

	public void Enqueue(FileTransfer transfer)
	{
		this.pending.Enqueue(transfer);
	}

	public void Cancel()
	{
		if (!this.transferTaskTokenSource.IsCancellationRequested)
		{
			this.transferTaskTokenSource.Cancel();
		}
	}

	public void SetCount(int count)
	{
		this.transferTaskTokenSource = new();
		for (int i = 0; i < count; i++)
		{
			Task.Run(TransferTask, this.transferTaskTokenSource.Token);
		}
	}

	public bool IsCharacterPending(CharacterSync character)
	{
		foreach (FileTransfer transfer in this.pending)
		{
			if (transfer.Character == character)
			{
				return true;
			}
		}

		foreach (FileTransfer transfer in this.active)
		{
			if (transfer.Character == character)
			{
				return true;
			}
		}

		return false;
	}

	public void DrawStatus(string label)
	{
		if (ImGui.BeginTable(label, 3))
		{
			ImGui.TableSetupColumn($"###{label}Name", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn($"###{label}Progress", ImGuiTableColumnFlags.WidthFixed);
			ImGui.TableSetupColumn($"###{label}Hover", ImGuiTableColumnFlags.WidthFixed);

			int rowIndex = 0;
			foreach (FileTransfer transfer in this.active)
			{
				rowIndex++;
				this.DrawTransferRow(transfer, rowIndex);
				ImGui.TableNextRow();
			}

			foreach (FileTransfer transfer in this.pending)
			{
				rowIndex++;
				this.DrawTransferRow(transfer, rowIndex);
				ImGui.TableNextRow();
			}

			ImGui.EndTable();
		}
	}

	private void DrawTransferRow(FileTransfer transfer, int rowIndex)
	{
		ImGui.TableNextColumn();
		ImGui.Text(transfer.Name);

		ImGui.TableNextColumn();

		if (transfer.Progress > 0 && transfer.Progress < 1)
			ImGuiEx.ThinProgressBar(transfer.Progress);

		ImGui.TableNextColumn();
		ImGui.Selectable(
			$"##TransferProgressRowSelector{rowIndex}",
			false,
			ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.Disabled);

		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.BeginTooltip();

			ImGui.Text(transfer.Name);

			ImGui.Text($"{(transfer.Progress * 100).ToString("F0")}%");
			ImGui.Text($"{transfer.Character.Pair.CharacterName} @ {transfer.Character.Pair.World}");

			ImGui.EndTooltip();
		}
	}


	private async Task TransferTask()
	{
		while (!this.transferTaskTokenSource.IsCancellationRequested)
		{
			await Task.Delay(100, this.transferTaskTokenSource.Token);

			if (!this.pending.TryDequeue(out FileTransfer? transfer) || transfer == null)
				continue;

			this.active.Add(transfer);

			await transfer.TransferSafe();

			this.active.TryRemove(transfer);
		}
	}
}