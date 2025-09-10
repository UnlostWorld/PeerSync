// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

	public static object QueueLock = new();
	public static int ActiveTransfers = 0;

	public readonly FileCache fileCache = new();
	public readonly ConcurrentHashSet<FileTransfer> transfers = new();
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

		int downloads = this.GetActiveTransfers<FileDownload>();
		int uploads = this.GetActiveTransfers<FileUpload>();

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
		Dictionary<string, string>? resources = this.resourceMonitor.GetResources(objectIndex);
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
				new FileDownload(this, name, hash, expectedSize, character, this.CancellationToken);
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
			foreach (FileTransfer transfer in this.transfers)
			{
				if (transfer.Character == character
					&& transfer is FileDownload
					&& !transfer.IsComplete)
				{
					done = false;
					break;
				}
			}

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

	public int GetActiveTransfers<T>()
		where T : FileTransfer
	{
		int count = 0;
		foreach (FileTransfer transfer in this.transfers)
		{
			if (transfer.IsWaiting)
				continue;

			if (transfer is not T)
				continue;

			count++;
		}

		return count;
	}

	public override void DrawStatus()
	{
		base.DrawStatus();

		string transferStatus = string.Empty;
		if (this.transfers.Count > 0)
			transferStatus = $" (↓{this.GetActiveTransfers<FileDownload>()} ↑{this.GetActiveTransfers<FileUpload>()} / {this.transfers.Count})";

		if (ImGui.CollapsingHeader($"Transfers{transferStatus}###transfersSection"))
		{
			int maxDownloads = Configuration.Current.MaxDownloads;
			if (ImGui.InputInt("Limit Downloads###Downloads", ref maxDownloads))
			{
				Configuration.Current.MaxDownloads = maxDownloads;
				Configuration.Current.Save();
			}

			int maxUploads = Configuration.Current.MaxUploads;
			if (ImGui.InputInt("Limit Uploads###Uploads", ref maxUploads))
			{
				Configuration.Current.MaxUploads = maxUploads;
				Configuration.Current.Save();
			}

			int maxTransfers = Configuration.Current.MaxTransfers;
			if (ImGui.InputInt("Limit Total###Total", ref maxTransfers))
			{
				Configuration.Current.MaxTransfers = maxTransfers;
				Configuration.Current.Save();
			}

			if (ImGui.BeginTable("TransferProgressTable", 4))
			{
				ImGui.TableSetupColumn("###TransferProgressTableDirection", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("###TransferProgressTableName", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("###TransferProgressTableProgress", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("###TransferProgressTableHover", ImGuiTableColumnFlags.WidthFixed);

				int downloadIndex = 0;
				foreach (FileTransfer transfer in this.transfers)
				{
					downloadIndex++;

					ImGui.TableNextColumn();
					ImGuiEx.Icon(transfer.Icon);

					ImGui.TableNextColumn();
					ImGui.Text(transfer.Name);

					ImGui.TableNextColumn();

					if (!transfer.IsWaiting)
						ImGuiEx.ThinProgressBar(transfer.Progress);

					ImGui.TableNextColumn();
					ImGui.Selectable(
						$"##TransferProgressRowSelector{downloadIndex}",
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

		new FileUpload(this, clientQueueIndex, hash, character, this.CancellationToken);
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

		foreach (FileTransfer transfer in this.transfers)
		{
			transfer.Dispose();
		}

		this.transfers.Clear();
	}
}