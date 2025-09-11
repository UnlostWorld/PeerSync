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
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using global::Penumbra.Api.Enums;
using global::Penumbra.Api.Helpers;
using global::Penumbra.Api.IpcSubscribers;

public class ResourceMonitor : IDisposable
{
	private readonly EventSubscriber<nint, string, string> gameObjectResourcePathResolved;
	private readonly EventSubscriber<ModSettingChange, Guid, string, bool> modSettingChanged;

	private readonly Dictionary<int, Dictionary<string, string>> indexToRedirects = new();

	public ResourceMonitor()
	{
		this.gameObjectResourcePathResolved = GameObjectResourcePathResolved.Subscriber(
			Plugin.PluginInterface,
			this.OnGameObjectResourcePathResolved);

		this.modSettingChanged = ModSettingChanged.Subscriber(
			Plugin.PluginInterface,
			this.OnModSettingsChanged);
	}

	public void Dispose()
	{
		this.gameObjectResourcePathResolved.Dispose();
		this.modSettingChanged.Dispose();
	}

	public Dictionary<string, string>? GetResources(int objectIndex)
	{
		Dictionary<string, string>? redirects = null;
		this.indexToRedirects.TryGetValue(objectIndex, out redirects);

		if (redirects == null)
			return null;

		return redirects;
	}

	private void OnGameObjectResourcePathResolved(IntPtr ptr, string gamePath, string redirectPath)
	{
		if (ptr == IntPtr.Zero)
			return;

		// Unsure why, but some redirect paths have some sort of Id at the stat of them...
		string[] parts = redirectPath.Split("|");
		if (parts.Length == 3)
			redirectPath = parts[2];

		redirectPath = redirectPath.Replace('\\', '/');
		if (gamePath == redirectPath)
			return;

		int objectIndex = 0;
		unsafe
		{
			GameObject* pGameObject = (GameObject*)ptr;
			if (pGameObject == null)
				return;

			objectIndex = pGameObject->ObjectIndex;
		}

		Dictionary<string, string>? resources;
		if (!this.indexToRedirects.TryGetValue(objectIndex, out resources))
		{
			resources = new();
			this.indexToRedirects[objectIndex] = resources;
		}

		lock (resources)
		{
			resources[gamePath] = redirectPath;
		}
	}

	private void OnModSettingsChanged(ModSettingChange change, Guid guid, string a, bool b)
	{
		if (change == ModSettingChange.TemporaryMod)
			return;

		Plugin.Log.Information("Settings changed");
		this.indexToRedirects.Clear();
	}
}