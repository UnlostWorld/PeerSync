// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Glamourer;

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using PeerSync.UI;

public class GlamourerSync : SyncProviderBase
{
	private readonly GlamourerCommunicator glamourer = new();

	public override string DisplayName => "Glamourer";
	public override string Key => "g";

	public override async Task<string?> Serialize(Configuration.Character character, ushort objectIndex)
	{
		if (!this.glamourer.GetIsAvailable())
			return null;

		return await this.glamourer.GetState(objectIndex);
	}

	public override async Task Deserialize(
		string? lastContent,
		string? content,
		CharacterSync character,
		ushort objectIndex)
	{
		if (!this.glamourer.GetIsAvailable())
		{
			if (!string.IsNullOrEmpty(content))
				this.SetStatus(character, SyncProgressStatus.NotApplied);

			return;
		}

		if (lastContent == content)
			return;

		if (content == null)
		{
			await this.glamourer.RevertState(objectIndex);
			this.SetStatus(character, SyncProgressStatus.Empty);
		}
		else
		{
			await this.glamourer.SetState(objectIndex, content);
			this.SetStatus(character, SyncProgressStatus.Applied);
		}
	}

	public override async Task Reset(CharacterSync character, ushort? objectIndex)
	{
		if (objectIndex != null)
			await this.glamourer.RevertState(objectIndex.Value);

		this.SetStatus(character, SyncProgressStatus.Empty);
		await base.Reset(character, objectIndex);
	}

	public override void DrawInspect(CharacterSync? character, string content)
	{
		if (ImGui.CollapsingHeader(this.DisplayName))
		{
			// https://github.com/Ottermandias/Glamourer/blob/0a9693daea99f79c44b2a69e1bfb006573a721a0/Glamourer/Utility/CompressExtensions.cs#L33
			byte[] compressed = Convert.FromBase64String(content);
			byte ret = compressed[0];
			using MemoryStream compressedStream = new(compressed, 1, compressed.Length - 1);
			using GZipStream zipStream = new(compressedStream, CompressionMode.Decompress);
			using MemoryStream resultStream = new();
			zipStream.CopyTo(resultStream);
			byte[] decompressed = resultStream.ToArray();
			content = Encoding.UTF8.GetString(decompressed);

			ImGuiEx.JsonViewer("glamourerInspector", content);
		}
	}
}