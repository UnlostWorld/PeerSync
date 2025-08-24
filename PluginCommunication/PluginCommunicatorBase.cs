// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace PeerSync.PluginCommunication;

public abstract class PluginCommunicatorBase
{
	protected abstract string InternalName { get; }
	protected abstract Version Version { get; }

	public bool GetIsAvailable()
	{
		foreach (IExposedPlugin plugin in Plugin.PluginInterface.InstalledPlugins)
		{
			if (plugin.InternalName == this.InternalName && plugin.Version >= this.Version)
			{
				return true;
			}
		}

		return false;
	}

	protected TReturn? Invoke<TReturn, T1>(string name, T1 arg1)
	{
		try
		{
			ICallGateSubscriber<T1, TReturn> subscriber =
				Plugin.PluginInterface.GetIpcSubscriber<T1, TReturn>(name);

			TReturn ret = subscriber.InvokeFunc(arg1);
			return ret;
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error invoking IPC");
			return default;
		}
	}

	protected TReturn? Invoke<TReturn>(string name)
	{
		try
		{
			ICallGateSubscriber<TReturn> subscriber = Plugin.PluginInterface.GetIpcSubscriber<TReturn>(name);
			TReturn ret = subscriber.InvokeFunc();
			return ret;
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error invoking IPC");
			return default;
		}
	}

	protected async Task<TReturn?> InvokeAsync<TReturn>(string name)
	{
		try
		{
			ICallGateSubscriber<Task<TReturn>> subscriber = Plugin.PluginInterface.GetIpcSubscriber<Task<TReturn>>(name);
			TReturn ret = await subscriber.InvokeFunc();
			return ret;
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Error invoking IPC");
			return default;
		}
	}
}
