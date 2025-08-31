// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;

namespace PeerSync.SyncProviders.Penumbra;

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

	private void OnModSettingsChanged(ModSettingChange change, Guid guid, string a, bool b)
	{
		if (change == ModSettingChange.TemporaryMod)
			return;

		Plugin.Log.Information("Settings changed");
		indexToRedirects.Clear();
	}

	public void Dispose()
	{
		this.gameObjectResourcePathResolved.Dispose();
		this.modSettingChanged.Dispose();
	}

	public ReadOnlyDictionary<string, string>? GetResources(int objectIndex)
	{
		Dictionary<string, string>? redirects = null;
		this.indexToRedirects.TryGetValue(objectIndex, out redirects);

		if (redirects == null)
			return null;

		return redirects.AsReadOnly();
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

		Dictionary<string, string>? redirects;
		if (!this.indexToRedirects.TryGetValue(objectIndex, out redirects))
		{
			redirects = new();
			this.indexToRedirects[objectIndex] = redirects;
		}

		redirects[gamePath] = redirectPath;
	}
}