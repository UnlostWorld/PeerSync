// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using global::Penumbra.Api.Enums;
using global::Penumbra.Api.Helpers;
using global::Penumbra.Api.IpcSubscribers;
using Newtonsoft.Json;
using PeerSync.UI;

public class CharacterCache : IDisposable
{
	private readonly EventSubscriber<ModSettingChange, Guid, string, bool> modSettingChanged;

	public CharacterCache()
	{
		this.modSettingChanged = ModSettingChanged.Subscriber(
			Plugin.PluginInterface,
			this.OnModSettingsChanged);
	}

	public Dictionary<string, string> GetRedirects(string fingerprint)
	{
		DirectoryInfo? dir = this.GetDirectory();
		if (dir == null)
			return new();

		string filePath = $"{dir.FullName}/{fingerprint}.json";
		if (!File.Exists(filePath))
			return new();

		string json = File.ReadAllText(filePath);
		Dictionary<string, string>? result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
		if (result == null)
			return new();

		return result;
	}

	public void SetRedirects(string fingerprint, Dictionary<string, string> redirects)
	{
		DirectoryInfo? dir = this.GetDirectory();
		if (dir == null)
			return;

		string filePath = $"{dir.FullName}/{fingerprint}.json";
		string json = JsonConvert.SerializeObject(redirects, Formatting.Indented);
		File.WriteAllText(filePath, json);
	}

	public void Dispose()
	{
	}

	public void DrawInfo()
	{
		ImGui.Text("Character Cache:");

		if (ImGui.Button("Flush##Characters"))
		{
			DialogBox.Show(
				"Confirm",
				"Are you sure you want to flush the character cache?",
				FontAwesomeIcon.Question,
				0xFF0080FF,
				"Flush",
				"Cancel",
				() =>
				{
					DirectoryInfo? dir = this.GetDirectory();
					if (dir == null)
						return;

					dir.Delete(true);
				},
				null);
		}
	}

	private DirectoryInfo? GetDirectory()
	{
		DirectoryInfo dir = Plugin.PluginInterface.ConfigDirectory;

		try
		{
			dir = new(dir.FullName + "/Characters/");

			if (!dir.Exists)
				dir.Create();

			return dir;
		}
		catch (Exception)
		{
		}

		return null;
	}

	private void OnModSettingsChanged(ModSettingChange change, Guid guid, string a, bool b)
	{
		if (change == ModSettingChange.TemporaryMod)
			return;

		DirectoryInfo? dir = this.GetDirectory();
		if (dir == null)
			return;

		dir.Delete(true);
	}
}