// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeerSync.PluginCommunication;

public class Glamourer : PluginCommunicatorBase
{
	protected override string InternalName => "Glamourer";
	protected override Version Version => new Version(1, 3, 0, 10);

	public async Task<string?> GetState(ushort objectIndex)
	{
		if (!this.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		(int status, string? base64) = this.Invoke<(int, string?), ushort>("Glamourer.GetStateBase64", objectIndex);
		return base64;
	}

	public async Task SetState(ushort objectIndex, string state, uint key = 0, ulong flags = 0x01)
	{
		if (!this.GetIsAvailable())
			return;

		await Plugin.Framework.RunOnUpdate();

		this.Invoke<int, string, int, uint, ulong>("Glamourer.GetStateBase64", state, objectIndex, key, flags);
		return;
	}
}