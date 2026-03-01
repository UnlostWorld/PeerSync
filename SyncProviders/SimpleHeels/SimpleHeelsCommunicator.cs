// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Moodles;

using System;

public class SimpleHeelsCommunicator : PluginCommunicatorBase
{
	protected override string InternalName => "SimpleHeels";
	protected override Version Version => new Version(0, 11, 0, 0);

	public string? GetPlayerData()
	{
		if (!this.GetIsAvailable())
			return null;

		return this.InvokeFunc<string>("SimpleHeels.GetLocalPlayer");
	}

	public void SetPlayerData(ushort index, string data)
	{
		if (!this.GetIsAvailable())
			return;

		this.InvokeAction(
			"SimpleHeels.RegisterPlayer",
			(int)index,
			data);
	}

	public void ClearPlayerData(ushort index)
	{
		if (!this.GetIsAvailable())
			return;

		this.InvokeAction(
			"SimpleHeels.UnregisterPlayer",
			(int)index);
	}
}