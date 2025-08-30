// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace PeerSync.SyncProviders;

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

	protected TReturn? InvokeFunc<TReturn, T1, T2, T3, T4, T5>(string name, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
		=> Plugin.PluginInterface.GetIpcSubscriber<T1, T2, T3, T4, T5, TReturn>(name).InvokeFunc(arg1, arg2, arg3, arg4, arg5);

	protected TReturn? InvokeFunc<TReturn, T1, T2, T3, T4>(string name, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
		=> Plugin.PluginInterface.GetIpcSubscriber<T1, T2, T3, T4, TReturn>(name).InvokeFunc(arg1, arg2, arg3, arg4);

	protected TReturn? InvokeFunc<TReturn, T1, T2, T3>(string name, T1 arg1, T2 arg2, T3 arg3)
		=> Plugin.PluginInterface.GetIpcSubscriber<T1, T2, T3, TReturn>(name).InvokeFunc(arg1, arg2, arg3);

	protected TReturn? InvokeFunc<TReturn, T1, T2>(string name, T1 arg1, T2 arg2)
		=> Plugin.PluginInterface.GetIpcSubscriber<T1, T2, TReturn>(name).InvokeFunc(arg1, arg2);

	protected TReturn? InvokeFunc<TReturn, T1>(string name, T1 arg1)
		=> Plugin.PluginInterface.GetIpcSubscriber<T1, TReturn>(name).InvokeFunc(arg1);

	protected TReturn? InvokeFunc<TReturn>(string name)
		=> Plugin.PluginInterface.GetIpcSubscriber<TReturn>(name).InvokeFunc();

	protected void InvokeAction<T1, T2>(string name, T1 arg1, T2 arg2)
		=> Plugin.PluginInterface.GetIpcSubscriber<T1, T2, object?>(name).InvokeAction(arg1, arg2);


	protected void InvokeAction<T1>(string name, T1 arg1)
		=> Plugin.PluginInterface.GetIpcSubscriber<T1, object?>(name).InvokeAction(arg1);

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
