// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Moodles;

using System;
using Dalamud.Game.ClientState.Objects.SubKinds;

public class MoodlesCommunicator : PluginCommunicatorBase
{
	protected override string InternalName => "Moodles";
	protected override Version Version => new Version(1, 0, 0, 50);

	public int GetVersion()
	{
		return this.InvokeFunc<int>("Moodles.Version");
	}

	public string? GetStatusManagerByPC(IPlayerCharacter pc)
	{
		if (!this.GetIsAvailable() || this.GetVersion() != 3)
			return null;

		return this.InvokeFunc<string, IPlayerCharacter>(
			"Moodles.GetStatusManagerByPlayerV2",
			pc);
	}

	public void SetStatusManagerByPC(IPlayerCharacter pc, string data)
	{
		if (!this.GetIsAvailable() || this.GetVersion() != 3)
			return;

		this.InvokeAction(
			"Moodles.SetStatusManagerByPlayerV2",
			pc,
			data);
	}

	public void ClearStatusManager(IPlayerCharacter pc)
	{
		if (!this.GetIsAvailable() || this.GetVersion() != 3)
			return;

		this.InvokeAction(
			"Moodles.ClearStatusManagerByPlayerV2",
			pc);
	}
}