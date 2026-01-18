// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncBlockers;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using PeerSync.SyncProviders;

public class LightlessCommunicator : PluginCommunicatorBase
{
	protected override string InternalName => "LightlessSync";
	protected override Version Version => new(2, 0, 0, 0);

	public List<nint>? GetHandledGameObjects()
	{
		return this.InvokeFunc<List<nint>>("LightlessSync.GetHandledAddresses");
	}

	public async Task<bool> GetIsGameObjectHandled(int objectIndex)
	{
		if (!this.GetIsAvailable())
			return false;

		await Plugin.Framework.RunOnUpdate();

		IGameObject? character = Plugin.ObjectTable[objectIndex];
		if (character == null)
			return false;

		List<nint>? handled = this.GetHandledGameObjects();
		if (handled == null)
			return false;

		return handled.Contains(character.Address);
	}
}