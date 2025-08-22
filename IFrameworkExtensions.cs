// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioSync;

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

public static class IFrameworkExtensions
{
	public static RunOnUpdateCompletionSource RunOnUpdate(this IFramework self) => new();

	public static async Task Delay(this IFramework self, int ms)
	{
		await Task.Delay(ms);
		await self.RunOnUpdate();
	}

	public struct RunOnUpdateCompletionSource()
		: INotifyCompletion
	{
		public bool IsCompleted => Plugin.Framework.IsInFrameworkUpdateThread;

		public RunOnUpdateCompletionSource GetAwaiter() => this;
		public readonly void GetResult()
		{
		}

		public readonly void OnCompleted(Action continuation)
		{
			Plugin.Framework.RunOnFrameworkThread(continuation);
		}
	}
}