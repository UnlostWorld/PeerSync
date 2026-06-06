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
using PeerSync.Connections;
using PeerSync.UI;

public class GlamourerSync : SyncProviderBase
{
	private readonly GlamourerCommunicator glamourer = new();

	public override string DisplayName => "Glamourer";
	public override string Key => "g";

	public override async Task<string?> Serialize(Configuration.Character character, ushort objectIndex)
	{
		await Plugin.Framework.RunOnUpdate();

		if (!this.glamourer.GetIsAvailable())
			return null;

		return this.glamourer.GetState(objectIndex);
	}

	public override SyncProgressStatus Apply(
		string? lastContent,
		string? content,
		CharacterConnection character,
		ushort objectIndex)
	{
		if (!this.glamourer.GetIsAvailable())
		{
			return SyncProgressStatus.NotApplied;
		}

		if (content == null)
		{
			this.glamourer.RevertState(objectIndex);
			return SyncProgressStatus.Empty;
		}
		else
		{
			this.glamourer.SetState(objectIndex, content);
			return SyncProgressStatus.Applied;
		}
	}

	public override void Reset(CharacterConnection character, ushort? objectIndex)
	{
		if (objectIndex != null)
		{
			this.glamourer.RevertState(objectIndex.Value);
		}
	}

	public override void DrawInspect(CharacterConnection? character, string content)
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