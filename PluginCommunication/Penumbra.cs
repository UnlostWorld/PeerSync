// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeerSync.PluginCommunication;

public class Penumbra : PluginCommunicatorBase
{
	protected override string InternalName => "Penumbra";
	protected override Version Version => new Version(1, 2, 0, 22);

	public async Task<Dictionary<string, HashSet<string>>?> GetGameObjectResourcePaths(ushort objectIndex)
	{
		if (!this.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		Dictionary<string, HashSet<string>>?[]? objectsResourcePaths = this.Invoke<Dictionary<string, HashSet<string>>?[], ushort[]>("Penumbra.GetGameObjectResourcePaths.V5", [objectIndex]);

		if (objectsResourcePaths == null || objectsResourcePaths.Length <= 0)
			return null;

		return objectsResourcePaths[0];
	}
}