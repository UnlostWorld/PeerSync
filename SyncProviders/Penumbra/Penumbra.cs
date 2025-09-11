// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using System;
using global::Penumbra.Api.IpcSubscribers;

public class Penumbra
{
	public readonly GetEnabledState GetEnabledState = new(Plugin.PluginInterface);
	public readonly GetMetaManipulations GetMetaManipulations = new(Plugin.PluginInterface);
	public readonly CreateTemporaryCollection CreateTemporaryCollection = new(Plugin.PluginInterface);
	public readonly AssignTemporaryCollection AssignTemporaryCollection = new(Plugin.PluginInterface);
	public readonly RemoveTemporaryMod RemoveTemporaryMod = new(Plugin.PluginInterface);
	public readonly AddTemporaryMod AddTemporaryMod = new(Plugin.PluginInterface);
	public readonly RedrawObject RedrawObject = new(Plugin.PluginInterface);
	public readonly DeleteTemporaryCollection DeleteTemporaryCollection = new(Plugin.PluginInterface);

	public bool GetIsAvailable()
	{
		try
		{
			return this.GetEnabledState.Invoke();
		}
		catch (Exception)
		{
			return false;
		}
	}
}
