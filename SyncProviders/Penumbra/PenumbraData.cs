// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System.Collections.Generic;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using PeerSync.UI;

public class PenumbraData
{
	public Dictionary<string, string> Files { get; set; } = new();
	public Dictionary<string, long> FileSizes { get; set; } = new();
	public Dictionary<string, string> Redirects { get; set; } = new();
	public string? MetaManipulations { get; set; }

	public bool IsSame(PenumbraData other)
	{
		if (this.Files.Count != other.Files.Count)
			return false;

		if (this.FileSizes.Count != other.FileSizes.Count)
			return false;

		if (this.Redirects.Count != other.Redirects.Count)
			return false;

		if (this.MetaManipulations != other.MetaManipulations)
			return false;

		foreach ((string key, string value) in this.Files)
		{
			if (!other.Files.TryGetValue(key, out string? otherValue)
			|| otherValue != value)
			{
				Plugin.Log.Info("files");
				return false;
			}
		}

		foreach ((string key, long value) in this.FileSizes)
		{
			if (!other.FileSizes.TryGetValue(key, out long otherValue)
			|| otherValue != value)
			{
				return false;
			}
		}

		foreach ((string key, string value) in this.Redirects)
		{
			if (!other.Redirects.TryGetValue(key, out string? otherValue)
			|| otherValue != value)
			{
				return false;
			}
		}

		return true;
	}

	public void DrawInspect(PenumbraSync sync)
	{
		if (ImGui.CollapsingHeader("Penumbra Files"))
		{
			if (ImGui.BeginTable("PenumbraFileInspector", 4, ImGuiTableFlags.Sortable))
			{
				ImGui.TableSetupColumn("Game Path", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("Hash", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableSetupColumn("Cache", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableHeadersRow();

				int sortColumn = 0;
				ImGuiSortDirection sortDirection = ImGuiSortDirection.Ascending;

				ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();
				if (!sortSpecs.IsNull)
				{
					sortColumn = sortSpecs.Specs.ColumnIndex;
					sortDirection = sortSpecs.Specs.SortDirection;
				}

				List<string> gamePathsSorted = new(this.Files.Keys);

				// Sort by GamePath
				if (sortColumn == 0)
				{
					gamePathsSorted.Sort();
				}

				// Sort by Hash
				else if (sortColumn == 1)
				{
					gamePathsSorted.Sort((string a, string b) =>
					{
						string? hashA;
						if (!this.Files.TryGetValue(a, out hashA))
							return 0;

						string? hashB;
						if (!this.Files.TryGetValue(b, out hashB))
							return 0;

						return hashA.CompareTo(hashB);
					});
				}

				// sort by size
				else if (sortColumn == 2)
				{
					gamePathsSorted.Sort((string a, string b) =>
					{
						string? hashA;
						if (!this.Files.TryGetValue(a, out hashA))
							return 0;

						long sizeA = 0;
						this.FileSizes.TryGetValue(hashA, out sizeA);

						string? hashB;
						if (!this.Files.TryGetValue(b, out hashB))
							return 0;

						long sizeB = 0;
						this.FileSizes.TryGetValue(hashB, out sizeB);

						return sizeA.CompareTo(sizeB);
					});
				}

				// sort by cache status
				else if (sortColumn == 3)
				{
					gamePathsSorted.Sort((string a, string b) =>
					{
						string? hashA;
						if (!this.Files.TryGetValue(a, out hashA))
							return 0;

						FileInfo? fileInfoA = sync.FileCache.GetFile(hashA);
						if (fileInfoA == null)
							return 0;

						string? hashB;
						if (!this.Files.TryGetValue(b, out hashB))
							return 0;

						FileInfo? fileInfoB = sync.FileCache.GetFile(hashB);
						if (fileInfoB == null)
							return 0;

						return fileInfoA.Exists.CompareTo(fileInfoB.Exists);
					});
				}

				if (sortDirection != ImGuiSortDirection.Ascending)
					gamePathsSorted.Reverse();

				foreach (string gamePath in gamePathsSorted)
				{
					ImGui.TableNextRow();

					string? hash;
					if (!this.Files.TryGetValue(gamePath, out hash))
						continue;

					long fileSize = 0;
					this.FileSizes.TryGetValue(hash, out fileSize);

					ImGui.TableNextColumn();
					ImGui.Text(gamePath);

					ImGui.TableNextColumn();
					ImGui.Text(hash);

					ImGui.TableNextColumn();
					ImGuiEx.Size(fileSize);

					ImGui.TableNextColumn();
					FileInfo? fileInfo = sync.FileCache.GetFile(hash);
					if (fileInfo != null && fileInfo.Exists)
					{
						ImGuiEx.Icon(FontAwesomeIcon.Check);
					}
					else
					{
						ImGuiEx.Icon(FontAwesomeIcon.Times);
					}

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}
		}

		if (ImGui.CollapsingHeader("Penumbra Redirects"))
		{
			if (ImGui.BeginTable("PenumbraRedirectInspector", 2, ImGuiTableFlags.Sortable))
			{
				ImGui.TableSetupColumn("Game Path", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("Redirect Path", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableHeadersRow();

				int sortColumn = 0;
				ImGuiSortDirection sortDirection = ImGuiSortDirection.Ascending;

				ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();
				if (!sortSpecs.IsNull)
				{
					sortColumn = sortSpecs.Specs.ColumnIndex;
					sortDirection = sortSpecs.Specs.SortDirection;
				}

				List<string> gamePathsSorted = new(this.Redirects.Keys);

				// Sort by GamePath
				if (sortColumn == 0)
				{
					gamePathsSorted.Sort();
				}

				// Sort by Redirect
				else if (sortColumn == 1)
				{
					gamePathsSorted.Sort((string a, string b) =>
					{
						string? redirectA;
						if (!this.Redirects.TryGetValue(a, out redirectA))
							return 0;

						string? redirectB;
						if (!this.Redirects.TryGetValue(b, out redirectB))
							return 0;

						return redirectA.CompareTo(redirectB);
					});
				}

				if (sortDirection != ImGuiSortDirection.Ascending)
					gamePathsSorted.Reverse();

				foreach (string gamePath in gamePathsSorted)
				{
					ImGui.TableNextRow();

					string? redirectPath;
					if (!this.Redirects.TryGetValue(gamePath, out redirectPath))
						continue;

					ImGui.TableNextColumn();
					ImGui.Text(gamePath);

					ImGui.TableNextColumn();
					ImGui.Text(redirectPath);

					ImGui.TableNextRow();
				}

				ImGui.EndTable();
			}
		}

		if (ImGui.CollapsingHeader("Penumbra Meta Manipulations"))
		{
			ImGui.Text(this.MetaManipulations);
		}
	}
}