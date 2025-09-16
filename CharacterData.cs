// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Newtonsoft.Json;
using PeerSync.SyncProviders;

public class CharacterData()
{
	[JsonProperty("F")]
	public string? Fingerprint { get; set; }

	[JsonProperty("C")]
	public Dictionary<string, string?> Character { get; init; } = new();

	[JsonProperty("M")]
	public Dictionary<string, string?> MountOrMinion { get; init; } = new();

	[JsonProperty("P")]
	public Dictionary<string, string?> Pet { get; init; } = new();

	public void Clear()
	{
		this.Character.Clear();
		this.MountOrMinion.Clear();
		this.Pet.Clear();
	}

	public void CopyTo(CharacterData other)
	{
		other.Fingerprint = this.Fingerprint;

		other.Clear();

		foreach ((string key, string? value) in this.Character)
		{
			other.Character.Add(key, value);
		}

		foreach ((string key, string? value) in this.MountOrMinion)
		{
			other.MountOrMinion.Add(key, value);
		}

		foreach ((string key, string? value) in this.Pet)
		{
			other.Pet.Add(key, value);
		}
	}

	public bool IsSame(CharacterData other)
	{
		if (other.Fingerprint != this.Fingerprint)
			return false;

		if (this.Character.Count != other.Character.Count)
			return false;

		if (this.MountOrMinion.Count != other.MountOrMinion.Count)
			return false;

		if (this.Pet.Count != other.Pet.Count)
			return false;

		foreach ((string key, string? value) in this.Character)
		{
			if (!other.Character.TryGetValue(key, out string? otherValue)
			|| otherValue != value)
			{
				return false;
			}
		}

		foreach ((string key, string? value) in this.MountOrMinion)
		{
			if (!other.MountOrMinion.TryGetValue(key, out string? otherValue)
			|| otherValue != value)
			{
				return false;
			}
		}

		foreach ((string key, string? value) in this.Pet)
		{
			if (!other.Pet.TryGetValue(key, out string? otherValue)
			|| otherValue != value)
			{
				return false;
			}
		}

		return true;
	}

	public void DrawInspect()
	{
		if (ImGui.BeginTabBar("###InspectorDataTabs"))
		{
			if (this.Character != null)
			{
				if (ImGui.BeginTabItem("Character Data"))
				{
					this.DrawData(this.Character);
					ImGui.EndTabItem();
				}
			}

			if (this.MountOrMinion != null && this.MountOrMinion.Count > 0)
			{
				if (ImGui.BeginTabItem("Mount Or Minion Data"))
				{
					this.DrawData(this.MountOrMinion);
					ImGui.EndTabItem();
				}
			}

			if (this.Pet != null && this.Pet.Count > 0)
			{
				if (ImGui.BeginTabItem("Pet Data"))
				{
					this.DrawData(this.Pet);
					ImGui.EndTabItem();
				}
			}
		}
	}

	private void DrawData(Dictionary<string, string?> data)
	{
		foreach ((string key, string? content) in data)
		{
			if (content == null)
				continue;

			SyncProviderBase? provider = Plugin.Instance?.GetSyncProvider(key);
			if (provider == null)
			{
				if (ImGui.CollapsingHeader(key))
				{
					ImGui.PushFont(UiBuilder.MonoFont);
					ImGui.TextWrapped(content);
					ImGui.PopFont();
				}
			}
			else
			{
				provider.DrawInspect(null, content);
			}
		}
	}
}