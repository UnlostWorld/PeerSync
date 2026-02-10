// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Moodles;

using System;

public class PetNamesCommunicator : PluginCommunicatorBase
{
	protected override string InternalName => "PetRenamer";
	protected override Version Version => new Version(2, 8, 0, 0);

	public bool IsEnabled()
	{
		return this.InvokeFunc<bool>("PetRenamer.IsEnabled");
	}

	public string? GetPlayerData()
	{
		if (!this.GetIsAvailable() || !this.IsEnabled())
			return null;

		return this.InvokeFunc<string>("PetRenamer.GetPlayerData");
	}

	public void SetPlayerData(string data)
	{
		if (!this.GetIsAvailable() || !this.IsEnabled())
			return;

		this.InvokeAction(
			"PetRenamer.SetPlayerData",
			data);
	}

	public void ClearPlayerData(ushort index)
	{
		if (!this.GetIsAvailable() || !this.IsEnabled())
			return;

		this.InvokeAction(
			"PetRenamer.ClearPlayerData",
			index);
	}
}